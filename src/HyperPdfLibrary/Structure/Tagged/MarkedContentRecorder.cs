// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// A content device that records each glyph a page draws, with its Unicode text, its box in viewer space and the
/// marked content it is drawn in, and the bounds of the paths and images each marked content id paints.
/// </summary>
/// <remarks>
/// Text is kept to what <see cref = "GlyphEvent"/> gives, so the recorder can move onto the shared text page once it exists.
/// </remarks>
[DebuggerDisplay("MarkedContentRecorder: {_glyphs.Count} glyphs")]
internal sealed class MarkedContentRecorder : IContentDevice
{
    /// <summary>How far ahead the scanned operators are searched for the one the interpreter just ran.</summary>
    private const int MaxLookahead = 64;

    /// <summary>The most glyphs or characters an idle recorder keeps room for.</summary>
    private const int MaxKeptCapacity = 1 << 16;

    /// <summary>Idle recorders, kept with the lists and text buffer they grew so recording another page does not grow them again.</summary>
    private static readonly ReusableSlots<MarkedContentRecorder> Idle = new();

    /// <summary>The page's marked content operators in running order.</summary>
    private readonly List<ScannedMark> _scanned = [];

    /// <summary>The open marked content levels, outermost first.</summary>
    private readonly List<MarkFrame> _frames = [];

    /// <summary>The glyphs recorded.</summary>
    private readonly List<PdfMarkedGlyph> _glyphs = [];

    /// <summary>The images recorded.</summary>
    private readonly List<PdfMarkedImage> _images = [];

    /// <summary>The straight lines recorded.</summary>
    private readonly List<LayoutRuling> _rulings = [];

    /// <summary>The glyphs' text, back to back.</summary>
    private readonly StringBuilder _text = new();

    /// <summary>Where each marked content id's bounds sit in <see cref="_graphicIds"/>; kept with the recorder and cleared for each page.</summary>
    private readonly Dictionary<int, int> _graphicIndex = [];

    /// <summary>The marked content ids that paint paths or images, in the order first painted.</summary>
    private readonly List<int> _graphicIds = [];

    /// <summary>The bounds of each id in <see cref="_graphicIds"/>.</summary>
    private readonly List<PdfViewerRect> _graphicBounds = [];

    /// <summary>The document's name table; null while the recorder sits idle.</summary>
    private PdfNameTable _names = null!;

    /// <summary>The next scanned operator to match.</summary>
    private int _next;

    /// <summary>The marked content id in force: the innermost open level with one, because content belongs to its closest marked-content sequence (ISO 32000-2 14.7.4.2).</summary>
    private int _mcid = -1;

    /// <summary>Whether an open level is an artifact.</summary>
    private bool _artifact;

    /// <summary>Whether the page draws an image.</summary>
    private bool _hasImages;

    /// <summary>The paths and images painted outside marked content with an id and outside artifacts.</summary>
    private int _untaggedGraphics;

    /// <summary>The marked content property lists whose <c>/Lang</c> is not a language identifier.</summary>
    private int _invalidLanguages;

    /// <inheritdoc/>
    public bool RetainsPaths => false;

    /// <inheritdoc/>
    public bool ReadsImagePixels => false;

    /// <inheritdoc/>
    public void Save()
    {
    }

    /// <inheritdoc/>
    public void Restore()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        AddGraphic(path.Bounds, state.Ctm);
        RulingCollector.AddFilled(path, state.Ctm, _rulings);
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
        AddGraphic(path.Bounds, state.Ctm);
        RulingCollector.AddStroked(path, state.Ctm, state.LineWidth, _rulings);
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state) => DrawImageArea(ref state);

    /// <inheritdoc/>
    public void DrawImageArea(ref GraphicsState state)
    {
        _hasImages = true;
        _images.Add(new(PdfViewerRect.Transform(0, 0, 1, 1, state.Ctm), _mcid, _artifact));
        AddGraphic(new(0, 0, 1, 1), state.Ctm, false);
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        var font = glyph.Font;
        var size = glyph.FontSize;
        var width = font.GetWidth(glyph.Code) * size * state.HorizontalScaling;
        var bounds = PdfViewerRect.Transform(0, state.Rise + (font.Descent * size), width, state.Rise + (font.Ascent * size), glyph.TextMatrix * state.Ctm);
        var em = font.Ascent - font.Descent;
        var start = _text.Length;
        _ = _text.Append(glyph.Unicode);
        _glyphs.Add(new(bounds, _mcid, _artifact, start, glyph.Unicode.Length, em > 0 ? bounds.Height / em : size));
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        var scanned = Match(tag);
        var mcid = properties?.GetInt32(KnownName.MCID, -1) ?? scanned.Mcid;
        _frames.Add(new(mcid, IsArtifactTag(tag)));
        if ((properties ?? scanned.Properties)?.GetText(KnownName.Lang) is { } language && !AccessibilityLanguageTags.IsValid(language))
        {
            _invalidLanguages++;
        }

        Refresh();
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        if (_frames.Count <= 0)
        {
            return;
        }

        _frames.RemoveAt(_frames.Count - 1);
        Refresh();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect cull) => new NullPictureDevice(cull);

    /// <summary>Records a page.</summary>
    /// <param name = "document">The document.</param>
    /// <param name = "page">The page.</param>
    /// <returns>The page's marked content.</returns>
    internal static PdfMarkedContentPage Record(PdfDocument document, PdfPage page)
    {
        var device = Idle.Take() ?? new();
        device._names = document.Objects.Names;
        try
        {
            // The scan and the interpreter read the same decoded content, so it is decoded once (or taken from the check's shared scope).
            using var content = PdfPageContentScope.Lease(document, page, CancellationToken.None);
            var images = MarkedContentScanner.Scan(document, page, content.Content, device._scanned, out var untaggedImages);
            using (var interpreter = ContentInterpreter.Rent(PdfDocumentRendering.GetRenderCache(document), device, 0))
            {
                ContentExecution.RunPage(interpreter, page, content.Content);
            }

            // The scan sees images the interpreter cannot decode, such as JBIG2 or JPEG 2000 scans.
            var untagged = device._untaggedGraphics + untaggedImages;
            var drawing = new PageDrawing(
                device._images.Count == 0 ? [] : device._images.ToArray(),
                device._rulings.Count == 0 ? [] : device._rulings.ToArray(),
                page.Width,
                page.Height);
            var counts = new PageAccessibilityCounts(untagged, device._invalidLanguages);
            return new(page.Index, device._text.ToString(), device._glyphs.ToArray(), device.TakeGraphics(), images || device._hasImages, drawing, counts);
        }
        finally
        {
            device.Release();
        }
    }

    /// <summary>Determines whether a marked content tag is <c>/Artifact</c>.</summary>
    /// <param name = "spelling">The tag's bytes.</param>
    /// <returns><see langword="true"/> for an artifact.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsArtifactTag(ReadOnlySpan<byte> spelling) => spelling.SequenceEqual("Artifact"u8);

    /// <summary>Finds the scanned operator the interpreter just ran, by its tag.</summary>
    /// <param name = "tag">The tag's bytes.</param>
    /// <returns>The operator, or one with no id when none matches.</returns>
    private ScannedMark Match(ReadOnlySpan<byte> tag)
    {
        var end = Math.Min(_scanned.Count, _next + MaxLookahead);
        for (var i = _next; i < end; i++)
        {
            if (!_names.NameEquals(_scanned[i].Tag, tag))
            {
                continue;
            }

            _next = i + 1;
            return _scanned[i];
        }

        return new(default, -1, null);
    }

    /// <summary>Works out the marked content id and artifact flag in force from the open levels.</summary>
    private void Refresh()
    {
        _mcid = -1;
        _artifact = false;
        foreach (var frame in CollectionsMarshal.AsSpan(_frames))
        {
            _artifact |= frame.IsArtifact;
            if (frame.Mcid >= 0)
            {
                _mcid = frame.Mcid;
            }
        }
    }

    /// <summary>Adds what a path or image covers to the marked content id in force.</summary>
    /// <param name = "bounds">The bounds in user space.</param>
    /// <param name = "ctm">The matrix from user space to viewer space.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddGraphic(PdfRect bounds, Matrix3x2 ctm) => AddGraphic(bounds, ctm, true);

    /// <summary>Adds what a path or image covers to the marked content id in force.</summary>
    /// <param name = "bounds">The bounds in user space.</param>
    /// <param name = "ctm">The matrix from user space to viewer space.</param>
    /// <param name = "countsUntagged">Whether painting outside marked content is counted here; images are counted by the scan.</param>
    private void AddGraphic(PdfRect bounds, Matrix3x2 ctm, bool countsUntagged)
    {
        if (_artifact)
        {
            return;
        }

        if (_mcid < 0)
        {
            _untaggedGraphics += countsUntagged ? 1 : 0;
            return;
        }

        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_graphicIndex, _mcid, out var exists);
        if (!exists)
        {
            slot = _graphicIds.Count;
            _graphicIds.Add(_mcid);
            _graphicBounds.Add(default);
        }

        ref var covered = ref CollectionsMarshal.AsSpan(_graphicBounds)[slot];
        covered = covered.Union(PdfViewerRect.Transform(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, ctm));
    }

    /// <summary>Makes the bounds of each marked content id's paths and images, sorted by id, for the page.</summary>
    /// <returns>The bounds; the shared empty value when no id painted a path or image.</returns>
    private McidGraphics TakeGraphics()
    {
        if (_graphicIds.Count == 0)
        {
            return McidGraphics.Empty;
        }

        var ids = _graphicIds.ToArray();
        var bounds = _graphicBounds.ToArray();
        Array.Sort(ids, bounds);
        return new(ids, bounds);
    }

    /// <summary>Forgets the page just recorded and keeps the recorder, with its grown buffers, for the next one.</summary>
    private void Release()
    {
        _scanned.Clear();
        _frames.Clear();
        _glyphs.Clear();
        _images.Clear();
        _rulings.Clear();
        _ = _text.Clear();
        _graphicIndex.Clear();
        _graphicIds.Clear();
        _graphicBounds.Clear();
        _names = null!;
        _next = 0;
        _mcid = -1;
        _artifact = false;
        _hasImages = false;
        _untaggedGraphics = 0;
        _invalidLanguages = 0;

        // A recorder that grew for an enormous page is dropped instead of pinning that memory.
        if (_text.Capacity <= MaxKeptCapacity && _glyphs.Capacity <= MaxKeptCapacity && _graphicIds.Capacity <= MaxKeptCapacity)
        {
            _ = Idle.Offer(this);
        }
    }
}

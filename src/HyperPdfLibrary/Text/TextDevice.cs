// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// A content device that collects shown glyphs into text runs, the counterparts of PDFium's text objects. Paths, images
/// and shadings are ignored. Each active extraction owns a device, which its document may reuse later.
/// </summary>
[DebuggerDisplay("TextDevice: {Runs.Count} runs, {Glyphs.Count} glyphs")]
internal sealed partial class TextDevice : ITextObjectDevice
{
    /// <summary>The initial capacity of the glyph list.</summary>
    private const int GlyphCapacity = 1024;

    /// <summary>The initial capacity of the run list.</summary>
    private const int RunCapacity = 128;

    /// <summary>The property lists of the open marked content levels, innermost last.</summary>
    private readonly List<PdfDictionary?> _marks = [];

    /// <summary>The matrix from the device space the interpreter draws in back to user space.</summary>
    private Matrix3x2 _userTransform = Matrix3x2.Identity;

    /// <summary>The marks of the open marked content levels, made when a run first needs them; null after a change.</summary>
    private TextMarks? _currentMarks = TextMarks.None;

    /// <summary>Whether the interpreter reports text object starts, so runs follow them instead of glyph positions.</summary>
    private bool _hooked;

    /// <summary>Whether a text object started since the last glyph.</summary>
    private bool _newObject;

    /// <summary>The drawing services of the page currently being collected.</summary>
    private PdfDrawingServices? _drawingServices;

    /// <summary>Gets the runs, in content order.</summary>
    internal List<TextRun> Runs { get; } = [with(RunCapacity)];

    /// <summary>Gets the glyphs of every run.</summary>
    internal List<TextGlyph> Glyphs { get; } = [with(GlyphCapacity)];

    /// <summary>Gets the Unicode text of every glyph.</summary>
    internal List<char> Unicode { get; } = [with(GlyphCapacity)];

    /// <inheritdoc/>
    public void Save()
    {
    }

    /// <inheritdoc/>
    public void Restore()
    {
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
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
    public void BeginTextObject()
    {
        _hooked = true;
        _newObject = true;
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        _marks.Add(properties);
        _currentMarks = null;
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        if (_marks.Count == 0)
        {
            return;
        }

        _marks.RemoveAt(_marks.Count - 1);
        _currentMarks = null;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect cull) => (_drawingServices ?? throw new InvalidOperationException("Reset the text device for a page first.")).Backend.CreatePictureDevice(cull);

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        var textToUser = Matrix3x2.CreateTranslation(0, state.Rise) * glyph.TextMatrix * state.Ctm * _userTransform;
        var matrix = Matrix3x2.CreateScale(state.HorizontalScaling, 1) * textToUser;
        if (!Continues(glyph.Font, glyph.FontSize, matrix, ref state))
        {
            CloseRun();
            OpenRun(glyph.Font, glyph.FontSize, matrix, ref state);
        }

        AddGlyph(glyph, matrix.Translation, ref state);
        _newObject = false;
    }

    /// <summary>Prepares the device for a page.</summary>
    /// <param name="page">The page; the interpreter draws it in viewer space, which the device maps back to user space.</param>
    internal void Reset(PdfPage page)
    {
        Clear();
        _drawingServices = PdfDrawingServices.For(page.Dictionary);
        _userTransform = page.UserTransform;
    }

    /// <summary>Closes the last run.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Finish() => CloseRun();

    /// <summary>Drops everything collected, releasing font and dictionary references.</summary>
    internal void Clear()
    {
        Runs.Clear();
        Glyphs.Clear();
        Unicode.Clear();
        _marks.Clear();
        _currentMarks = TextMarks.None;
        _hooked = false;
        _newObject = false;
        _open = false;
        _font = null;
        _runMarks = TextMarks.None;
    }

    /// <summary>Reads the /ActualText details of the open marked content levels.</summary>
    /// <param name="marks">The property lists, innermost last.</param>
    /// <returns>The marks.</returns>
    private static TextMarks ReadMarks(List<PdfDictionary?> marks)
    {
        if (marks.Count == 0)
        {
            return TextMarks.None;
        }

        PdfDictionary? last = null;
        PdfDictionary? withText = null;
        foreach (var properties in marks)
        {
            if (properties is null)
            {
                continue;
            }

            last = properties;
            if (properties.Get(KnownName.ActualText).Kind == PdfKind.String)
            {
                withText = properties;
            }
        }

        var actual = withText?.GetText(KnownName.ActualText);
        var lastText = ReferenceEquals(last, withText) ? actual : last?.GetText(KnownName.ActualText);
        return new(marks.Count, marks[^1], last, withText is not null, actual, lastText ?? string.Empty);
    }

    /// <summary>Gets the marks of the open marked content levels.</summary>
    /// <returns>The marks.</returns>
    private TextMarks CurrentMarks() => _currentMarks ??= ReadMarks(_marks);
}

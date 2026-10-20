// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// A content device that collects glyphs into runs (ISO 32000-2, 9.4 and 9.7). Opt-in visibility filtering tracks
/// clipping bounds and transparency groups; images and shadings are ignored. Each extraction owns a reusable device.
/// </summary>
[DebuggerDisplay("TextDevice: {Runs.Count} runs, {Glyphs.Count} glyphs")]
internal sealed class TextDevice : ITextObjectDevice
{
    /// <summary>The initial capacity of the glyph list.</summary>
    private const int GlyphCapacity = 1024;

    /// <summary>The initial capacity of the run list.</summary>
    private const int RunCapacity = 128;

    /// <summary>The mask that keeps the red, green and blue channels of a packed colour.</summary>
    private const uint RgbMask = 0xFFFFFF;

    /// <summary>The most points a path may have to be kept as a mark: a rectangle or a line, not a drawing.</summary>
    private const int MarkPointLimit = 6;

    /// <summary>The most marks kept for a page, so a page of many small shapes cannot grow the list without limit.</summary>
    private const int MarkLimit = 8192;

    /// <summary>The tallest mark kept, in points; a highlight is no taller than a few lines of text.</summary>
    private const float MarkHeightLimit = 144;

    /// <summary>How far, as a share of the font size, a glyph may sit off the run's baseline or behind the pen and still continue the run.</summary>
    private const float ContinueShare = 0.01F;

    /// <summary>The largest difference between matrix entries of glyphs in one run.</summary>
    private const float MatrixTolerance = 0.0001F;

    /// <summary>Half, for centring a vertical glyph cell on its pen.</summary>
    private const float Half = 0.5F;

    /// <summary>The replacement text of each open marked-content level, innermost last; null for levels without any.</summary>
    private readonly List<TextMarks?> _levels = [];

    /// <summary>The saved clipping bounds, reused with this device.</summary>
    private readonly List<PdfRect> _clips = [];

    /// <summary>The parent group opacities, reused with this device.</summary>
    private readonly List<float> _groupAlphas = [];

    /// <summary>The matrix from the device space the interpreter draws in back to user space.</summary>
    private Matrix3x2 _userTransform = Matrix3x2.Identity;

    /// <summary>The marks of the open levels, made when a run first needs them; null after a change.</summary>
    private TextMarks? _currentMarks = TextMarks.None;

    /// <summary>The number of replacement-text sequences opened so far.</summary>
    private int _sequences;

    /// <summary>The drawing services of the page currently being collected.</summary>
    private PdfDrawingServices? _drawingServices;

    /// <summary>Whether a run is open.</summary>
    private bool _open;

    /// <summary>What the open run is drawn with: its font, size, matrix, render mode and colours.</summary>
    private GlyphStyle _style;

    /// <summary>Whether the open run writes vertically.</summary>
    private bool _vertical;

    /// <summary>The inverse of the open run's matrix.</summary>
    private Matrix3x2 _inverse;

    /// <summary>The open run's marks.</summary>
    private TextMarks _runMarks = TextMarks.None;

    /// <summary>The open run's first glyph.</summary>
    private int _first;

    /// <summary>Where the next glyph of the open run would start if nothing moved the text position.</summary>
    private float _nextPen;

    /// <summary>The conservative clipping bounds in device space.</summary>
    private PdfRect _clip;

    /// <summary>The cumulative transparency group opacity.</summary>
    private float _groupAlpha = 1;

    /// <summary>Gets the opt-in filters for this extraction.</summary>
    internal PdfTextExtractionOptions Options { get; private set; }

    /// <summary>Gets the explicit token for this extraction.</summary>
    internal CancellationToken CancellationToken { get; private set; }

    /// <summary>Gets the runs, in content order.</summary>
    internal List<TextRun> Runs { get; } = [with(RunCapacity)];

    /// <summary>Gets the glyphs of every run.</summary>
    internal List<TextGlyph> Glyphs { get; } = [with(GlyphCapacity)];

    /// <summary>Gets the Unicode text of every glyph.</summary>
    internal List<char> Unicode { get; } = [with(GlyphCapacity)];

    /// <summary>Gets the small lines and boxes the page draws, in the order it draws them.</summary>
    internal List<TextMark> Marks { get; } = [];

    /// <inheritdoc/>
    public void Save()
    {
        if (Options.SkipNonVisibleText)
        {
            _clips.Add(_clip);
        }
    }

    /// <inheritdoc/>
    public void Restore()
    {
        if (_clips.Count == 0)
        {
            return;
        }

        _clip = _clips[^1];
        _clips.RemoveAt(_clips.Count - 1);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state) => AddMark(path, ref state, state.Fill, 0);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Stroke(PdfPath path, ref GraphicsState state) => AddMark(path, ref state, state.Stroke, state.LineWidth);

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
        if (Options.SkipNonVisibleText)
        {
            _clip = TextVisibility.Intersect(_clip, path, ctm, CancellationToken);
        }
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
        if (!Options.SkipNonVisibleText)
        {
            return;
        }

        _groupAlphas.Add(_groupAlpha);
        _groupAlpha *= group.Alpha;
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
        if (_groupAlphas.Count == 0)
        {
            return;
        }

        _groupAlpha = _groupAlphas[^1];
        _groupAlphas.RemoveAt(_groupAlphas.Count - 1);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void BeginTextObject() => Finish();

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
        var text = properties is not null && properties.Get(KnownName.ActualText).Kind == PdfKind.String ? properties.GetText(KnownName.ActualText) : null;
        if (text is not null)
        {
            _sequences++;
        }

        _levels.Add(text is null ? null : new(_sequences, text));
        _currentMarks = null;
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
        if (_levels.Count == 0)
        {
            return;
        }

        _levels.RemoveAt(_levels.Count - 1);
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
        if (Options.SkipNonVisibleText && (_groupAlpha <= 0 || !TextVisibility.CanPaint(glyph, ref state, _clip, matrix)))
        {
            CloseRun();
            return;
        }

        var style = new GlyphStyle(glyph.Font, glyph.FontSize, matrix, state.RenderMode, PaintOf(state.Fill), PaintOf(state.Stroke));
        if (!Continues(style))
        {
            CloseRun();
            OpenRun(style);
        }

        AddGlyph(glyph, matrix.Translation, state.HorizontalScaling);
    }

    /// <summary>Prepares the device for a page.</summary>
    /// <param name="page">The page; the interpreter draws it in viewer space, which the device maps back to user space.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Reset(PdfPage page) => Reset(page, default, CancellationToken.None);

    /// <summary>Prepares the device with explicit filtering and cancellation.</summary>
    /// <param name="page">The page.</param>
    /// <param name="options">The opt-in filters.</param>
    /// <param name="cancellationToken">Cancels extraction.</param>
    internal void Reset(PdfPage page, PdfTextExtractionOptions options, CancellationToken cancellationToken)
    {
        Clear();
        Options = options;
        CancellationToken = cancellationToken;
        _clip = new(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);
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
        Marks.Clear();
        _levels.Clear();
        _clips.Clear();
        _groupAlphas.Clear();
        _groupAlpha = 1;
        Options = default;
        CancellationToken = default;
        _currentMarks = TextMarks.None;
        _sequences = 0;
        _drawingServices = null;
        _open = false;
        _style = default;
        _runMarks = TextMarks.None;
    }

    /// <summary>Gets the colour a glyph is painted with, as 0xRRGGBB. A pattern paints no single colour, so it counts as 0.</summary>
    /// <param name="color">The fill or stroke colour.</param>
    /// <returns>The colour.</returns>
    private static uint PaintOf(in ColorState color) => color.Pattern is null ? color.Rgb & RgbMask : 0;

    /// <summary>Determines whether two matrices share their scale, rotation and skew.</summary>
    /// <param name="first">The first matrix.</param>
    /// <param name="second">The second matrix.</param>
    /// <returns><see langword="true"/> when the linear parts match.</returns>
    private static bool SameLinear(Matrix3x2 first, Matrix3x2 second) =>
        MathF.Abs(first.M11 - second.M11) <= MatrixTolerance
        && MathF.Abs(first.M12 - second.M12) <= MatrixTolerance
        && MathF.Abs(first.M21 - second.M21) <= MatrixTolerance
        && MathF.Abs(first.M22 - second.M22) <= MatrixTolerance;

    /// <summary>Gets a glyph's outline bounds in text space units, or an empty rectangle for a blank glyph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The bounds relative to the glyph origin.</returns>
    private static PdfRectangle OutlineOf(PdfFont font, int code, float fontSize)
    {
        if (font.GetOutline(code) is not { } outline || outline.Bounds.IsEmpty)
        {
            return default;
        }

        var bounds = outline.Bounds;
        return TextGeometry.TransformRect(font.FontMatrix * Matrix3x2.CreateScale(fontSize), new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }

    /// <summary>Moves a glyph-relative rectangle to a glyph origin, keeping a blank outline empty.</summary>
    /// <param name="outline">The outline bounds relative to the origin.</param>
    /// <param name="origin">The origin.</param>
    /// <returns>The moved rectangle.</returns>
    private static PdfRectangle Offset(in PdfRectangle outline, Vector2 origin) =>
        TextGeometry.IsEmpty(outline) ? default : new(outline.Left + origin.X, outline.Bottom + origin.Y, outline.Right + origin.X, outline.Top + origin.Y);

    /// <summary>Keeps a small shape the page draws, which may be the underline, strikeout or highlight of the text near it.</summary>
    /// <param name="path">The path, valid only during the call.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="color">The paint of the fill or stroke.</param>
    /// <param name="lineWidth">The stroke's line width, or 0 for a fill.</param>
    private void AddMark(PdfPath path, ref GraphicsState state, in ColorState color, float lineWidth)
    {
        if (path.IsEmpty || path.PointCount > MarkPointLimit || Marks.Count >= MarkLimit || color.PaintsNothing || color.Pattern is not null)
        {
            return;
        }

        var matrix = state.Ctm * _userTransform;
        var bounds = path.Bounds;
        var rect = TextGeometry.Normalize(TextGeometry.TransformRect(matrix, new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom)));
        var grow = lineWidth * MathF.Sqrt(MathF.Abs(matrix.GetDeterminant())) * Half;
        rect = new(rect.Left - grow, rect.Bottom - grow, rect.Right + grow, rect.Top + grow);
        if (rect.Height <= MarkHeightLimit && rect.Width > 0 && rect.Height >= 0)
        {
            Marks.Add(new(rect, color.Rgb & RgbMask));
        }
    }

    /// <summary>Determines whether a glyph continues the open run.</summary>
    /// <param name="style">What the glyph is drawn with.</param>
    /// <returns><see langword="true"/> when the glyph belongs to the open run.</returns>
    private bool Continues(in GlyphStyle style)
    {
        if (!SameState(style))
        {
            return false;
        }

        // A glyph on the run's baseline at or past the pen continues it, and any gap is a text adjustment.
        var local = Vector2.Transform(style.Matrix.Translation, _inverse);
        var along = _vertical ? local.Y : local.X;
        var across = _vertical ? local.X : local.Y;
        var behind = _vertical ? along - _nextPen : _nextPen - along;
        var tolerance = ContinueShare * MathF.Abs(style.FontSize);
        return MathF.Abs(across) <= tolerance && behind <= tolerance;
    }

    /// <summary>Determines whether the text state of a glyph matches the open run's.</summary>
    /// <param name="style">What the glyph is drawn with.</param>
    /// <returns><see langword="true"/> when nothing that shapes a run changed.</returns>
    private bool SameState(in GlyphStyle style) =>
        _open
        && ReferenceEquals(style.Font, _style.Font)
        && TextGeometry.Same(style.FontSize, _style.FontSize)
        && style.RenderMode == _style.RenderMode
        && style.FillColor == _style.FillColor
        && style.StrokeColor == _style.StrokeColor
        && CurrentMarks().Id == _runMarks.Id
        && SameLinear(style.Matrix, _style.Matrix);

    /// <summary>Starts a run at a glyph.</summary>
    /// <param name="style">What the glyph is drawn with.</param>
    private void OpenRun(in GlyphStyle style)
    {
        _open = true;
        _style = style;
        _vertical = style.Font!.IsVertical;
        _inverse = TextGeometry.Invert(style.Matrix);
        _runMarks = CurrentMarks();
        _first = Glyphs.Count;
        _nextPen = 0;
    }

    /// <summary>Adds the open run to the run list.</summary>
    private void CloseRun()
    {
        if (!_open)
        {
            return;
        }

        _open = false;
        var count = Glyphs.Count - _first;
        if (count > 0)
        {
            Runs.Add(new(_style.Font!, _style.FontSize, _style.RenderMode, _style.Matrix, _first, count, _runMarks, _style.FillColor, _style.StrokeColor));
        }
    }

    /// <summary>Adds a glyph to the open run.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="pen">The text position of the glyph in user space.</param>
    /// <param name="scaling">The horizontal scaling.</param>
    private void AddGlyph(in GlyphEvent glyph, Vector2 pen, float scaling)
    {
        var font = glyph.Font;
        var fontSize = _style.FontSize;
        var along = 0F;
        if (Glyphs.Count > _first)
        {
            var local = Vector2.Transform(pen, _inverse);
            along = _vertical ? local.Y : local.X;
        }

        var move = _vertical || scaling == 0 ? glyph.Advance : glyph.Advance / scaling;
        var outline = OutlineOf(font, glyph.Code, fontSize);
        TextGlyph entry;
        if (_vertical)
        {
            font.GetVerticalMetrics(glyph.Code, out var advance, out var originX, out var originY);
            var origin = new Vector2(-originX * fontSize, along - (originY * fontSize));
            var half = fontSize * Half;
            var cell = new PdfRectangle(-half, along + (advance * fontSize), half, along);
            entry = new(glyph.Code, Unicode.Count, glyph.Unicode.Length, along, move, origin, Offset(outline, origin), cell);
        }
        else
        {
            var cell = new PdfRectangle(along, font.Descent * fontSize, along + (font.GetWidth(glyph.Code) * fontSize), font.Ascent * fontSize);
            entry = new(glyph.Code, Unicode.Count, glyph.Unicode.Length, along, move, new(along, 0), Offset(outline, new(along, 0)), cell);
        }

        _nextPen = along + move;
        Glyphs.Add(entry);
        Unicode.AddRange(glyph.Unicode);
    }

    /// <summary>Gets the replacement text that covers the open marked-content levels: the innermost level that has some.</summary>
    /// <returns>The marks.</returns>
    private TextMarks CurrentMarks() => _currentMarks ??= InnermostMarks();

    /// <summary>Finds the innermost open level that has replacement text.</summary>
    /// <returns>The marks, or <see cref="TextMarks.None"/>.</returns>
    private TextMarks InnermostMarks()
    {
        for (var i = _levels.Count - 1; i >= 0; i--)
        {
            if (_levels[i] is { } level)
            {
                return level;
            }
        }

        return TextMarks.None;
    }
}

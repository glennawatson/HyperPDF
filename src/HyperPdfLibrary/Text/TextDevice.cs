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
/// A content device that collects the glyphs a page shows into runs (ISO 32000-2, 9.4 and 9.7). Paths, images and
/// shadings are ignored. Each active extraction owns a device, which its document may reuse later.
/// </summary>
[DebuggerDisplay("TextDevice: {Runs.Count} runs, {Glyphs.Count} glyphs")]
internal sealed class TextDevice : ITextObjectDevice
{
    /// <summary>The initial capacity of the glyph list.</summary>
    private const int GlyphCapacity = 1024;

    /// <summary>The initial capacity of the run list.</summary>
    private const int RunCapacity = 128;

    /// <summary>How far, as a share of the font size, a glyph may sit off the run's baseline or behind the pen and still continue the run.</summary>
    private const float ContinueShare = 0.01F;

    /// <summary>The largest difference between matrix entries of glyphs in one run.</summary>
    private const float MatrixTolerance = 0.0001F;

    /// <summary>Half, for centring a vertical glyph cell on its pen.</summary>
    private const float Half = 0.5F;

    /// <summary>The replacement text of each open marked-content level, innermost last; null for levels without any.</summary>
    private readonly List<TextMarks?> _levels = [];

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

    /// <summary>The open run's font.</summary>
    private PdfFont? _font;

    /// <summary>The open run's font size.</summary>
    private float _fontSize;

    /// <summary>The open run's render mode.</summary>
    private int _mode;

    /// <summary>Whether the open run writes vertically.</summary>
    private bool _vertical;

    /// <summary>The open run's matrix.</summary>
    private Matrix3x2 _matrix;

    /// <summary>The inverse of the open run's matrix.</summary>
    private Matrix3x2 _inverse;

    /// <summary>The open run's marks.</summary>
    private TextMarks _runMarks = TextMarks.None;

    /// <summary>The open run's first glyph.</summary>
    private int _first;

    /// <summary>Where the next glyph of the open run would start if nothing moved the text position.</summary>
    private float _nextPen;

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
        if (!Continues(glyph.Font, glyph.FontSize, matrix, state.RenderMode))
        {
            CloseRun();
            OpenRun(glyph.Font, glyph.FontSize, matrix, state.RenderMode);
        }

        AddGlyph(glyph, matrix.Translation, state.HorizontalScaling);
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
        _levels.Clear();
        _currentMarks = TextMarks.None;
        _sequences = 0;
        _drawingServices = null;
        _open = false;
        _font = null;
        _runMarks = TextMarks.None;
    }

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

    /// <summary>Determines whether a glyph continues the open run.</summary>
    /// <param name="font">The glyph's font.</param>
    /// <param name="fontSize">The glyph's font size.</param>
    /// <param name="matrix">The glyph's run matrix.</param>
    /// <param name="mode">The render mode.</param>
    /// <returns><see langword="true"/> when the glyph belongs to the open run.</returns>
    private bool Continues(PdfFont font, float fontSize, Matrix3x2 matrix, int mode)
    {
        if (!SameState(font, fontSize, matrix, mode))
        {
            return false;
        }

        // A glyph on the run's baseline at or past the pen continues it, and any gap is a text adjustment.
        var local = Vector2.Transform(matrix.Translation, _inverse);
        var along = _vertical ? local.Y : local.X;
        var across = _vertical ? local.X : local.Y;
        var behind = _vertical ? along - _nextPen : _nextPen - along;
        var tolerance = ContinueShare * MathF.Abs(fontSize);
        return MathF.Abs(across) <= tolerance && behind <= tolerance;
    }

    /// <summary>Determines whether the text state of a glyph matches the open run's.</summary>
    /// <param name="font">The glyph's font.</param>
    /// <param name="fontSize">The glyph's font size.</param>
    /// <param name="matrix">The glyph's run matrix.</param>
    /// <param name="mode">The render mode.</param>
    /// <returns><see langword="true"/> when nothing that shapes a run changed.</returns>
    private bool SameState(PdfFont font, float fontSize, Matrix3x2 matrix, int mode) =>
        _open
        && ReferenceEquals(font, _font)
        && TextGeometry.Same(fontSize, _fontSize)
        && mode == _mode
        && CurrentMarks().Id == _runMarks.Id
        && SameLinear(matrix, _matrix);

    /// <summary>Starts a run at a glyph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="matrix">The run matrix.</param>
    /// <param name="mode">The render mode.</param>
    private void OpenRun(PdfFont font, float fontSize, Matrix3x2 matrix, int mode)
    {
        _open = true;
        _font = font;
        _fontSize = fontSize;
        _mode = mode;
        _vertical = font.IsVertical;
        _matrix = matrix;
        _inverse = TextGeometry.Invert(matrix);
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
            Runs.Add(new(_font!, _fontSize, _mode, _matrix, _first, count, _runMarks));
        }
    }

    /// <summary>Adds a glyph to the open run.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="pen">The text position of the glyph in user space.</param>
    /// <param name="scaling">The horizontal scaling.</param>
    private void AddGlyph(in GlyphEvent glyph, Vector2 pen, float scaling)
    {
        var font = glyph.Font;
        var fontSize = _fontSize;
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

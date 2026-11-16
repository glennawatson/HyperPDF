// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Engine;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdf.Viewer.Text;

/// <summary>One font face measured and drawn for the window's own text: the bundled screen font, or a system font that has characters it lacks.</summary>
[DebuggerDisplay("UiFont: {_face.Source.FamilyName}")]
internal sealed class UiFont
{
    /// <summary>The family every window text is set in first. It ships inside the application, so text looks the same on every system.</summary>
    private const string Family = "Arimo";

    /// <summary>The face.</summary>
    private readonly ScreenFontFace _face;

    /// <summary>The shaper that reads the face's own layout tables, made the first time text needs shaping.</summary>
    private PdfOpenTypeShaper? _shaper;

    /// <summary>Initializes a new instance of the <see cref="UiFont"/> class.</summary>
    /// <param name="face">The face.</param>
    internal UiFont(ScreenFontFace face) => _face = face;

    /// <summary>Gets the face's outlines and metrics.</summary>
    private TrueTypeProgram Program => _face.Program;

    /// <summary>Loads a face of the bundled screen font.</summary>
    /// <param name="bold">Whether the face is bold.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled font is missing or damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static UiFont? Load(bool bold) => Load(Family, bold);

    /// <summary>Loads a face of a family of the bundled screen fonts.</summary>
    /// <param name="family">The family: Arimo, Tinos or Cousine.</param>
    /// <param name="bold">Whether the face is bold.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled font is missing or damaged.</returns>
    internal static UiFont? Load(string family, bool bold) => ScreenFontFace.OpenBundled(family, bold) is { } face ? new UiFont(face) : null;

    /// <summary>Gets how far the tallest letters rise above the baseline.</summary>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The rise in pixels.</returns>
    internal float Ascent(float size) => Program.Ascent * size / Program.UnitsPerEm;

    /// <summary>Gets how far the lowest letters hang below the baseline.</summary>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The drop in pixels, as a positive number.</returns>
    internal float Descent(float size) => MathF.Abs(Program.Descent) * size / Program.UnitsPerEm;

    /// <summary>Tells whether the face has a glyph for a character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Has(Rune rune) => Program.GetGlyphByUnicode(rune.Value) > 0;

    /// <summary>Tells whether the face has a glyph for every character of a piece of text, so it can be set without shaping or another font.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has them all.</returns>
    internal bool HasAll(ReadOnlySpan<char> text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Has(rune))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Measures a piece of text set one character after another.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The width in pixels.</returns>
    internal float Measure(ReadOnlySpan<char> text, float size)
    {
        var units = 0F;
        foreach (var rune in text.EnumerateRunes())
        {
            units += Program.GetAdvanceWidth(GlyphOf(rune));
        }

        return units * size / Program.UnitsPerEm;
    }

    /// <summary>Adds the outlines of a piece of text, set one character after another, to a coverage buffer.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <param name="origin">Where the text starts and where its baseline is, in pixels.</param>
    /// <param name="buffer">The coverage buffer.</param>
    internal void Draw(ReadOnlySpan<char> text, float size, TextOrigin origin, CoverageBuffer buffer)
    {
        var x = origin.X;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = GlyphOf(rune);
            DrawGlyph(glyph, size, new(x, origin.Baseline), buffer);
            x += Program.GetAdvanceWidth(glyph) * size / Program.UnitsPerEm;
        }
    }

    /// <summary>Shapes a piece of text with the face's own layout tables: joined, reordered and kerned as its script needs, in the order it is drawn.</summary>
    /// <param name="text">The text.</param>
    /// <param name="glyphs">Receives the glyphs, left to right.</param>
    internal void Shape(ReadOnlySpan<char> text, List<PdfShapedGlyph> glyphs) =>
        _ = (_shaper ??= new(_face.Source)).Shape(text, PdfShapingOptions.Default, glyphs);

    /// <summary>Adds the outline of one glyph to a coverage buffer.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <param name="origin">Where the glyph's origin and baseline are, in pixels.</param>
    /// <param name="buffer">The coverage buffer.</param>
    internal void DrawGlyph(int glyph, float size, TextOrigin origin, CoverageBuffer buffer)
    {
        var pen = new OutlinePen(buffer, size / Program.UnitsPerEm, origin.X, origin.Baseline);
        Program.DecodeGlyph(glyph, ref pen);
        pen.Close();
    }

    /// <summary>Finds the glyph of a character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The glyph, or the font's missing glyph when it has none for the character.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GlyphOf(Rune rune) => Math.Max(Program.GetGlyphByUnicode(rune.Value), 0);
}

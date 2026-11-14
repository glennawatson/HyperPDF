// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdf.Engine;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdf.Viewer.Text;

/// <summary>One face of the bundled screen font, measured and drawn for the window's own text: tabs, buttons, menus and dialogs.</summary>
[DebuggerDisplay("UiFont: {_program.GlyphCount} glyphs")]
internal sealed class UiFont
{
    /// <summary>The family every window text is set in. It ships inside the application, so text looks the same on every system.</summary>
    private const string Family = "Arimo";

    /// <summary>The font program.</summary>
    private readonly TrueTypeProgram _program;

    /// <summary>Initializes a new instance of the <see cref="UiFont"/> class.</summary>
    /// <param name="program">The font program.</param>
    private UiFont(TrueTypeProgram program) => _program = program;

    /// <summary>Loads a face of the bundled screen font.</summary>
    /// <param name="bold">Whether the face is bold.</param>
    /// <returns>The face, or <see langword="null"/> when the bundled font is missing or damaged.</returns>
    internal static UiFont? Load(bool bold) =>
        BundledScreenFonts.Read(Family, bold, false) is { } data && TrueTypeProgram.TryParse(data, out var program) ? new UiFont(program) : null;

    /// <summary>Gets how far the tallest letters rise above the baseline.</summary>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The rise in pixels.</returns>
    internal float Ascent(float size) => _program.Ascent * size / _program.UnitsPerEm;

    /// <summary>Gets how far the lowest letters hang below the baseline.</summary>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The drop in pixels, as a positive number.</returns>
    internal float Descent(float size) => MathF.Abs(_program.Descent) * size / _program.UnitsPerEm;

    /// <summary>Measures a piece of text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The width in pixels.</returns>
    internal float Measure(ReadOnlySpan<char> text, float size)
    {
        var units = 0F;
        foreach (var rune in text.EnumerateRunes())
        {
            units += _program.GetAdvanceWidth(GlyphOf(rune));
        }

        return units * size / _program.UnitsPerEm;
    }

    /// <summary>Adds the outlines of a piece of text to a coverage buffer.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <param name="origin">Where the text starts and where its baseline is, in pixels.</param>
    /// <param name="buffer">The coverage buffer.</param>
    internal void Draw(ReadOnlySpan<char> text, float size, TextOrigin origin, CoverageBuffer buffer)
    {
        var scale = size / _program.UnitsPerEm;
        var x = origin.X;
        foreach (var rune in text.EnumerateRunes())
        {
            var glyph = GlyphOf(rune);
            var pen = new OutlinePen(buffer, scale, x, origin.Baseline);
            _program.DecodeGlyph(glyph, ref pen);
            pen.Close();
            x += _program.GetAdvanceWidth(glyph) * scale;
        }
    }

    /// <summary>Finds the glyph of a character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The glyph, or the font's missing glyph when it has none for the character.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GlyphOf(Rune rune) => Math.Max(_program.GetGlyphByUnicode(rune.Value), 0);
}

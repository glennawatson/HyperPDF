// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Viewer.Text;

/// <summary>
/// Lays out a piece of text that the bundled screen font cannot set alone. The text is cut where the font changes, each part is
/// shaped with its own font, so Arabic joins, Indic syllables reorder and right-to-left parts run the right way, and the parts
/// are placed one after another.
/// </summary>
[DebuggerDisplay("TextLayout: {_placed.Count} glyphs")]
internal sealed class TextLayout
{
    /// <summary>The system fonts used for characters the first font lacks.</summary>
    private readonly FallbackFonts _fallback = new();

    /// <summary>The glyphs of the part being shaped, reused for every part.</summary>
    private readonly List<PdfShapedGlyph> _shaped = [];

    /// <summary>The glyphs of the text laid out last.</summary>
    private readonly List<PlacedGlyph> _placed = [];

    /// <summary>Gets the glyphs of the text laid out last, in the order they are drawn.</summary>
    internal List<PlacedGlyph> Placed => _placed;

    /// <summary>Lays out a piece of text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="first">The font tried first for every character.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <returns>The width of the text in pixels.</returns>
    internal float Run(ReadOnlySpan<char> text, UiFont first, float size)
    {
        _placed.Clear();
        var width = 0F;
        var start = 0;
        var at = 0;
        UiFont? current = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var font = Pick(rune, first, current);
            if (current is not null && !ReferenceEquals(font, current))
            {
                width = Place(text[start..at], current, size, width);
                start = at;
            }

            current = font;
            at += rune.Utf16SequenceLength;
        }

        return current is null ? width : Place(text[start..], current, size, width);
    }

    /// <summary>Tells whether a character belongs with the one before it: a space, a joining mark or an invisible control.</summary>
    /// <param name="rune">The character.</param>
    /// <returns><see langword="true"/> when it takes the font of the text around it.</returns>
    private static bool FollowsNeighbour(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.SpaceSeparator or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
        or UnicodeCategory.Format or UnicodeCategory.Control;

    /// <summary>Picks the font for a character: the first font when it has the character, else a system font that has it.</summary>
    /// <param name="rune">The character.</param>
    /// <param name="first">The font tried first.</param>
    /// <param name="current">The font of the character before, or <see langword="null"/> at the start.</param>
    /// <returns>The font.</returns>
    private UiFont Pick(Rune rune, UiFont first, UiFont? current)
    {
        // A mark or a space between two characters of another script stays in their font, so the part is shaped as one.
        if (current is not null && FollowsNeighbour(rune) && (current.Has(rune) || !first.Has(rune)))
        {
            return current;
        }

        return first.Has(rune) ? first : _fallback.Find(rune) ?? first;
    }

    /// <summary>Shapes one part of the text with its font and places its glyphs after what was placed before.</summary>
    /// <param name="part">The part.</param>
    /// <param name="font">Its font.</param>
    /// <param name="size">The text size in pixels.</param>
    /// <param name="x">Where the part starts, in pixels.</param>
    /// <returns>Where the part ends.</returns>
    private float Place(ReadOnlySpan<char> part, UiFont font, float size, float x)
    {
        _shaped.Clear();
        font.Shape(part, _shaped);
        foreach (var glyph in _shaped)
        {
            _placed.Add(new(font, glyph.Glyph, x + (glyph.OffsetX * size), glyph.OffsetY * size));
            x += glyph.Advance * size;
        }

        return x;
    }
}

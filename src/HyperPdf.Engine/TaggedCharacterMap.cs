// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>
/// Matches the glyphs the managed library recorded to the page characters the document's text source gives, both in
/// drawing order, so tagged blocks can name characters by the indices the reading view uses. Generated characters
/// (inserted spaces and line breaks) are never matched. When text extraction moves onto the same glyph list, every
/// glyph matches its own index and this map becomes the identity.
/// </summary>
internal static class TaggedCharacterMap
{
    /// <summary>How many characters ahead a glyph's character is looked for, to step over dropped or reordered ones.</summary>
    private const int Window = 16;

    /// <summary>Matches each glyph to its characters.</summary>
    /// <param name="content">The recorded glyphs.</param>
    /// <param name="characters">The page characters.</param>
    /// <returns>Each glyph's characters.</returns>
    internal static GlyphCharacters[] Create(PdfMarkedContentPage content, List<PageCharacter> characters)
    {
        var map = new GlyphCharacters[content.GlyphCount];
        var next = 0;
        for (var i = 0; i < map.Length; i++)
        {
            var match = Match(content.GetText(i), characters, next);
            if (match.Start < 0)
            {
                map[i] = match;
                continue;
            }

            map[i] = IncludesGeneratedSpace(content, characters, map, i, match) ? new(match.Start - 1, match.Count + 1) : match;
            next = match.Start + match.Count;
        }

        return map;
    }

    /// <summary>Appends a glyph's characters.</summary>
    /// <param name="map">The map.</param>
    /// <param name="glyph">The glyph index.</param>
    /// <param name="output">Receives the character indices.</param>
    internal static void Append(GlyphCharacters[] map, int glyph, List<int> output)
    {
        if ((uint)glyph >= (uint)map.Length)
        {
            return;
        }

        var (start, count) = map[glyph];
        for (var i = 0; i < count; i++)
        {
            output.Add(start + i);
        }
    }

    /// <summary>
    /// Determines whether a space the extractor generated between a glyph and the one before it belongs to the glyph.
    /// A space between two glyphs of one marked content id counts in that marked content; between two ids it belongs
    /// to neither.
    /// </summary>
    /// <param name="content">The recorded glyphs.</param>
    /// <param name="characters">The page characters.</param>
    /// <param name="map">The matches so far.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="match">The glyph's match.</param>
    /// <returns><see langword="true"/> when the character just before the match is such a space.</returns>
    private static bool IncludesGeneratedSpace(PdfMarkedContentPage content, List<PageCharacter> characters, GlyphCharacters[] map, int glyph, GlyphCharacters match)
    {
        if (glyph == 0 || match.Start < 1 || map[glyph - 1].Start < 0)
        {
            return false;
        }

        var (previousStart, previousCount) = map[glyph - 1];
        var space = characters[match.Start - 1];
        return previousStart + previousCount == match.Start - 1
            && space.Generated
            && space.Value == ' '
            && content.Glyphs[glyph - 1].Mcid == content.Glyphs[glyph].Mcid;
    }

    /// <summary>Finds a glyph's characters at or after a position.</summary>
    /// <param name="text">The glyph's text; empty when the font gives none.</param>
    /// <param name="characters">The page characters.</param>
    /// <param name="from">The first character that may match.</param>
    /// <returns>The match, or one starting at -1.</returns>
    private static GlyphCharacters Match(ReadOnlySpan<char> text, List<PageCharacter> characters, int from)
    {
        if (!text.IsEmpty && char.IsWhiteSpace(text[0]))
        {
            return MatchSpace(characters, from);
        }

        var seen = 0;
        for (var i = from; i < characters.Count && seen < Window; i++)
        {
            var character = characters[i];
            if (character.Generated)
            {
                continue;
            }

            if (text.IsEmpty)
            {
                // A glyph with no text matches the next real character by position.
                return new(i, 1);
            }

            if (character.Value == text[0])
            {
                return new(i, Extend(text, characters, i));
            }

            seen++;
        }

        return new(-1, 0);
    }

    /// <summary>
    /// Matches a drawn space to the very next character when that is a space. A generated space matches too, because
    /// it may stand in for a drawn one when spaces are collapsed; generated line breaks are skipped.
    /// </summary>
    /// <param name="characters">The page characters.</param>
    /// <param name="from">The first character that may match.</param>
    /// <returns>The match, or one starting at -1.</returns>
    private static GlyphCharacters MatchSpace(List<PageCharacter> characters, int from)
    {
        for (var i = from; i < characters.Count; i++)
        {
            var character = characters[i];
            if (character.Value == ' ' || (!character.Generated && char.IsWhiteSpace(character.Value)))
            {
                return new(i, 1);
            }

            if (!character.Generated)
            {
                break;
            }
        }

        return new(-1, 0);
    }

    /// <summary>Counts how many characters a ligature's text covers from its first character.</summary>
    /// <param name="text">The glyph's text.</param>
    /// <param name="characters">The page characters.</param>
    /// <param name="start">The first character.</param>
    /// <returns>The count, at least 1.</returns>
    private static int Extend(ReadOnlySpan<char> text, List<PageCharacter> characters, int start)
    {
        var count = 1;
        while (count < text.Length && start + count < characters.Count && !characters[start + count].Generated && characters[start + count].Value == text[count])
        {
            count++;
        }

        return count;
    }
}

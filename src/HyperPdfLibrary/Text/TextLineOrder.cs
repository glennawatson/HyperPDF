// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finishes a line: converts its characters from the order they sit on the page to logical order with the Unicode
/// Bidirectional Algorithm, then joins the line to the page, either with a line break, by joining a hyphenated word, or
/// directly when the text is written without spaces.
/// </summary>
internal static class TextLineOrder
{
    /// <summary>The paragraph level of a right-to-left line.</summary>
    private const int RightToLeftLevel = 1;

    /// <summary>The characters a hyphenated word needs: the hyphen and the letter before it.</summary>
    private const int MinCharsForHyphenWord = 2;

    /// <summary>The Unicode hyphen, which ends a hyphenated line like the hyphen-minus does.</summary>
    private const char UnicodeHyphen = (char)0x2010;

    /// <summary>The soft hyphen, which only ever marks a place to break a word.</summary>
    private const char SoftHyphen = (char)0x00AD;

    /// <summary>Puts the line being built in logical order and adds it to the page.</summary>
    /// <param name="state">The build state.</param>
    internal static void Complete(TextPageBuildState state)
    {
        if (state.Device.Options.StripFormattingControls)
        {
            TextFormattingControls.Strip(state.LineChars, state.Device.CancellationToken);
        }

        if (state.LineChars.Count == 0)
        {
            return;
        }

        var line = state.Device.Options.Order != PdfTextOrder.Raw && Reorder(state) ? state.LineLogical : state.LineChars;
        JoinToPage(state, line);
        state.Chars.AddRange(line);
        state.LineIndex++;
    }

    /// <summary>Reorders the line when right-to-left text makes the page order differ from the logical order.</summary>
    /// <param name="state">The build state.</param>
    /// <returns><see langword="true"/> when the logical order was written to <see cref="TextPageBuildState.LineLogical"/>.</returns>
    private static bool Reorder(TextPageBuildState state)
    {
        FindGroups(state);
        var types = CollectionsMarshal.AsSpan(state.GroupTypes);
        if (!TextBidi.NeedsReordering(types))
        {
            return false;
        }

        var baseLevel = state.RightToLeft || TextBidi.IsRightToLeftLine(types) ? RightToLeftLevel : 0;
        CollectionsMarshal.SetCount(state.GroupLevels, types.Length);
        CollectionsMarshal.SetCount(state.GroupOrder, types.Length);
        var levels = CollectionsMarshal.AsSpan(state.GroupLevels);
        TextBidi.ResolveLevels(types, baseLevel, levels);
        MirrorOddGroups(state, levels);
        TextBidi.Reorder(levels, CollectionsMarshal.AsSpan(state.GroupOrder));
        state.LineLogical.Clear();
        var starts = CollectionsMarshal.AsSpan(state.GroupStarts);
        foreach (var group in CollectionsMarshal.AsSpan(state.GroupOrder))
        {
            for (var i = starts[group]; i < starts[group + 1]; i++)
            {
                state.LineLogical.Add(state.LineChars[i] with { Level = levels[group] });
            }
        }

        return true;
    }

    /// <summary>Finds the glyph groups of the line and the bidirectional class of each.</summary>
    /// <param name="state">The build state.</param>
    private static void FindGroups(TextPageBuildState state)
    {
        state.GroupStarts.Clear();
        state.GroupTypes.Clear();
        var chars = CollectionsMarshal.AsSpan(state.LineChars);
        var group = -1;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i].Group == group)
            {
                continue;
            }

            group = chars[i].Group;
            state.GroupStarts.Add(i);
            state.GroupTypes.Add(chars[i].InText ? TextUnicode.GetClass(chars[i].Unicode) : TextBidiClass.ON);
        }

        state.GroupStarts.Add(chars.Length);
    }

    /// <summary>Replaces brackets and similar characters with their mirrored counterparts where they sit at an odd level.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="levels">The level of each group.</param>
    private static void MirrorOddGroups(TextPageBuildState state, ReadOnlySpan<byte> levels)
    {
        var chars = CollectionsMarshal.AsSpan(state.LineChars);
        var starts = CollectionsMarshal.AsSpan(state.GroupStarts);
        for (var group = 0; group < levels.Length; group++)
        {
            if ((levels[group] & 1) == 0)
            {
                continue;
            }

            for (var i = starts[group]; i < starts[group + 1]; i++)
            {
                chars[i] = chars[i] with { Unicode = TextUnicode.GetMirror(chars[i].Unicode) };
            }
        }
    }

    /// <summary>Adds what separates the line from the text before it.</summary>
    /// <param name="state">The build state.</param>
    /// <param name="line">The line's characters in logical order.</param>
    private static void JoinToPage(TextPageBuildState state, List<TextBuildChar> line)
    {
        if (state.Chars.Count == 0)
        {
            return;
        }

        var previous = state.Chars[^1];
        var next = FirstTextChar(line);
        if (state.Device.Options.HyphenMode == PdfTextHyphenMode.Join && EndsHyphenatedWord(state, next))
        {
            state.Chars[^1] = previous with { Kind = PdfTextCharKind.Hyphen, InText = false };
        }
        else if (!(TextUnicode.IsCjk(previous.Unicode) && TextUnicode.IsCjk(next)))
        {
            var point = new Vector2(previous.Box.Right, previous.Box.Bottom);
            var box = new PdfRectangle(point.X, point.Y, point.X, point.Y);
            state.Chars.Add(new(PdfTextCharKind.Generated, -1, '\r', point, box, box, previous.Run, line[0].Group, -1, true, 0));
            state.Chars.Add(new(PdfTextCharKind.Generated, -1, '\n', point, box, box, previous.Run, line[0].Group, -1, true, 0));
        }
    }

    /// <summary>Gets the first character of a line that is part of the text.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The character, or NUL when the line has none.</returns>
    private static char FirstTextChar(List<TextBuildChar> line)
    {
        foreach (var character in CollectionsMarshal.AsSpan(line))
        {
            if (character.InText)
            {
                return character.Unicode;
            }
        }

        return '\0';
    }

    /// <summary>
    /// Determines whether the text so far ends in a hyphen that splits a word across the line break. A soft hyphen always
    /// does. A hyphen-minus or Unicode hyphen does when a letter comes before it and a lower-case letter starts the next
    /// line, so "well-" then "known" joins and "A-" then "B" or a list dash does not.
    /// </summary>
    /// <param name="state">The build state, whose last character is tested.</param>
    /// <param name="next">The first text character of the next line.</param>
    /// <returns><see langword="true"/> for a split word.</returns>
    private static bool EndsHyphenatedWord(TextPageBuildState state, char next)
    {
        var last = state.Chars[^1];
        if (!last.InText || last.Kind == PdfTextCharKind.Generated)
        {
            return false;
        }

        if (last.Unicode == SoftHyphen)
        {
            return true;
        }

        if (last.Unicode is not ('-' or UnicodeHyphen) || !char.IsLower(next) || state.Chars.Count < MinCharsForHyphenWord)
        {
            return false;
        }

        var before = state.Chars[^MinCharsForHyphenWord];
        return before.InText && char.IsLetter(before.Unicode);
    }
}

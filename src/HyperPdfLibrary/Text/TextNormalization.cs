// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Decomposes characters and orders their combining marks (UAX #15, NFD and NFKD) from the generated tables, so a search
/// sees the same text for "é" written as one character and as "e" with a combining acute accent. It uses no operating
/// system or ICU data: .NET's own normalisation does nothing for non-ASCII text when invariant globalisation is on, and
/// the library must give the same answer on every platform. The Basic Multilingual Plane is covered. Hangul syllables
/// are left as they are, and composition is not needed because the text and the query are both left decomposed.
/// </summary>
internal static class TextNormalization
{
    /// <summary>The first character that can have a canonical decomposition: U+00C0, the first accented capital.</summary>
    private const char FirstCanonical = (char)0x00C0;

    /// <summary>The first character that can have a compatibility decomposition: U+00A0, the no-break space.</summary>
    private const char FirstCompatibility = (char)0x00A0;

    /// <summary>The first character that has a combining class: U+0300, the combining grave accent.</summary>
    private const char FirstCombining = (char)0x0300;

    /// <summary>Gets the most characters one character decomposes into.</summary>
    /// <param name="compatibility">Whether compatibility decompositions apply.</param>
    /// <returns>The most characters.</returns>
    internal static int LongestDecomposition(bool compatibility) => compatibility ? TextUnicodeData.LongestCompatibility : TextUnicodeData.LongestCanonical;

    /// <summary>Writes the full decomposition of a character.</summary>
    /// <param name="value">The character.</param>
    /// <param name="compatibility">Whether compatibility decompositions apply as well as canonical ones.</param>
    /// <param name="destination">The buffer, at least <see cref="LongestDecomposition"/> characters.</param>
    /// <returns>The characters written; 0 when the character does not decompose.</returns>
    internal static int Decompose(char value, bool compatibility, Span<char> destination)
    {
        if (value < (compatibility ? FirstCompatibility : FirstCanonical))
        {
            return 0;
        }

        if (compatibility)
        {
            var written = Lookup(TextUnicodeData.CompatibilityKeys, TextUnicodeData.CompatibilityOffsets, TextUnicodeData.CompatibilityValues, value, destination);
            if (written > 0)
            {
                return written;
            }
        }

        return Lookup(TextUnicodeData.CanonicalKeys, TextUnicodeData.CanonicalOffsets, TextUnicodeData.CanonicalValues, value, destination);
    }

    /// <summary>Counts the characters a text has once every character is decomposed.</summary>
    /// <param name="text">The text.</param>
    /// <param name="compatibility">Whether compatibility decompositions apply as well as canonical ones.</param>
    /// <returns>The length of the decomposed text.</returns>
    internal static int DecomposedLength(ReadOnlySpan<char> text, bool compatibility)
    {
        var limit = compatibility ? FirstCompatibility : FirstCanonical;
        var first = IndexOfAtOrAbove(text, limit);
        if (first < 0)
        {
            return text.Length;
        }

        Span<char> parts = stackalloc char[TextUnicodeData.LongestCompatibility];
        var length = first;
        for (var i = first; i < text.Length; i++)
        {
            var written = Decompose(text[i], compatibility, parts);
            length += written == 0 ? 1 : written;
        }

        return length;
    }

    /// <summary>Gets a character's canonical combining class.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The class; 0 for a character that is not a combining mark that reorders.</returns>
    internal static byte CombiningClass(char value)
    {
        if (value < FirstCombining)
        {
            return 0;
        }

        var index = TextUnicodeData.CombiningClassStarts.BinarySearch((ushort)value);
        if (index < 0)
        {
            index = ~index - 1;
        }

        return TextUnicodeData.CombiningClasses[index];
    }

    /// <summary>Puts each run of combining marks in canonical order: ascending combining class, keeping the order of equal classes.</summary>
    /// <param name="chars">The decomposed characters, changed in place.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Reorder(Span<char> chars) => Reorder(chars, default);

    /// <summary>Puts each run of combining marks in canonical order, moving the entries of a parallel map with them.</summary>
    /// <param name="chars">The decomposed characters, changed in place.</param>
    /// <param name="map">The source index of each character, or empty when there is no map.</param>
    internal static void Reorder(Span<char> chars, Span<int> map)
    {
        if (IndexOfAtOrAbove(chars, FirstCombining) < 0)
        {
            return;
        }

        var i = 0;
        while (i < chars.Length)
        {
            if (CombiningClass(chars[i]) == 0)
            {
                i++;
                continue;
            }

            var end = i + 1;
            while (end < chars.Length && CombiningClass(chars[end]) != 0)
            {
                end++;
            }

            SortRun(chars[i..end], map.IsEmpty ? default : map[i..end]);
            i = end;
        }
    }

    /// <summary>Finds the first character at or above a limit, so a text with none of the characters that decompose or combine is skipped in one pass.</summary>
    /// <param name="chars">The characters.</param>
    /// <param name="limit">The lowest character that counts.</param>
    /// <returns>The index of the first such character, or -1.</returns>
    private static int IndexOfAtOrAbove(ReadOnlySpan<char> chars, char limit)
    {
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] >= limit)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Sorts one run of combining marks by combining class with an insertion sort, which is stable and the runs are short.</summary>
    /// <param name="chars">The run.</param>
    /// <param name="map">The run's source indexes, or empty.</param>
    private static void SortRun(Span<char> chars, Span<int> map)
    {
        for (var i = 1; i < chars.Length; i++)
        {
            var value = chars[i];
            var source = map.IsEmpty ? 0 : map[i];
            var rank = CombiningClass(value);
            var j = i - 1;
            while (j >= 0 && CombiningClass(chars[j]) > rank)
            {
                chars[j + 1] = chars[j];
                if (!map.IsEmpty)
                {
                    map[j + 1] = map[j];
                }

                j--;
            }

            chars[j + 1] = value;
            if (!map.IsEmpty)
            {
                map[j + 1] = source;
            }
        }
    }

    /// <summary>Finds a character in a decomposition table and writes its decomposition.</summary>
    /// <param name="keys">The decomposable characters, in order.</param>
    /// <param name="offsets">Where each decomposition starts in <paramref name="values"/>, with a final end offset.</param>
    /// <param name="values">The decomposed characters.</param>
    /// <param name="value">The character.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The characters written; 0 when the character is not in the table.</returns>
    private static int Lookup(ReadOnlySpan<ushort> keys, ReadOnlySpan<ushort> offsets, ReadOnlySpan<ushort> values, char value, Span<char> destination)
    {
        var index = keys.BinarySearch((ushort)value);
        if (index < 0)
        {
            return 0;
        }

        var parts = values[offsets[index]..offsets[index + 1]];
        for (var i = 0; i < parts.Length; i++)
        {
            destination[i] = (char)parts[i];
        }

        return parts.Length;
    }
}

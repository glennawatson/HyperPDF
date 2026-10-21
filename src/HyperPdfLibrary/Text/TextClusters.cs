// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Numerics;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds the clusters of a text: runs of characters a reader sees as one unit and a selection must not split, such as a
/// letter with its accents, an Indic consonant with its vowel signs, or a conjunct of consonants joined by a virama. The
/// rules are the extended grapheme clusters of UAX #29 that .NET carries in its own data, so they need no operating system
/// or ICU support and give the same answer on every platform, plus the rule for conjuncts that .NET's data does not yet
/// apply: in Devanagari, Bengali, Gujarati, Oriya, Telugu and Malayalam a consonant that follows a virama joins the cluster
/// of the consonant before it (UAX #29 rule GB9c). A text's cluster starts are kept as a set of bits.
/// </summary>
internal static class TextClusters
{
    /// <summary>The bits in one word of the set.</summary>
    private const int WordBits = 32;

    /// <summary>The shift that turns a bit index into a word index.</summary>
    private const int WordShift = 5;

    /// <summary>The mask that keeps the bit of a bit index inside its word.</summary>
    private const int BitMask = WordBits - 1;

    /// <summary>The shift that turns a character into the index of its 128-character block.</summary>
    private const int BlockShift = 7;

    /// <summary>
    /// The set of 128-character blocks of the scripts whose consonants form conjuncts, one bit per block: Devanagari
    /// (U+0900), Bengali (U+0980), Gujarati (U+0A80), Oriya (U+0B00), Telugu (U+0C00) and Malayalam (U+0D00).
    /// </summary>
    private const uint ConjunctBlocks = (1U << 0x12) | (1U << 0x13) | (1U << 0x15) | (1U << 0x16) | (1U << 0x18) | (1U << 0x1A);

    /// <summary>The Devanagari virama.</summary>
    private const char DevanagariVirama = (char)0x094D;

    /// <summary>The Bengali virama.</summary>
    private const char BengaliVirama = (char)0x09CD;

    /// <summary>The Gujarati virama.</summary>
    private const char GujaratiVirama = (char)0x0ACD;

    /// <summary>The Oriya virama.</summary>
    private const char OriyaVirama = (char)0x0B4D;

    /// <summary>The Telugu virama.</summary>
    private const char TeluguVirama = (char)0x0C4D;

    /// <summary>The Malayalam virama.</summary>
    private const char MalayalamVirama = (char)0x0D4D;

    /// <summary>The characters that are always a cluster of their own: tab, line feed, printable ASCII and Latin-1 from U+00A0; the set never changes after it is built.</summary>
    private static readonly SearchValues<char> PlainCharacters = SearchValues.Create(ListPlainCharacters());

    /// <summary>
    /// Determines whether every character of a text is a cluster of its own. Only printable ASCII, tab, line feed and the
    /// Latin-1 letters and symbols qualify: none of them extends, joins or prepends, so a text of them needs no segmentation.
    /// Carriage return is left out because it joins a following line feed. Anything else counts as not plain.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when every character is a cluster of its own.</returns>
    internal static bool IsPlain(ReadOnlySpan<char> text) => !text.ContainsAnyExcept(PlainCharacters);

    /// <summary>Marks the start of every cluster of a text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bits, one for each character of the text, set where a cluster starts.</returns>
    internal static uint[] FindStarts(string text)
    {
        var bits = new uint[(text.Length + BitMask) >> WordShift];
        var index = 0;
        while (index < text.Length)
        {
            bits[index >> WordShift] |= 1U << (index & BitMask);
            index += ClusterLength(text, index);
        }

        return bits;
    }

    /// <summary>Finds the start of the cluster that holds a character.</summary>
    /// <param name="starts">The cluster starts of the text.</param>
    /// <param name="index">The index of a character of the text.</param>
    /// <returns>The index of the cluster's first character.</returns>
    internal static int StartOf(ReadOnlySpan<uint> starts, int index)
    {
        var word = index >> WordShift;
        var bits = starts[word] & (uint.MaxValue >> (BitMask - (index & BitMask)));
        while (bits == 0 && word > 0)
        {
            word--;
            bits = starts[word];
        }

        return bits == 0 ? 0 : (word << WordShift) + BitOperations.Log2(bits);
    }

    /// <summary>Finds the end of the cluster that holds a character.</summary>
    /// <param name="starts">The cluster starts of the text.</param>
    /// <param name="length">The length of the text.</param>
    /// <param name="index">The index of a character of the text.</param>
    /// <returns>The index after the cluster's last character.</returns>
    internal static int EndOf(ReadOnlySpan<uint> starts, int length, int index)
    {
        var next = index + 1;
        if (next >= length)
        {
            return length;
        }

        var word = next >> WordShift;
        var bits = starts[word] & (uint.MaxValue << (next & BitMask));
        while (bits == 0 && word + 1 < starts.Length)
        {
            word++;
            bits = starts[word];
        }

        return bits == 0 ? length : (word << WordShift) + BitOperations.TrailingZeroCount(bits);
    }

    /// <summary>Determines whether a position in a text is inside a conjunct: a consonant that a virama joins to the consonant before it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The position of a character.</param>
    /// <returns><see langword="true"/> when the character is a consonant right after a virama.</returns>
    internal static bool IsInsideConjunct(ReadOnlySpan<char> text, int index) =>
        index > 0 && IsLinker(text[index - 1]) && IsConjunctConsonant(text[index]);

    /// <summary>Lists tab, line feed, printable ASCII (U+0020 to U+007E) and Latin-1 from U+00A0 to U+00FF.</summary>
    /// <returns>The characters as a string.</returns>
    private static string ListPlainCharacters()
    {
        const char tab = '\t';
        const char lineFeed = '\n';
        const char firstAscii = ' ';
        const char lastAscii = '~';
        const char firstLatin = ' ';
        const char lastLatin = 'ÿ';
        var builder = new System.Text.StringBuilder();
        _ = builder.Append(tab).Append(lineFeed);
        for (var value = firstAscii; value <= lastAscii; value++)
        {
            _ = builder.Append(value);
        }

        for (var value = firstLatin; value <= lastLatin; value++)
        {
            _ = builder.Append(value);
        }

        return builder.ToString();
    }

    /// <summary>Gets the length of the cluster that starts at an index.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index of the cluster's first character.</param>
    /// <returns>The number of characters in the cluster, at least 1.</returns>
    private static int ClusterLength(string text, int start)
    {
        var length = Math.Max(1, StringInfo.GetNextTextElementLength(text.AsSpan(start)));
        while (start + length < text.Length && IsInsideConjunct(text, start + length))
        {
            length += Math.Max(1, StringInfo.GetNextTextElementLength(text.AsSpan(start + length)));
        }

        return length;
    }

    /// <summary>Determines whether a character is the virama of a script whose consonants form conjuncts: the Indic Conjunct Break linkers.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those viramas.</returns>
    private static bool IsLinker(char value) =>
        value is DevanagariVirama or BengaliVirama or GujaratiVirama or OriyaVirama or TeluguVirama or MalayalamVirama;

    /// <summary>Determines whether a character is a letter of a script whose consonants form conjuncts.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for a letter in the Devanagari, Bengali, Gujarati, Oriya, Telugu or Malayalam blocks.</returns>
    private static bool IsConjunctConsonant(char value)
    {
        var block = value >> BlockShift;
        return char.IsLetter(value) && block < WordBits && ((ConjunctBlocks >> block) & 1) != 0;
    }
}

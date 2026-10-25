// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Folds characters so a search can treat variants as equal. Text and query are first decomposed (canonical
/// decomposition, or compatibility decomposition when asked) with their combining marks put in canonical order, so a
/// letter written as one character and as a base letter with a mark are the same text. Each folded character then comes
/// from one source character, or is dropped (combining marks when accents are ignored), so a match in folded text maps
/// back to the page text through an index table; the characters one source character decomposes into share its index.
/// Order: decomposition, diacritics, compatibility variants, then case.
/// </summary>
internal static class TextFolding
{
    /// <summary>The options that change how text is folded.</summary>
    internal const PdfTextSearchOptions FoldOptions = PdfTextSearchOptions.MatchCase | PdfTextSearchOptions.IgnoreDiacritics | PdfTextSearchOptions.FoldCompatibility;

    /// <summary>The offset between a fullwidth ASCII variant (U+FF01 to U+FF5E) and its ASCII character.</summary>
    private const int FullwidthOffset = 0xFEE0;

    /// <summary>The highest ASCII character.</summary>
    private const int HighestAscii = 0x7F;

    /// <summary>The first fullwidth ASCII variant.</summary>
    private const char FullwidthFirst = (char)0xFF01;

    /// <summary>The last fullwidth ASCII variant.</summary>
    private const char FullwidthLast = (char)0xFF5E;

    /// <summary>Gets the case-folded and fold-option-folded text, with a map from folded index to source index.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="options">The search options; only the folding options matter.</param>
    /// <returns>The folded text.</returns>
    internal static FoldedText Fold(string text, PdfTextSearchOptions options)
    {
        var compatibility = (options & PdfTextSearchOptions.FoldCompatibility) != 0;
        var capacity = TextNormalization.DecomposedLength(text, compatibility);
        var buffer = ArrayPool<char>.Shared.Rent(capacity);
        var writer = new FoldWriter(buffer, capacity);
        try
        {
            FoldInto(text, options, ref writer);

            // Text that folds to itself keeps its own string instead of a copy.
            var result = buffer.AsSpan(0, writer.Count);
            var foldedText = result.SequenceEqual(text) ? text : new string(result);
            return new(foldedText, writer.CopyMap(), text.Length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
            writer.Release();
        }
    }

    /// <summary>Folds a query into a buffer.</summary>
    /// <param name="source">The query.</param>
    /// <param name="destination">The buffer, at least <see cref="TextNormalization.DecomposedLength"/> characters.</param>
    /// <param name="options">The search options.</param>
    /// <returns>The folded length.</returns>
    internal static int Fold(ReadOnlySpan<char> source, Span<char> destination, PdfTextSearchOptions options)
    {
        var compatibility = (options & PdfTextSearchOptions.FoldCompatibility) != 0;
        Span<char> parts = stackalloc char[TextUnicodeData.LongestCompatibility];
        var count = 0;
        foreach (var value in source)
        {
            var written = TextNormalization.Decompose(value, compatibility, parts);
            if (written == 0)
            {
                count = Append(value, options, destination, count);
                continue;
            }

            for (var part = 0; part < written; part++)
            {
                count = Append(parts[part], options, destination, count);
            }
        }

        TextNormalization.Reorder(destination[..count]);
        return count;
    }

    /// <summary>
    /// Folds page text into pooled buffers, so a search can scan a page without keeping a string or a map for it. The caller
    /// disposes the result to return the buffers.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <param name="options">The search options; only the folding options matter.</param>
    /// <returns>The folded text in pooled buffers.</returns>
    internal static PooledFold FoldPooled(ReadOnlySpan<char> text, PdfTextSearchOptions options)
    {
        var compatibility = (options & PdfTextSearchOptions.FoldCompatibility) != 0;
        var capacity = TextNormalization.DecomposedLength(text, compatibility);
        var buffer = ArrayPool<char>.Shared.Rent(capacity);
        var writer = new FoldWriter(buffer, capacity);
        try
        {
            FoldInto(text, options, ref writer);
            return new(buffer, writer.Count, writer.TakeMap(), text.Length);
        }
        catch
        {
            ArrayPool<char>.Shared.Return(buffer);
            throw;
        }
        finally
        {
            writer.Release();
        }
    }

    /// <summary>Folds one character.</summary>
    /// <param name="value">The character.</param>
    /// <param name="options">The search options.</param>
    /// <param name="folded">The folded character.</param>
    /// <returns><see langword="false"/> when the character is dropped.</returns>
    internal static bool TryFold(char value, PdfTextSearchOptions options, out char folded)
    {
        folded = value;
        if ((options & PdfTextSearchOptions.IgnoreDiacritics) != 0 && TextUnicode.IsCombiningMark(value))
        {
            return false;
        }

        if ((options & PdfTextSearchOptions.FoldCompatibility) != 0)
        {
            folded = FoldCompatibilityVariant(folded);
        }

        if ((options & PdfTextSearchOptions.MatchCase) == 0)
        {
            folded = char.ToLowerInvariant(folded);
        }

        return true;
    }

    /// <summary>Decomposes, folds and reorders every character of a text into a writer.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="options">The search options.</param>
    /// <param name="writer">Receives the folded characters and their source indexes.</param>
    private static void FoldInto(ReadOnlySpan<char> text, PdfTextSearchOptions options, ref FoldWriter writer)
    {
        // ASCII has no decomposition, no combining mark and no variant to fold, so plain ASCII text is only lower-cased, in one pass.
        if (Ascii.IsValid(text))
        {
            writer.AddAscii(text, options);
            return;
        }

        var compatibility = (options & PdfTextSearchOptions.FoldCompatibility) != 0;
        Span<char> parts = stackalloc char[TextUnicodeData.LongestCompatibility];
        var position = 0;
        while (position < text.Length)
        {
            // Pages are mostly ASCII with a few special characters, so each run of ASCII is folded in one call.
            var nonAscii = text[position..].IndexOfAnyExceptInRange((char)0, (char)HighestAscii);
            var asciiLength = nonAscii < 0 ? text.Length - position : nonAscii;
            if (asciiLength > 0)
            {
                writer.AddAsciiRun(text.Slice(position, asciiLength), options, position);
                position += asciiLength;
                continue;
            }

            var written = TextNormalization.Decompose(text[position], compatibility, parts);
            if (written == 0)
            {
                writer.Add(text[position], options, position);
            }

            for (var part = 0; part < written; part++)
            {
                writer.Add(parts[part], options, position);
            }

            position++;
        }

        writer.Reorder();
    }

    /// <summary>Folds a character into a buffer, unless it is dropped.</summary>
    /// <param name="value">The character.</param>
    /// <param name="options">The search options.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="count">The characters in the buffer.</param>
    /// <returns>The characters in the buffer afterwards.</returns>
    private static int Append(char value, PdfTextSearchOptions options, Span<char> destination, int count)
    {
        if (!TryFold(value, options, out var folded))
        {
            return count;
        }

        destination[count] = folded;
        return count + 1;
    }

    /// <summary>Maps typographic and fullwidth variants to the plain character.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The plain character, or the character itself.</returns>
    private static char FoldCompatibilityVariant(char value) => value switch
    {
        >= FullwidthFirst and <= FullwidthLast => (char)(value - FullwidthOffset),
        _ when IsSpaceVariant(value) => ' ',
        _ when IsSingleQuoteVariant(value) => '\'',
        >= '“' and <= '‟' => '"',
        _ when IsDashVariant(value) => '-',
        _ => value,
    };

    /// <summary>Determines whether a character is a no-break, typographic or ideographic space.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those spaces.</returns>
    private static bool IsSpaceVariant(char value) => value is ' ' or (>= ' ' and <= ' ') or ' ' or ' ' or '　';

    /// <summary>Determines whether a character is a curly or low single quote.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those quotes.</returns>
    private static bool IsSingleQuoteVariant(char value) => value is >= '‘' and <= '‛';

    /// <summary>Determines whether a character is a hyphen, en dash, em dash, bar or minus.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those dashes.</returns>
    private static bool IsDashVariant(char value) => value is (>= '‐' and <= '―') or '−' or '﹘' or '﹣' or '－';

    /// <summary>
    /// Collects the folded characters of a text and the source index of each. The map is only made once a source character
    /// does not give exactly one folded character, so text that folds one for one has none.
    /// </summary>
    private ref struct FoldWriter
    {
        /// <summary>The buffer the folded characters go into.</summary>
        private readonly Span<char> _chars;

        /// <summary>The length the map needs.</summary>
        private readonly int _capacity;

        /// <summary>The source index of each folded character, rented when first needed.</summary>
        private int[]? _map;

        /// <summary>Initializes a new instance of the <see cref="FoldWriter"/> struct.</summary>
        /// <param name="chars">The buffer.</param>
        /// <param name="capacity">The most folded characters that will be written.</param>
        internal FoldWriter(Span<char> chars, int capacity)
        {
            _chars = chars;
            _capacity = capacity;
            Count = 0;
        }

        /// <summary>Gets the number of folded characters written.</summary>
        internal int Count { get; private set; }

        /// <summary>Folds a character and writes it, unless it is dropped.</summary>
        /// <param name="value">The character.</param>
        /// <param name="options">The search options.</param>
        /// <param name="source">The index of the source character in the text.</param>
        internal void Add(char value, PdfTextSearchOptions options, int source)
        {
            if (!TryFold(value, options, out var folded))
            {
                return;
            }

            if (_map is null && source != Count)
            {
                _map = IdentityMap(_capacity, Count);
            }

            if (_map is not null)
            {
                _map[Count] = source;
            }

            _chars[Count] = folded;
            Count++;
        }

        /// <summary>
        /// Writes a whole text of ASCII characters, each of which folds to one character: itself, or its lower case unless case
        /// matters. Nothing is dropped, so the text needs no map.
        /// </summary>
        /// <param name="text">The ASCII text.</param>
        /// <param name="options">The search options.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddAscii(ReadOnlySpan<char> text, PdfTextSearchOptions options) => AddAsciiRun(text, options, 0);

        /// <summary>Appends a run of ASCII characters that starts at a given index of the source text; each folds to one character.</summary>
        /// <param name="run">The ASCII characters.</param>
        /// <param name="options">The search options.</param>
        /// <param name="source">The index of the run's first character in the text.</param>
        internal void AddAsciiRun(ReadOnlySpan<char> run, PdfTextSearchOptions options, int source)
        {
            var destination = _chars.Slice(Count, run.Length);
            if ((options & PdfTextSearchOptions.MatchCase) != 0)
            {
                run.CopyTo(destination);
            }
            else
            {
                _ = Ascii.ToLower(run, destination, out _);
            }

            if (_map is null && source != Count)
            {
                _map = IdentityMap(_capacity, Count);
            }

            if (_map is not null)
            {
                for (var i = 0; i < run.Length; i++)
                {
                    _map[Count + i] = source + i;
                }
            }

            Count += run.Length;
        }

        /// <summary>Puts the combining marks of the written characters in canonical order.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void Reorder() => TextNormalization.Reorder(_chars[..Count], _map is null ? default : _map.AsSpan(0, Count));

        /// <summary>Copies the map to an array of its own.</summary>
        /// <returns>The map, or <see langword="null"/> when folded characters map to the same indexes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly int[]? CopyMap() => _map?.AsSpan(0, Count).ToArray();

        /// <summary>Hands the rented map to the caller, who returns it; <see cref="Release"/> then returns nothing.</summary>
        /// <returns>The rented map, or <see langword="null"/> when folded characters map to the same indexes.</returns>
        internal int[]? TakeMap()
        {
            var map = _map;
            _map = null;
            return map;
        }

        /// <summary>Returns the rented map.</summary>
        internal readonly void Release()
        {
            if (_map is not null)
            {
                ArrayPool<int>.Shared.Return(_map);
            }
        }

        /// <summary>Makes a map where the first entries point at themselves.</summary>
        /// <param name="length">The map length.</param>
        /// <param name="filled">The number of leading entries to fill.</param>
        /// <returns>The map.</returns>
        private static int[] IdentityMap(int length, int filled)
        {
            var map = ArrayPool<int>.Shared.Rent(length);
            for (var i = 0; i < filled; i++)
            {
                map[i] = i;
            }

            return map;
        }
    }
}

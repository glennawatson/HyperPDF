// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Damages PDF files in repeatable ways: byte flips, truncation, duplicated chunks, a missing xref, bad /Length and bad filters.</summary>
internal static class PdfMutator
{
    /// <summary>The most bytes one flip mutation changes.</summary>
    private const int MaxFlips = 16;

    /// <summary>The most bytes a truncation removes when it cuts near the end.</summary>
    private const int MaxTailCut = 2048;

    /// <summary>The longest chunk that is duplicated.</summary>
    private const int MaxChunk = 512;

    /// <summary>The most mutations applied to one file.</summary>
    private const int MaxMutations = 3;

    /// <summary>The number of mutation kinds.</summary>
    private const int KindCount = 6;

    /// <summary>The kind that flips bytes.</summary>
    private const int FlipKind = 0;

    /// <summary>The kind that truncates the file.</summary>
    private const int TruncateKind = 1;

    /// <summary>The kind that duplicates a chunk.</summary>
    private const int DuplicateKind = 2;

    /// <summary>The kind that damages the cross-reference table.</summary>
    private const int XrefKind = 3;

    /// <summary>The kind that damages a /Length.</summary>
    private const int LengthKind = 4;

    /// <summary>The number of choices in a coin flip.</summary>
    private const int Coin = 2;

    /// <summary>The number of replacement values a /Length can take.</summary>
    private const int LengthChoices = 4;

    /// <summary>The number of replacement values a filter can take.</summary>
    private const int FilterChoices = 5;

    /// <summary>The first replacement /Length choice.</summary>
    private const int LengthZero = 0;

    /// <summary>The second replacement /Length choice.</summary>
    private const int LengthHuge = 1;

    /// <summary>The third replacement /Length choice.</summary>
    private const int LengthNegative = 2;

    /// <summary>The first replacement filter choice.</summary>
    private const int FilterLzw = 0;

    /// <summary>The second replacement filter choice.</summary>
    private const int FilterAscii85 = 1;

    /// <summary>The third replacement filter choice.</summary>
    private const int FilterUnknown = 2;

    /// <summary>The fourth replacement filter choice.</summary>
    private const int FilterChain = 3;

    /// <summary>Gets the keyword that starts the cross-reference table.</summary>
    private static ReadOnlySpan<byte> XrefKeyword => "xref"u8;

    /// <summary>Gets the keyword before the offset of the cross-reference table.</summary>
    private static ReadOnlySpan<byte> StartXrefKeyword => "startxref"u8;

    /// <summary>Gets the key that holds a stream's length.</summary>
    private static ReadOnlySpan<byte> LengthKey => "/Length "u8;

    /// <summary>Gets the key that names a stream's filters.</summary>
    private static ReadOnlySpan<byte> FilterKey => "/Filter "u8;

    /// <summary>Gets the bytes that end a name value.</summary>
    private static ReadOnlySpan<byte> NameEnds => "/>\n\r "u8;

    /// <summary>Applies one to three mutations.</summary>
    /// <param name="source">The file; not changed.</param>
    /// <param name="random">The seeded random source.</param>
    /// <param name="log">Receives the kinds applied, for failure messages.</param>
    /// <returns>The damaged file.</returns>
    internal static byte[] Mutate(byte[] source, SeededRandom random, StringBuilder log)
    {
        var current = source;
        var count = random.Next(1, MaxMutations + 1);
        for (var i = 0; i < count; i++)
        {
            var kind = random.Next(KindCount);
            _ = log.Append(kind).Append(' ');
            current = Apply(kind, current, random);
        }

        return current;
    }

    /// <summary>Applies one kind of mutation.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] Apply(int kind, byte[] data, SeededRandom random) => kind switch
    {
        FlipKind => Flip(data, random),
        TruncateKind => Truncate(data, random),
        DuplicateKind => DuplicateChunk(data, random),
        XrefKind => BreakXref(data, random),
        LengthKind => CorruptLength(data, random),
        _ => CorruptFilter(data, random),
    };

    /// <summary>Overwrites random bytes.</summary>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] Flip(byte[] data, SeededRandom random)
    {
        var copy = (byte[])data.Clone();
        var flips = random.Next(1, MaxFlips + 1);
        for (var i = 0; i < flips && copy.Length > 0; i++)
        {
            copy[random.Next(copy.Length)] = (byte)random.Next(byte.MaxValue + 1);
        }

        return copy;
    }

    /// <summary>Cuts the file short, often near the end where the cross-reference table sits.</summary>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] Truncate(byte[] data, SeededRandom random)
    {
        if (data.Length < Coin)
        {
            return data;
        }

        var length = random.Next(Coin) == 0
            ? random.Next(1, data.Length)
            : Math.Max(1, data.Length - random.Next(1, Math.Min(data.Length, MaxTailCut) + 1));
        return data.AsSpan(0, length).ToArray();
    }

    /// <summary>Inserts a copy of a random chunk at a random place.</summary>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] DuplicateChunk(byte[] data, SeededRandom random)
    {
        if (data.Length == 0)
        {
            return data;
        }

        var start = random.Next(data.Length);
        var length = random.Next(1, Math.Min(MaxChunk, data.Length - start) + 1);
        return Replace(data, random.Next(data.Length + 1), 0, data.AsSpan(start, length));
    }

    /// <summary>Spoils the cross-reference table: the keyword, or the offset that startxref gives.</summary>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] BreakXref(byte[] data, SeededRandom random)
    {
        var copy = (byte[])data.Clone();
        if (random.Next(Coin) == 0)
        {
            RenameKeyword(copy);
        }
        else
        {
            ScrambleStartXref(copy);
        }

        return copy;
    }

    /// <summary>Changes the first letter of every "xref" keyword, which also spoils "startxref" and "/XRef" streams' names in part.</summary>
    /// <param name="copy">The file, changed in place.</param>
    private static void RenameKeyword(byte[] copy)
    {
        // Each change removes one match, so the search ends.
        for (var found = copy.AsSpan().IndexOf(XrefKeyword); found >= 0; found = copy.AsSpan().IndexOf(XrefKeyword))
        {
            copy[found] = (byte)'z';
        }
    }

    /// <summary>Replaces the digits after the last "startxref" with nines.</summary>
    /// <param name="copy">The file, changed in place.</param>
    private static void ScrambleStartXref(byte[] copy)
    {
        var start = copy.AsSpan().LastIndexOf(StartXrefKeyword);
        if (start < 0)
        {
            return;
        }

        for (var i = start + StartXrefKeyword.Length; i < copy.Length; i++)
        {
            if (copy[i] is >= (byte)'0' and <= (byte)'9')
            {
                copy[i] = (byte)'9';
            }
        }
    }

    /// <summary>Replaces the number after a random /Length with a wrong one.</summary>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] CorruptLength(byte[] data, SeededRandom random)
    {
        var at = RandomIndexOf(data, LengthKey, random);
        if (at < 0)
        {
            return data;
        }

        var start = at + LengthKey.Length;
        var end = start;
        while (end < data.Length && data[end] is (>= (byte)'0' and <= (byte)'9') or (byte)'-')
        {
            end++;
        }

        var replacement = random.Next(LengthChoices) switch
        {
            LengthZero => "0"u8,
            LengthHuge => "999999999"u8,
            LengthNegative => "-5"u8,
            _ => "7"u8,
        };
        return Replace(data, start, end - start, replacement);
    }

    /// <summary>Replaces a random /Filter value with another filter, an unknown one, or a long chain.</summary>
    /// <param name="data">The file.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The damaged file.</returns>
    private static byte[] CorruptFilter(byte[] data, SeededRandom random)
    {
        var at = RandomIndexOf(data, FilterKey, random);
        if (at < 0)
        {
            return data;
        }

        var start = at + FilterKey.Length;
        var end = FilterValueEnd(data, start);
        var replacement = random.Next(FilterChoices) switch
        {
            FilterLzw => "/LZWDecode "u8,
            FilterAscii85 => "/ASCII85Decode "u8,
            FilterUnknown => "/NoSuchFilter "u8,
            FilterChain => "[/FlateDecode /FlateDecode /FlateDecode /FlateDecode /FlateDecode /FlateDecode /FlateDecode /FlateDecode /FlateDecode /FlateDecode] "u8,
            _ => "/RunLengthDecode "u8,
        };
        return Replace(data, start, end - start, replacement);
    }

    /// <summary>Finds the end of a /Filter value: a name, or an array up to its closing bracket.</summary>
    /// <param name="data">The file.</param>
    /// <param name="start">The offset of the value.</param>
    /// <returns>The offset after the value.</returns>
    private static int FilterValueEnd(byte[] data, int start)
    {
        if (start >= data.Length)
        {
            return data.Length;
        }

        var isArray = data[start] == (byte)'[';
        var stop = isArray ? data.AsSpan(start).IndexOf((byte)']') : data.AsSpan(start + 1).IndexOfAny(NameEnds);
        if (stop < 0)
        {
            return data.Length;
        }

        return isArray ? start + stop + 1 : start + 1 + stop;
    }

    /// <summary>Finds a random occurrence of a pattern.</summary>
    /// <param name="data">The file.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="random">The random source.</param>
    /// <returns>The offset, or -1 when the pattern is absent.</returns>
    private static int RandomIndexOf(byte[] data, ReadOnlySpan<byte> pattern, SeededRandom random)
    {
        var count = 0;
        for (var offset = 0; offset < data.Length;)
        {
            var found = data.AsSpan(offset).IndexOf(pattern);
            if (found < 0)
            {
                break;
            }

            count++;
            offset += found + pattern.Length;
        }

        if (count == 0)
        {
            return -1;
        }

        var wanted = random.Next(count);
        var at = 0;
        for (var i = 0; i < wanted; i++)
        {
            at += data.AsSpan(at).IndexOf(pattern) + pattern.Length;
        }

        return at + data.AsSpan(at).IndexOf(pattern);
    }

    /// <summary>Replaces part of a file.</summary>
    /// <param name="data">The file.</param>
    /// <param name="start">The offset to replace from.</param>
    /// <param name="removed">The number of bytes to remove.</param>
    /// <param name="inserted">The bytes to put in their place.</param>
    /// <returns>A new file.</returns>
    private static byte[] Replace(byte[] data, int start, int removed, ReadOnlySpan<byte> inserted)
    {
        var result = new byte[data.Length - removed + inserted.Length];
        data.AsSpan(0, start).CopyTo(result);
        inserted.CopyTo(result.AsSpan(start));
        data.AsSpan(start + removed).CopyTo(result.AsSpan(start + inserted.Length));
        return result;
    }
}

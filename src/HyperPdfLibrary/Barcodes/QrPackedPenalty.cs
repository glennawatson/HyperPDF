// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Barcodes;

/// <summary>
/// Scores a QR Code of up to 128 modules across with each row packed into one 128-bit word, so a whole row or column of
/// runs, finder look-alikes and blocks is tested with a few shifts and masks. It gives the same score as <see cref="QrPenalty"/>.
/// </summary>
internal static class QrPackedPenalty
{
    /// <summary>The widest symbol that fits one word per row; version 27 is 125 modules across.</summary>
    internal const int MaximumSize = 128;

    /// <summary>The shortest run of one colour that costs points, and the length of the window that finds it.</summary>
    private const int RunWindow = 5;

    /// <summary>The extra cost of the first window of a run.</summary>
    private const int RunStartExtra = 2;

    /// <summary>The cost of a two by two block of one colour.</summary>
    private const int BlockCost = 3;

    /// <summary>The cost of a look-alike of the finder pattern.</summary>
    private const int FinderCost = 40;

    /// <summary>The length of the finder look-alike.</summary>
    private const int FinderLength = 11;

    /// <summary>The finder look-alike with light modules after it, the first module as the highest bit.</summary>
    private const int FinderAfter = 0b10111010000;

    /// <summary>The finder look-alike with light modules before it.</summary>
    private const int FinderBefore = 0b00001011101;

    /// <summary>The cost of each step the dark share is away from half.</summary>
    private const int BalanceCost = 10;

    /// <summary>Twenty, to scale the dark count against the total.</summary>
    private const int Twenty = 20;

    /// <summary>Ten, to scale the total.</summary>
    private const int Ten = 10;

    /// <summary>Works out the penalty, giving up once it is more than a limit.</summary>
    /// <param name="modules">The modules row by row; true is dark.</param>
    /// <param name="size">The modules across, at most <see cref="MaximumSize"/>.</param>
    /// <param name="limit">The best penalty so far.</param>
    /// <returns>The penalty, or a value above <paramref name="limit"/> when it gave up early.</returns>
    internal static int Score(bool[] modules, int size, int limit)
    {
        Span<UInt128> rows = stackalloc UInt128[MaximumSize];
        rows = rows[..size];
        var dark = Pack(modules, size, rows);
        var score = ScoreAcross(rows, size);
        if (score > limit)
        {
            return score;
        }

        score += ScoreDown(rows, size);
        score += ScoreBlocks(rows, size);
        return score + ScoreBalance(dark, modules.Length);
    }

    /// <summary>Packs the rows, the first module of a row in the lowest bit.</summary>
    /// <param name="modules">The modules.</param>
    /// <param name="size">The modules across.</param>
    /// <param name="rows">Receives one word per row.</param>
    /// <returns>The number of dark modules.</returns>
    private static int Pack(bool[] modules, int size, Span<UInt128> rows)
    {
        var dark = 0;
        for (var y = 0; y < size; y++)
        {
            UInt128 row = 0;
            var line = modules.AsSpan(y * size, size);
            for (var x = 0; x < line.Length; x++)
            {
                row |= (line[x] ? UInt128.One : UInt128.Zero) << x;
            }

            dark += Count(row);
            rows[y] = row;
        }

        return dark;
    }

    /// <summary>Scores the runs and finder look-alikes along every row.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="size">The modules across.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreAcross(ReadOnlySpan<UInt128> rows, int size)
    {
        var runStarts = Low(size - RunWindow + 1);
        var finderStarts = Low(size - FinderLength + 1);
        var score = 0;
        foreach (var row in rows)
        {
            var light = ~row;
            var windows = (AndShifted(row, RunWindow) | AndShifted(light, RunWindow)) & runStarts;
            score += CountWindows(windows);
            score += FinderCost * (Count(MatchAcross(row, FinderAfter) & finderStarts) + Count(MatchAcross(row, FinderBefore) & finderStarts));
        }

        return score;
    }

    /// <summary>Scores the runs and finder look-alikes down every column, all columns at once.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="size">The modules across and down.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreDown(ReadOnlySpan<UInt128> rows, int size)
    {
        var columns = Low(size);
        var score = 0;
        UInt128 previous = 0;
        for (var y = 0; y + RunWindow <= size; y++)
        {
            var dark = columns;
            var light = columns;
            for (var k = 0; k < RunWindow; k++)
            {
                dark &= rows[y + k];
                light &= ~rows[y + k];
            }

            var windows = dark | light;
            score += Count(windows) + (RunStartExtra * Count(windows & ~previous));
            previous = windows;
        }

        for (var y = 0; y + FinderLength <= size; y++)
        {
            score += FinderCost * (Count(MatchDown(rows, y, FinderAfter, columns)) + Count(MatchDown(rows, y, FinderBefore, columns)));
        }

        return score;
    }

    /// <summary>Scores the two by two blocks of one colour.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="size">The modules across.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreBlocks(ReadOnlySpan<UInt128> rows, int size)
    {
        var starts = Low(size - 1);
        var score = 0;
        for (var y = 0; y + 1 < size; y++)
        {
            var upper = rows[y];
            var lower = rows[y + 1];
            var same = ~(upper ^ (upper >> 1)) & ~(lower ^ (lower >> 1)) & ~(upper ^ lower) & starts;
            score += BlockCost * Count(same);
        }

        return score;
    }

    /// <summary>Scores how far the share of dark modules is from half.</summary>
    /// <param name="dark">The dark modules.</param>
    /// <param name="total">The modules.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreBalance(int dark, int total)
    {
        var steps = ((Math.Abs((dark * Twenty) - (total * Ten)) + total - 1) / total) - 1;
        return steps * BalanceCost;
    }

    /// <summary>Scores the windows of one colour: each costs one, and the first window of a run costs two more.</summary>
    /// <param name="windows">The start of each window of five modules of one colour.</param>
    /// <returns>The penalty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountWindows(UInt128 windows) => Count(windows) + (RunStartExtra * Count(windows & ~(windows << 1)));

    /// <summary>ANDs a word with itself shifted by each of the next few places, so a set bit starts a run of that many ones.</summary>
    /// <param name="value">The word.</param>
    /// <param name="length">The run length.</param>
    /// <returns>A bit for each place where <paramref name="length"/> ones start.</returns>
    private static UInt128 AndShifted(UInt128 value, int length)
    {
        var result = value;
        for (var k = 1; k < length; k++)
        {
            result &= value >> k;
        }

        return result;
    }

    /// <summary>Finds where a row holds the finder look-alike.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="pattern">The pattern, the first module as the highest bit.</param>
    /// <returns>A bit for each place where the pattern starts.</returns>
    private static UInt128 MatchAcross(UInt128 row, int pattern)
    {
        var matches = ~UInt128.Zero;
        for (var k = 0; k < FinderLength; k++)
        {
            var shifted = row >> k;
            matches &= ((pattern >> (FinderLength - 1 - k)) & 1) == 1 ? shifted : ~shifted;
        }

        return matches;
    }

    /// <summary>Finds which columns hold the finder look-alike starting at a row.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="top">The row the pattern starts at.</param>
    /// <param name="pattern">The pattern, the first module as the highest bit.</param>
    /// <param name="columns">A bit for each column.</param>
    /// <returns>A bit for each column where the pattern starts at <paramref name="top"/>.</returns>
    private static UInt128 MatchDown(ReadOnlySpan<UInt128> rows, int top, int pattern, UInt128 columns)
    {
        var matches = columns;
        for (var k = 0; k < FinderLength; k++)
        {
            matches &= ((pattern >> (FinderLength - 1 - k)) & 1) == 1 ? rows[top + k] : ~rows[top + k];
        }

        return matches;
    }

    /// <summary>Makes a word with the lowest bits set.</summary>
    /// <param name="count">How many bits; zero or less gives no bits.</param>
    /// <returns>The word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UInt128 Low(int count) => count <= 0 ? UInt128.Zero : (UInt128.One << count) - UInt128.One;

    /// <summary>Counts the set bits of a word.</summary>
    /// <param name="value">The word.</param>
    /// <returns>The count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Count(UInt128 value) => (int)UInt128.PopCount(value);
}

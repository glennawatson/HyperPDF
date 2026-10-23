// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Scores a masked QR Code by how hard it would be to read; the mask with the lowest score is used.</summary>
internal static class QrPenalty
{
    /// <summary>The shortest run of one colour that costs points.</summary>
    private const int RunLimit = 5;

    /// <summary>The cost of a run of <see cref="RunLimit"/> modules, before extra modules add one each.</summary>
    private const int RunCost = 3;

    /// <summary>The cost of a two by two block of one colour.</summary>
    private const int BlockCost = 3;

    /// <summary>The cost of a look-alike of the finder pattern.</summary>
    private const int FinderCost = 40;

    /// <summary>The cost of each step the dark share is away from half.</summary>
    private const int BalanceCost = 10;

    /// <summary>The length of the finder look-alike, with four light modules on one side.</summary>
    private const int FinderLength = 11;

    /// <summary>The finder look-alike with its light modules after it, as bits with the first module highest.</summary>
    private const int FinderAfter = 0b10111010000;

    /// <summary>The finder look-alike with its light modules before it.</summary>
    private const int FinderBefore = 0b00001011101;

    /// <summary>Twenty, to scale the dark count against the total.</summary>
    private const int Twenty = 20;

    /// <summary>Ten, to scale the total.</summary>
    private const int Ten = 10;

    /// <summary>Works out the penalty.</summary>
    /// <param name="modules">The modules row by row; true is dark.</param>
    /// <param name="size">The modules across.</param>
    /// <returns>The penalty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Score(bool[] modules, int size) => Score(modules, size, int.MaxValue);

    /// <summary>Works out the penalty, giving up once it is more than a limit because the mask cannot win.</summary>
    /// <param name="modules">The modules row by row; true is dark.</param>
    /// <param name="size">The modules across.</param>
    /// <param name="limit">The best penalty so far.</param>
    /// <returns>The penalty, or a value above <paramref name="limit"/> when it gave up early.</returns>
    internal static int Score(bool[] modules, int size, int limit) =>
        size <= QrPackedPenalty.MaximumSize ? QrPackedPenalty.Score(modules, size, limit) : ScoreScalar(modules, size, limit);

    /// <summary>Works out the penalty one module at a time, for symbols too wide to pack a row in one word.</summary>
    /// <param name="modules">The modules row by row; true is dark.</param>
    /// <param name="size">The modules across.</param>
    /// <param name="limit">The best penalty so far.</param>
    /// <returns>The penalty, or a value above <paramref name="limit"/> when it gave up early.</returns>
    internal static int ScoreScalar(bool[] modules, int size, int limit)
    {
        var score = 0;
        for (var line = 0; line < size; line++)
        {
            score += ScoreLine(modules, size, line * size, 1);
            score += ScoreLine(modules, size, line, size);
            if (score > limit)
            {
                return score;
            }
        }

        return score + ScoreBlocks(modules, size) + ScoreBalance(modules);
    }

    /// <summary>Scores the runs and finder look-alikes along one row or column.</summary>
    /// <param name="modules">The modules.</param>
    /// <param name="size">The modules in the line.</param>
    /// <param name="start">The index of the first module.</param>
    /// <param name="step">The distance between modules.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreLine(bool[] modules, int size, int start, int step)
    {
        var score = 0;
        var run = 0;
        var window = 0;
        var previous = false;
        for (var i = 0; i < size; i++)
        {
            var dark = modules[start + (i * step)];
            run = i > 0 && dark == previous ? run + 1 : 1;
            previous = dark;
            if (run == RunLimit)
            {
                score += RunCost;
            }
            else if (run > RunLimit)
            {
                score++;
            }

            window = ((window << 1) | (dark ? 1 : 0)) & ((1 << FinderLength) - 1);
            if (i >= FinderLength - 1 && window is FinderAfter or FinderBefore)
            {
                score += FinderCost;
            }
        }

        return score;
    }

    /// <summary>Scores the two by two blocks of one colour.</summary>
    /// <param name="modules">The modules.</param>
    /// <param name="size">The modules across.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreBlocks(bool[] modules, int size)
    {
        var score = 0;
        for (var y = 0; y < size - 1; y++)
        {
            for (var x = 0; x < size - 1; x++)
            {
                var at = (y * size) + x;
                var colour = modules[at];
                if (colour == modules[at + 1] && colour == modules[at + size] && colour == modules[at + size + 1])
                {
                    score += BlockCost;
                }
            }
        }

        return score;
    }

    /// <summary>Scores how far the share of dark modules is from half.</summary>
    /// <param name="modules">The modules.</param>
    /// <returns>The penalty.</returns>
    private static int ScoreBalance(bool[] modules)
    {
        var dark = 0;
        foreach (var module in modules)
        {
            if (module)
            {
                dark++;
            }
        }

        var total = modules.Length;
        var steps = ((Math.Abs((dark * Twenty) - (total * Ten)) + total - 1) / total) - 1;
        return steps * BalanceCost;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Codes one row of changing elements: one-dimensionally as alternating runs (T.4) or against the row above (T.4 two-dimensional, T.6).</summary>
internal static class CcittLineEncoder
{
    /// <summary>The extra entries after a row's changes: room for the end sentinels.</summary>
    private const int LineSlack = 4;

    /// <summary>Codes a row as alternating white and black runs, starting with white.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="changes">The positions where the colour changes, without padding.</param>
    /// <param name="width">The pixels in a row.</param>
    internal static void Encode1D(ref CcittBitWriter bits, ReadOnlySpan<int> changes, int width)
    {
        var position = 0;
        var black = false;
        foreach (var change in changes)
        {
            CcittCodeTables.WriteRun(ref bits, change - position, black);
            position = change;
            black = !black;
        }

        CcittCodeTables.WriteRun(ref bits, width - position, black);
    }

    /// <summary>Codes a row against the row above it with pass, vertical and horizontal modes.</summary>
    /// <param name="bits">The bit writer.</param>
    /// <param name="current">The row's changes, padded with the width.</param>
    /// <param name="reference">The row above's changes, padded with the width.</param>
    /// <param name="width">The pixels in a row.</param>
    internal static void Encode2D(ref CcittBitWriter bits, ReadOnlySpan<int> current, ReadOnlySpan<int> reference, int width)
    {
        var a0 = -1;
        var black = false;
        var currentIndex = 0;
        var referenceIndex = 0;
        while (a0 < width)
        {
            currentIndex = FirstAfter(current, currentIndex, a0);
            referenceIndex = FirstAfter(reference, referenceIndex, a0);

            // b1 is the first change after a0 to the colour opposite a0's: even entries start black runs.
            var b1Index = referenceIndex + (((referenceIndex & 1) == 0) == black ? 1 : 0);
            var a1 = current[currentIndex];
            var b1 = reference[b1Index];
            var b2 = reference[b1Index + 1];
            if (b2 < a1)
            {
                bits.Write(CcittCodeTables.Modes[CcittCodeTables.PassMode]);
                a0 = b2;
                continue;
            }

            var offset = a1 - b1;
            if (Math.Abs(offset) <= CcittCodeTables.MaxVertical)
            {
                bits.Write(CcittCodeTables.Modes[CcittCodeTables.VerticalZero + offset]);
                a0 = a1;
                black = !black;
                continue;
            }

            var a2 = current[currentIndex + 1];
            bits.Write(CcittCodeTables.Modes[CcittCodeTables.HorizontalMode]);
            CcittCodeTables.WriteRun(ref bits, a1 - Math.Max(a0, 0), black);
            CcittCodeTables.WriteRun(ref bits, a2 - a1, !black);
            a0 = a2;
        }
    }

    /// <summary>Finds the first change after a position, starting from an index known to be at or before it.</summary>
    /// <param name="changes">The changes, padded with the width.</param>
    /// <param name="start">The index to start at.</param>
    /// <param name="position">The position.</param>
    /// <returns>The index of the first change after the position, or of the first padding entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FirstAfter(ReadOnlySpan<int> changes, int start, int position)
    {
        var index = start;
        while (index < changes.Length - LineSlack && changes[index] <= position)
        {
            index++;
        }

        return index;
    }
}

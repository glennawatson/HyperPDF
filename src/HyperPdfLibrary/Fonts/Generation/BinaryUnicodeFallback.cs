// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Derives missing single-character CID values from the collection's Unicode-to-CID mapping.</summary>
internal static class BinaryUnicodeFallback
{
    /// <summary>The largest Unicode scalar value.</summary>
    private const uint MaxScalar = 0x10FFFF;

    /// <summary>The number of mappings between cancellation checks.</summary>
    private const int CancellationChunk = 256;

    /// <summary>The largest candidate table kept on the stack.</summary>
    private const int StackCandidates = 256;

    /// <summary>Fills unmapped CIDs while preserving every explicit Unicode mapping.</summary>
    /// <param name="values">The preferred CID-to-Unicode values.</param>
    /// <param name="ranges">The collection's Unicode-to-CID ranges.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Fill(Span<int> values, ReadOnlySpan<CidRange> ranges) => Fill(values, ranges, CancellationToken.None);

    /// <summary>Fills unmapped CIDs while preserving every explicit Unicode mapping.</summary>
    /// <param name="values">The preferred CID-to-Unicode values.</param>
    /// <param name="ranges">The collection's Unicode-to-CID ranges.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    internal static void Fill(Span<int> values, ReadOnlySpan<CidRange> ranges, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (values.IsEmpty || ranges.IsEmpty || !values.Contains(0))
        {
            return;
        }

        int[]? rented = null;
        Span<int> candidates = values.Length <= StackCandidates
            ? stackalloc int[values.Length]
            : (rented = ArrayPool<int>.Shared.Rent(values.Length)).AsSpan(0, values.Length);
        try
        {
            candidates.Clear();
            FillCandidates(values, ranges, candidates, cancellationToken);
            ApplyCandidates(values, candidates, cancellationToken);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Collects canonical fallback values from Unicode ranges.</summary>
    /// <param name="values">The explicit mappings.</param>
    /// <param name="ranges">The inverse ranges.</param>
    /// <param name="candidates">Receives fallback candidates.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void FillCandidates(ReadOnlySpan<int> values, ReadOnlySpan<CidRange> ranges, Span<int> candidates, CancellationToken cancellationToken)
    {
        for (var rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
        {
            if (rangeIndex % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            FillRangeCandidates(values, ranges[rangeIndex], candidates, cancellationToken);
        }
    }

    /// <summary>Collects canonical fallback values from one Unicode range.</summary>
    /// <param name="values">The explicit mappings.</param>
    /// <param name="range">The inverse range.</param>
    /// <param name="candidates">Receives fallback candidates.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void FillRangeCandidates(ReadOnlySpan<int> values, CidRange range, Span<int> candidates, CancellationToken cancellationToken)
    {
        var high = Math.Min(range.High, MaxScalar);
        for (var code = range.Low; code <= high; code++)
        {
            if ((code - range.Low) % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var cid = range.Cid + (int)(code - range.Low);
            if ((uint)cid >= (uint)values.Length || values[cid] != 0 || !Rune.IsValid((int)code))
            {
                continue;
            }

            var candidate = UnicodeCanonicalScalars.Resolve((int)code);
            if (candidates[cid] == 0 || IsSpecial(candidates[cid]))
            {
                candidates[cid] = candidate;
            }
        }
    }

    /// <summary>Fills unmapped CIDs from the collected candidates.</summary>
    /// <param name="values">The explicit mappings to fill.</param>
    /// <param name="candidates">The fallback candidates.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void ApplyCandidates(Span<int> values, ReadOnlySpan<int> candidates, CancellationToken cancellationToken)
    {
        for (var cid = 0; cid < values.Length; cid++)
        {
            if (cid % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (values[cid] == 0)
            {
                values[cid] = candidates[cid];
            }
        }
    }

    /// <summary>Checks whether a candidate is a radical, presentation form or private-use glyph.</summary>
    /// <param name="value">The candidate.</param>
    /// <returns>Whether a later ordinary character should be preferred.</returns>
    private static bool IsSpecial(int value) =>
        value is >= '\u2E80' and <= '\u2FFF' or >= '\uFE10' and <= '\uFE6F' or >= '\uE000' and <= '\uFAFF';
}

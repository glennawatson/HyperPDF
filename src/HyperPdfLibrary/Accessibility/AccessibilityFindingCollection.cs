// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Collects capped findings in leased storage and creates detached report results.</summary>
internal static class AccessibilityFindingCollection
{
    /// <summary>Leases empty working storage for one report.</summary>
    /// <param name="pools">The document-owned storage.</param>
    /// <returns>Exclusively owned storage, returned with Release.</returns>
    internal static AccessibilityFindings Rent(ScratchPools pools)
    {
        var counts = pools.Ints.Rent(PdfAccessibilityMessages.CodeCount);
        counts.AsSpan().Clear();
        try
        {
            return new(pools.Slots<List<PdfAccessibilityFinding>>().Take() ?? [], counts);
        }
        catch
        {
            pools.Ints.Return(counts);
            throw;
        }
    }

    /// <summary>Clears and returns working storage without altering detached results.</summary>
    /// <param name="pools">The document-owned storage.</param>
    /// <param name="state">The report's working storage.</param>
    internal static void Release(ScratchPools pools, AccessibilityFindings state)
    {
        state.Findings.Clear();
        _ = pools.Slots<List<PdfAccessibilityFinding>>().Offer(state.Findings);
        pools.Ints.Return(state.Counts, true);
    }

    /// <summary>Adds a document-level finding.</summary>
    /// <param name="state">The report's working storage.</param>
    /// <param name="code">The finding code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(AccessibilityFindings state, PdfAccessibilityCode code) =>
        Add(state, code, AccessibilityFindings.DocumentLevel, default);

    /// <summary>Adds a page-level finding.</summary>
    /// <param name="state">The report's working storage.</param>
    /// <param name="code">The finding code.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(AccessibilityFindings state, PdfAccessibilityCode code, int pageIndex) =>
        Add(state, code, pageIndex, default);

    /// <summary>Counts every finding while keeping at most the cap for each code.</summary>
    /// <param name="state">The report's working storage.</param>
    /// <param name="code">The finding code.</param>
    /// <param name="pageIndex">The zero-based page index, or -1.</param>
    /// <param name="elementId">The structure element, when indirect.</param>
    internal static void Add(AccessibilityFindings state, PdfAccessibilityCode code, int pageIndex, PdfObjectId elementId)
    {
        var index = (int)code;
        state.Counts[index]++;
        if (state.Counts[index] <= AccessibilityFindings.MaxFindingsPerCode)
        {
            state.Findings.Add(new(code, PdfAccessibilityMessages.For(code), pageIndex, elementId.IsValid ? elementId : null));
        }
    }

    /// <summary>Copies nonzero counts in code order into an independently owned result.</summary>
    /// <param name="state">The report's working storage.</param>
    /// <returns>The report counts, or the shared empty array.</returns>
    internal static PdfAccessibilityCount[] GetCounts(AccessibilityFindings state)
    {
        var length = 0;
        for (var index = 1; index < PdfAccessibilityMessages.CodeCount; index++)
        {
            length += state.Counts[index] > 0 ? 1 : 0;
        }

        if (length == 0)
        {
            return [];
        }

        var counts = new PdfAccessibilityCount[length];
        var next = 0;
        for (var index = 1; index < PdfAccessibilityMessages.CodeCount; index++)
        {
            if (state.Counts[index] == 0)
            {
                continue;
            }

            counts[next] = new((PdfAccessibilityCode)index, state.Counts[index]);
            next++;
        }

        return counts;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Builds the merging candidate list of a prediction block and picks a candidate from it (8.5.3.2.2 to 8.5.3.2.5).</summary>
internal static class H265MergeCandidates
{
    /// <summary>The most candidates the list can hold.</summary>
    private const int MaxCandidates = 6;

    /// <summary>The number of spatial candidates.</summary>
    private const int SpatialCandidates = 4;

    /// <summary>The prediction block width plus height that forbids bi-prediction.</summary>
    private const int NoBiSize = 12;

    /// <summary>The coding unit size above which merge levels may be shared across its prediction blocks.</summary>
    private const int SharedListUnitSize = 8;

    /// <summary>The first merge level that shares candidate lists.</summary>
    private const int SharedLevel = 2;

    /// <summary>Gets the first list index of the pair used to build each combined candidate (Table 8-7).</summary>
    private static ReadOnlySpan<byte> CombinedFirst => [0x00, 0x01, 0x00, 0x02, 0x01, 0x02, 0x00, 0x03, 0x01, 0x03, 0x02, 0x03];

    /// <summary>Gets the second list index of the pair used to build each combined candidate (Table 8-7).</summary>
    private static ReadOnlySpan<byte> CombinedSecond => [0x01, 0x00, 0x02, 0x00, 0x02, 0x01, 0x03, 0x00, 0x03, 0x01, 0x03, 0x02];

    /// <summary>Derives the motion of a prediction block coded in merge mode.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="mergeIdx">The merge_idx.</param>
    /// <returns>The motion.</returns>
    internal static H265PuMotion Derive(H265SliceContext context, in H265PuBlock block, int mergeIdx)
    {
        var original = block.Area;
        var shared = block;
        if (context.Pps.Log2ParMrgLevel > SharedLevel && block.UnitSize == SharedListUnitSize)
        {
            shared = new(block.Unit with { PartMode = H265PartMode.Part2Nx2N }, new(block.Unit.X, block.Unit.Y, SharedListUnitSize, SharedListUnitSize), 0);
        }

        Span<H265PuMotion> list = stackalloc H265PuMotion[MaxCandidates];
        var count = BuildSpatial(context, shared, list);
        if (count <= mergeIdx)
        {
            count = AddTemporal(context, shared, list, count);
        }

        if (count <= mergeIdx && context.Header.SliceType == H265SliceType.B)
        {
            count = AddCombined(context, list, count);
        }

        if (count <= mergeIdx)
        {
            count = AddZero(context, list, count, mergeIdx);
        }

        var motion = list[mergeIdx];
        if (motion.UsesList0 && motion.UsesList1 && original.Width + original.Height == NoBiSize)
        {
            motion = motion with { RefIdx1 = -1, Vector1 = default };
        }

        return motion;
    }

    /// <summary>Adds the spatial candidates in the order A1, B1, B0, A0, B2.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block, or the coding unit's single block when the list is shared.</param>
    /// <param name="list">The candidate list.</param>
    /// <returns>The number of candidates.</returns>
    private static int BuildSpatial(H265SliceContext context, in H265PuBlock block, Span<H265PuMotion> list)
    {
        var area = block.Area;
        var a1 = Neighbour(context, block, area.X - 1, area.Y + area.Height - 1, SecondOfVerticalSplit(block));
        var b1 = Neighbour(context, block, area.X + area.Width - 1, area.Y - 1, SecondOfHorizontalSplit(block));
        var b0 = Neighbour(context, block, area.X + area.Width, area.Y - 1, false);
        var a0 = Neighbour(context, block, area.X - 1, area.Y + area.Height, false);
        var count = a1.Available ? Append(list, 0, a1.Motion) : 0;
        count = b1.IsNew(a1, default) ? Append(list, count, b1.Motion) : count;
        count = b0.IsNew(b1, default) ? Append(list, count, b0.Motion) : count;
        count = a0.IsNew(a1, default) ? Append(list, count, a0.Motion) : count;
        if (count == SpatialCandidates)
        {
            return count;
        }

        var b2 = Neighbour(context, block, area.X - 1, area.Y - 1, false);
        return b2.IsNew(a1, b1) ? Append(list, count, b2.Motion) : count;
    }

    /// <summary>Tells whether a block is the second of a split into a left and a right part, which may not merge with the first.</summary>
    /// <param name="block">The prediction block.</param>
    /// <returns><see langword="true"/> when the left neighbour is excluded.</returns>
    private static bool SecondOfVerticalSplit(in H265PuBlock block) =>
        block.PartIdx == 1 && block.Unit.PartMode is H265PartMode.PartNx2N or H265PartMode.PartnLx2N or H265PartMode.PartnRx2N;

    /// <summary>Tells whether a block is the second of a split into an upper and a lower part, which may not merge with the first.</summary>
    /// <param name="block">The prediction block.</param>
    /// <returns><see langword="true"/> when the upper neighbour is excluded.</returns>
    private static bool SecondOfHorizontalSplit(in H265PuBlock block) =>
        block.PartIdx == 1 && block.Unit.PartMode is H265PartMode.Part2NxN or H265PartMode.Part2NxnU or H265PartMode.Part2NxnD;

    /// <summary>Puts a candidate at the end of the list.</summary>
    /// <param name="list">The candidate list.</param>
    /// <param name="count">The candidates in the list.</param>
    /// <param name="motion">The candidate.</param>
    /// <returns>The new number of candidates.</returns>
    private static int Append(Span<H265PuMotion> list, int count, H265PuMotion motion)
    {
        list[count] = motion;
        return count + 1;
    }

    /// <summary>Finds a spatial neighbour that may supply a merge candidate: it must be available and outside the parallel merge region of the block.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="neighbourX">The neighbour's column.</param>
    /// <param name="neighbourY">The neighbour's row.</param>
    /// <param name="excluded">Whether the split of the coding unit rules the neighbour out.</param>
    /// <returns>The neighbour.</returns>
    private static H265MergeNeighbour Neighbour(H265SliceContext context, in H265PuBlock block, int neighbourX, int neighbourY, bool excluded)
    {
        var area = block.Area;
        var level = context.Pps.Log2ParMrgLevel;
        var inSameRegion = area.X >> level == neighbourX >> level && area.Y >> level == neighbourY >> level;
        var usable = !excluded && !inSameRegion && H265PuAvailability.IsAvailable(context, block, neighbourX, neighbourY);
        return new(usable, usable ? context.Picture.Motion[context.Picture.GridIndex(neighbourX, neighbourY)] : H265PuMotion.None);
    }

    /// <summary>Adds the temporal candidate.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The prediction block.</param>
    /// <param name="list">The candidate list.</param>
    /// <param name="count">The candidates in the list.</param>
    /// <returns>The new number of candidates.</returns>
    private static int AddTemporal(H265SliceContext context, in H265PuBlock block, Span<H265PuMotion> list, int count)
    {
        var useL0 = H265TemporalMotion.TryGet(context, block.Area, 0, 0, out var vector0);
        var useL1 = false;
        var vector1 = default(MotionVector);
        if (context.Header.SliceType == H265SliceType.B)
        {
            useL1 = H265TemporalMotion.TryGet(context, block.Area, 1, 0, out vector1);
        }

        if (!useL0 && !useL1)
        {
            return count;
        }

        list[count] = new(useL0 ? vector0 : default, useL1 ? vector1 : default, (sbyte)(useL0 ? 0 : -1), (sbyte)(useL1 ? 0 : -1));
        return count + 1;
    }

    /// <summary>Adds combined bi-predictive candidates built from pairs of the candidates so far (8.5.3.2.4).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="list">The candidate list.</param>
    /// <param name="count">The candidates in the list.</param>
    /// <returns>The new number of candidates.</returns>
    private static int AddCombined(H265SliceContext context, Span<H265PuMotion> list, int count)
    {
        var original = count;
        var maxCandidates = context.Header.Refs.MaxMergeCand;
        if (original <= 1 || original >= maxCandidates)
        {
            return count;
        }

        for (var combIdx = 0; combIdx < original * (original - 1); combIdx++)
        {
            var first = list[CombinedFirst[combIdx]];
            var second = list[CombinedSecond[combIdx]];
            if (first.UsesList0 && second.UsesList1 && (context.RefList0[first.RefIdx0].Poc != context.RefList1[second.RefIdx1].Poc || first.Vector0 != second.Vector1))
            {
                count = Append(list, count, new(first.Vector0, second.Vector1, first.RefIdx0, second.RefIdx1));
            }

            if (count == maxCandidates)
            {
                break;
            }
        }

        return count;
    }

    /// <summary>Adds zero motion candidates until the list is long enough (8.5.3.2.5).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="list">The candidate list.</param>
    /// <param name="count">The candidates in the list.</param>
    /// <param name="mergeIdx">The index of the candidate wanted.</param>
    /// <returns>The new number of candidates.</returns>
    private static int AddZero(H265SliceContext context, Span<H265PuMotion> list, int count, int mergeIdx)
    {
        var refs = context.Header.Refs;
        var isB = context.Header.SliceType == H265SliceType.B;
        var numRefIdx = isB ? Math.Min(refs.NumRefIdxL0, refs.NumRefIdxL1) : refs.NumRefIdxL0;
        var zeroIdx = 0;
        while (count <= mergeIdx)
        {
            var refIdx = (sbyte)(zeroIdx < numRefIdx ? zeroIdx : 0);
            count = Append(list, count, new(default, default, refIdx, isB ? refIdx : (sbyte)-1));
            zeroIdx++;
        }

        return count;
    }
}

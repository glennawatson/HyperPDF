// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>The deblocking filter that smooths the edges of blocks in a decoded frame (clause 8.7 of ITU-T H.264). Frames of 4:2:0 video with 8 bit samples and 4x4 transforms.</summary>
internal static class H264Deblocking
{
    /// <summary>The 4x4 blocks along a macroblock side, which is also the number of edges across it.</summary>
    private const int Side = 4;

    /// <summary>The 4x4 blocks along an 8x8 group.</summary>
    private const int Half = 2;

    /// <summary>The samples along an edge segment of luma, which is also the width of a 4x4 block.</summary>
    private const int LumaSegment = 4;

    /// <summary>The samples along the part of an edge that one luma segment covers in chroma.</summary>
    private const int ChromaSegment = 2;

    /// <summary>The strengths kept per direction: four edges of four segments.</summary>
    private const int PerDirection = Side * Side;

    /// <summary>The directions edges run in: vertical edges, then horizontal edges.</summary>
    private const int Directions = 2;

    /// <summary>The disable_deblocking_filter_idc value that filters nothing.</summary>
    private const int FilterNothing = 1;

    /// <summary>The disable_deblocking_filter_idc value that filters everything but the edges between slices.</summary>
    private const int FilterInsideSlices = 2;

    /// <summary>The boundary strength of an edge between macroblocks when one side is intra coded.</summary>
    private const int IntraMacroblockEdge = 4;

    /// <summary>The boundary strength of an edge inside a macroblock that is intra coded.</summary>
    private const int IntraEdge = 3;

    /// <summary>The boundary strength when a side has coefficients.</summary>
    private const int CoefficientEdge = 2;

    /// <summary>The largest sample value.</summary>
    private const int MaxSample = 255;

    /// <summary>The largest index into the threshold tables.</summary>
    private const int MaxIndex = 51;

    /// <summary>The shift that turns the weighted differences into the filter step.</summary>
    private const int DeltaShift = 3;

    /// <summary>The rounding of the filter step.</summary>
    private const int DeltaRound = 4;

    /// <summary>The shift that multiplies a difference by four in the filter step.</summary>
    private const int DifferenceShift = 2;

    /// <summary>The shift that divides alpha by four.</summary>
    private const int AlphaQuarterShift = 2;

    /// <summary>The slack added to the quarter of alpha when choosing the strong filter.</summary>
    private const int StrongSlack = 2;

    /// <summary>The weight of the third sample in the longest strong smoothing of the sample furthest from the edge.</summary>
    private const int ThirdWeight = 3;

    /// <summary>The distance in samples across an edge between the first and fourth sample.</summary>
    private const int Reach = 3;

    /// <summary>Filters a frame in place.</summary>
    /// <param name="picture">The decoded frame, whose per-macroblock records say how to filter it.</param>
    internal static void Apply(H264Picture picture)
    {
        var width = picture.MbWidth;
        Span<int> strength = stackalloc int[Directions * PerDirection];
        for (var address = 0; address < width * picture.MbHeight; address++)
        {
            var slice = picture.SliceOfMb[address];
            var filter = picture.SliceFilters[slice - 1];
            if (filter.Mode == FilterNothing)
            {
                continue;
            }

            var left = address % width > 0 && (filter.Mode != FilterInsideSlices || picture.SliceOfMb[address - 1] == slice);
            var top = address >= width && (filter.Mode != FilterInsideSlices || picture.SliceOfMb[address - width] == slice);
            ComputeStrengths(picture, address, new(left, top), strength);
            FilterPlane(picture, picture.Luma, new(picture.LumaStride, H264Picture.LumaSize, LumaSegment, 1, false, 0), address, filter, strength);
            FilterPlane(picture, picture.Cb, new(picture.ChromaStride, H264Picture.ChromaSize, ChromaSegment, Half, true, picture.ChromaQpOffsetCb), address, filter, strength);
            FilterPlane(picture, picture.Cr, new(picture.ChromaStride, H264Picture.ChromaSize, ChromaSegment, Half, true, picture.ChromaQpOffsetCr), address, filter, strength);
        }
    }

    /// <summary>Works out the boundary strength of every edge segment of a macroblock.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="address">The macroblock.</param>
    /// <param name="filtered">Whether its left and top macroblock edges are filtered.</param>
    /// <param name="strength">Receives 16 strengths for the vertical edges then 16 for the horizontal edges, each by edge then segment.</param>
    private static void ComputeStrengths(H264Picture picture, int address, H264EdgeFlags filtered, Span<int> strength)
    {
        if (picture.Kinds[address] != H264MbKind.Inter)
        {
            FillIntraStrengths(picture.Transform8x8[address], filtered, strength);
            return;
        }

        FillInterStrengths(picture, address, filtered, strength);
    }

    /// <summary>Works out the boundary strength of every edge segment of an inter macroblock, which depends on its neighbours' coefficients and motion.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="address">The macroblock.</param>
    /// <param name="filtered">Whether its left and top macroblock edges are filtered.</param>
    /// <param name="strength">Receives the strengths.</param>
    private static void FillInterStrengths(H264Picture picture, int address, H264EdgeFlags filtered, Span<int> strength)
    {
        for (var edge = 0; edge < Side; edge++)
        {
            for (var segment = 0; segment < Side; segment++)
            {
                var skipInner = (edge & 1) == 1 && picture.Transform8x8[address];
                strength[(edge * Side) + segment] = !skipInner && (edge > 0 || filtered.Left) ? VerticalStrength(picture, address, edge, segment) : 0;
                strength[PerDirection + (edge * Side) + segment] = !skipInner && (edge > 0 || filtered.Top) ? HorizontalStrength(picture, address, edge, segment) : 0;
            }
        }
    }

    /// <summary>Gives every edge segment of an intra macroblock its strength: the same for a whole edge, so no neighbour needs to be looked at.</summary>
    /// <param name="transform8x8">Whether the macroblock uses the 8x8 transform, which leaves out the inner edges between 4x4 blocks.</param>
    /// <param name="filtered">Whether its left and top macroblock edges are filtered.</param>
    /// <param name="strength">Receives the strengths.</param>
    private static void FillIntraStrengths(bool transform8x8, H264EdgeFlags filtered, Span<int> strength)
    {
        for (var edge = 0; edge < Side; edge++)
        {
            var skipInner = (edge & 1) == 1 && transform8x8;
            var level = edge == 0 ? IntraMacroblockEdge : IntraEdge;
            var vertical = skipInner || (edge == 0 && !filtered.Left) ? 0 : level;
            var horizontal = skipInner || (edge == 0 && !filtered.Top) ? 0 : level;
            strength.Slice(edge * Side, Side).Fill(vertical);
            strength.Slice(PerDirection + (edge * Side), Side).Fill(horizontal);
        }
    }

    /// <summary>Works out the strength of one segment of a vertical edge.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="address">The macroblock.</param>
    /// <param name="edge">The edge, counting from the left.</param>
    /// <param name="segment">The segment, counting from the top.</param>
    /// <returns>The strength.</returns>
    private static int VerticalStrength(H264Picture picture, int address, int edge, int segment)
    {
        H264BlockAt before = edge == 0 ? new(address - 1, Side - 1, segment) : new(address, edge - 1, segment);
        return Strength(picture, new(address, edge, segment), before, edge == 0);
    }

    /// <summary>Works out the strength of one segment of a horizontal edge.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="address">The macroblock.</param>
    /// <param name="edge">The edge, counting from the top.</param>
    /// <param name="segment">The segment, counting from the left.</param>
    /// <returns>The strength.</returns>
    private static int HorizontalStrength(H264Picture picture, int address, int edge, int segment)
    {
        H264BlockAt above = edge == 0 ? new(address - picture.MbWidth, segment, Side - 1) : new(address, segment, edge - 1);
        return Strength(picture, new(address, segment, edge), above, edge == 0);
    }

    /// <summary>Tells whether the transform block that holds a 4x4 block has non-zero coefficients: the 4x4 block itself, or its whole 8x8 block when the macroblock uses the 8x8 transform.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="block">The 4x4 block.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasCoefficients(H264Picture picture, H264BlockAt block)
    {
        var origin = block.Address * H264Picture.CountsPerMb;
        if (!picture.Transform8x8[block.Address])
        {
            return picture.Counts[origin + (block.Row * Side) + block.Column] != 0;
        }

        var first = origin + (((block.Row >> 1) * Half) * Side) + ((block.Column >> 1) * Half);
        return (picture.Counts[first] | picture.Counts[first + 1] | picture.Counts[first + Side] | picture.Counts[first + Side + 1]) != 0;
    }

    /// <summary>Works out the strength of one segment of an edge.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="after">The block on the right of or below the edge.</param>
    /// <param name="before">The block on the left of or above the edge.</param>
    /// <param name="macroblockEdge">Whether the edge is between macroblocks.</param>
    /// <returns>The strength, 0 to 4.</returns>
    private static int Strength(H264Picture picture, H264BlockAt after, H264BlockAt before, bool macroblockEdge)
    {
        if (picture.Kinds[after.Address] != H264MbKind.Inter || picture.Kinds[before.Address] != H264MbKind.Inter)
        {
            return macroblockEdge ? IntraMacroblockEdge : IntraEdge;
        }

        if (HasCoefficients(picture, after) || HasCoefficients(picture, before))
        {
            return CoefficientEdge;
        }

        return H264MotionCompare.Differs(picture, after, before) ? 1 : 0;
    }

    /// <summary>Filters the edges of one plane of a macroblock: the vertical edges from left to right, then the horizontal edges from top to bottom.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="layout">The plane's geometry.</param>
    /// <param name="address">The macroblock.</param>
    /// <param name="filter">The slice's filter settings.</param>
    /// <param name="strength">The strengths.</param>
    private static void FilterPlane(H264Picture picture, byte[] plane, H264PlaneLayout layout, int address, H264SliceFilter filter, ReadOnlySpan<int> strength)
    {
        var origin = ((address / picture.MbWidth) * layout.MbSamples * layout.Stride) + ((address % picture.MbWidth) * layout.MbSamples);
        FilterDirection(picture, plane, layout, new(address, origin, 0), filter, strength);
        FilterDirection(picture, plane, layout, new(address, origin, 1), filter, strength);
    }

    /// <summary>Filters the edges of one direction of one plane of a macroblock.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="plane">The plane.</param>
    /// <param name="layout">The plane's geometry.</param>
    /// <param name="where">The macroblock, the index of its first sample in the plane, and the direction: 0 for vertical edges, 1 for horizontal.</param>
    /// <param name="filter">The slice's filter settings.</param>
    /// <param name="strength">The strengths.</param>
    private static void FilterDirection(H264Picture picture, byte[] plane, H264PlaneLayout layout, H264EdgePass where, H264SliceFilter filter, ReadOnlySpan<int> strength)
    {
        var vertical = where.Direction == 0;
        var across = vertical ? 1 : layout.Stride;
        var along = vertical ? layout.Stride : 1;
        for (var edge = 0; edge < Side; edge += layout.EdgeStep)
        {
            if (!AnyStrength(strength, where.Direction, edge))
            {
                continue;
            }

            var neighbour = where.Address;
            if (edge == 0)
            {
                neighbour = vertical ? where.Address - 1 : where.Address - picture.MbWidth;
            }

            var limits = Limits(filter, AverageQp(picture, where.Address, neighbour, layout));
            for (var segment = 0; segment < Side; segment++)
            {
                var start = where.Origin + (((edge / layout.EdgeStep) * LumaSegment) * across) + (segment * layout.SegmentLength * along);
                FilterSegment(plane, start, new(across, along, layout.SegmentLength), strength[(where.Direction * PerDirection) + (edge * Side) + segment], limits, layout.Chroma);
            }
        }
    }

    /// <summary>Tells whether any segment of an edge has a non-zero strength.</summary>
    /// <param name="strength">The strengths.</param>
    /// <param name="direction">0 for vertical edges, 1 for horizontal.</param>
    /// <param name="edge">The edge.</param>
    /// <returns><see langword="true"/> when some segment is filtered.</returns>
    private static bool AnyStrength(ReadOnlySpan<int> strength, int direction, int edge)
    {
        for (var segment = 0; segment < Side; segment++)
        {
            if (strength[(direction * PerDirection) + (edge * Side) + segment] != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Averages the quantisers of the two macroblocks either side of an edge, in chroma terms for a chroma plane.</summary>
    /// <param name="picture">The frame.</param>
    /// <param name="address">The macroblock being filtered.</param>
    /// <param name="other">The macroblock on the other side of the edge, or the same macroblock for an inner edge.</param>
    /// <param name="layout">The plane's geometry.</param>
    /// <returns>The average quantiser.</returns>
    private static int AverageQp(H264Picture picture, int address, int other, H264PlaneLayout layout)
    {
        var first = layout.Chroma ? ChromaQp(picture.Qp[address], layout.QpOffset) : picture.Qp[address];
        var second = layout.Chroma ? ChromaQp(picture.Qp[other], layout.QpOffset) : picture.Qp[other];
        return (first + second + 1) >> 1;
    }

    /// <summary>Gets the chroma quantiser that goes with a luma quantiser.</summary>
    /// <param name="lumaQp">The luma quantiser.</param>
    /// <param name="offset">The chroma quantiser offset.</param>
    /// <returns>The chroma quantiser.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ChromaQp(int lumaQp, int offset) => H264Tables.ChromaQp(Math.Clamp(lumaQp + offset, 0, MaxIndex));

    /// <summary>Works out the thresholds of an edge from the average quantiser of the two sides (clause 8.7.2.2).</summary>
    /// <param name="filter">The slice's filter settings.</param>
    /// <param name="qp">The average quantiser of the two sides.</param>
    /// <returns>The thresholds.</returns>
    private static H264EdgeLimits Limits(H264SliceFilter filter, int qp)
    {
        var indexA = Math.Clamp(qp + filter.AlphaOffset, 0, MaxIndex);
        var indexB = Math.Clamp(qp + filter.BetaOffset, 0, MaxIndex);
        return new(H264DeblockTables.Alpha[indexA], H264DeblockTables.Beta[indexB], indexA);
    }

    /// <summary>Filters the samples along one segment of an edge.</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="start">The index of the first sample on the far side of the edge (q0).</param>
    /// <param name="line">How the samples are laid out around the edge.</param>
    /// <param name="strength">The boundary strength, 0 to 4.</param>
    /// <param name="limits">The thresholds.</param>
    /// <param name="chroma">Whether the samples are chroma, which only changes the two samples next to the edge.</param>
    private static void FilterSegment(byte[] plane, int start, H264EdgeLine line, int strength, H264EdgeLimits limits, bool chroma)
    {
        if (strength == 0 || limits.Alpha == 0 || limits.Beta == 0)
        {
            return;
        }

        for (var i = 0; i < line.Length; i++)
        {
            var q0 = start + (i * line.Along);
            if (strength < IntraMacroblockEdge)
            {
                FilterNormal(plane, q0, line.Across, strength, limits, chroma);
            }
            else
            {
                FilterStrong(plane, q0, line.Across, limits, chroma);
            }
        }
    }

    /// <summary>Filters one set of samples across an edge with a boundary strength below 4 (clause 8.7.2.3).</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="q0">The index of q0.</param>
    /// <param name="across">The distance between samples across the edge.</param>
    /// <param name="strength">The boundary strength, 1 to 3.</param>
    /// <param name="limits">The thresholds.</param>
    /// <param name="chroma">Whether the samples are chroma.</param>
    private static void FilterNormal(byte[] plane, int q0, int across, int strength, H264EdgeLimits limits, bool chroma)
    {
        int p0 = plane[q0 - across];
        int p1 = plane[q0 - (Half * across)];
        int q0Value = plane[q0];
        int q1 = plane[q0 + across];
        if (Math.Abs(p0 - q0Value) >= limits.Alpha || Math.Abs(p1 - p0) >= limits.Beta || Math.Abs(q1 - q0Value) >= limits.Beta)
        {
            return;
        }

        var tc0 = H264DeblockTables.TcZero[((strength - 1) * H264DeblockTables.IndexCount) + limits.IndexA];
        var tc = tc0 + 1;
        var p2 = 0;
        var q2 = 0;
        var smoothP = false;
        var smoothQ = false;
        if (!chroma)
        {
            p2 = plane[q0 - (Reach * across)];
            q2 = plane[q0 + (Half * across)];
            smoothP = Math.Abs(p2 - p0) < limits.Beta;
            smoothQ = Math.Abs(q2 - q0Value) < limits.Beta;
            tc = tc0 + (smoothP ? 1 : 0) + (smoothQ ? 1 : 0);
        }

        var delta = Math.Clamp((((q0Value - p0) << DifferenceShift) + (p1 - q1) + DeltaRound) >> DeltaShift, -tc, tc);
        plane[q0 - across] = (byte)Math.Clamp(p0 + delta, 0, MaxSample);
        plane[q0] = (byte)Math.Clamp(q0Value - delta, 0, MaxSample);
        if (smoothP)
        {
            plane[q0 - (Half * across)] = (byte)(p1 + Math.Clamp((p2 + ((p0 + q0Value + 1) >> 1) - (p1 << 1)) >> 1, -tc0, tc0));
        }

        if (smoothQ)
        {
            plane[q0 + across] = (byte)(q1 + Math.Clamp((q2 + ((p0 + q0Value + 1) >> 1) - (q1 << 1)) >> 1, -tc0, tc0));
        }
    }

    /// <summary>Filters one set of samples across an edge with a boundary strength of 4 (clause 8.7.2.4).</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="q0">The index of q0.</param>
    /// <param name="across">The distance between samples across the edge.</param>
    /// <param name="limits">The thresholds.</param>
    /// <param name="chroma">Whether the samples are chroma.</param>
    private static void FilterStrong(byte[] plane, int q0, int across, H264EdgeLimits limits, bool chroma)
    {
        int p0 = plane[q0 - across];
        int p1 = plane[q0 - (Half * across)];
        int q0Value = plane[q0];
        int q1 = plane[q0 + across];
        if (Math.Abs(p0 - q0Value) >= limits.Alpha || Math.Abs(p1 - p0) >= limits.Beta || Math.Abs(q1 - q0Value) >= limits.Beta)
        {
            return;
        }

        if (chroma)
        {
            plane[q0 - across] = (byte)(((Half * p1) + p0 + q1 + Half) >> Half);
            plane[q0] = (byte)(((Half * q1) + q0Value + p1 + Half) >> Half);
            return;
        }

        var close = Math.Abs(p0 - q0Value) < ((limits.Alpha >> AlphaQuarterShift) + StrongSlack);
        int p2 = plane[q0 - (Reach * across)];
        int q2 = plane[q0 + (Half * across)];
        var nearSide = new H264EdgeSide(p0, p1, p2, plane[q0 - (Half * Half * across)]);
        var farSide = new H264EdgeSide(q0Value, q1, q2, plane[q0 + (Reach * across)]);
        StrongSide(plane, q0 - across, -across, nearSide, farSide, close && Math.Abs(p2 - p0) < limits.Beta);
        StrongSide(plane, q0, across, farSide, nearSide, close && Math.Abs(q2 - q0Value) < limits.Beta);
    }

    /// <summary>Writes the strongly filtered samples of one side of an edge.</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="index">The index of the side's sample next to the edge.</param>
    /// <param name="step">The step away from the edge: negative on the near side, positive on the far side.</param>
    /// <param name="side">The side's four samples, nearest the edge first.</param>
    /// <param name="other">The other side's samples.</param>
    /// <param name="wide">Whether the long smoothing applies.</param>
    private static void StrongSide(byte[] plane, int index, int step, H264EdgeSide side, H264EdgeSide other, bool wide)
    {
        if (!wide)
        {
            plane[index] = (byte)(((Half * side.S1) + side.S0 + other.S1 + Half) >> Half);
            return;
        }

        plane[index] = (byte)((side.S2 + (Half * side.S1) + (Half * side.S0) + (Half * other.S0) + other.S1 + DeltaRound) >> DeltaShift);
        plane[index + step] = (byte)((side.S2 + side.S1 + side.S0 + other.S0 + Half) >> Half);
        plane[index + (Half * step)] = (byte)(((Half * side.S3) + (ThirdWeight * side.S2) + side.S1 + side.S0 + other.S0 + DeltaRound) >> DeltaShift);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Collects, completes and smooths the neighbouring samples of an intra block (8.4.4.2.1 to 8.4.4.2.3), in a line from the left column up to the corner and along the top.</summary>
internal static class H265IntraReference
{
    /// <summary>The base 2 logarithm of the block side that can take the strong filter.</summary>
    private const int StrongLog2 = 5;

    /// <summary>The sample count of the block side that can take the strong filter.</summary>
    private const int StrongSide = 1 << StrongLog2;

    /// <summary>The line index of the corner sample of a block that can take the strong filter.</summary>
    private const int StrongCorner = StrongSide * 2;

    /// <summary>The line index of the middle of the top row of a block that can take the strong filter.</summary>
    private const int StrongTopMiddle = StrongSide * 3;

    /// <summary>The line index of the far end of the top row of a block that can take the strong filter.</summary>
    private const int StrongEnd = StrongSide * 4;

    /// <summary>The bit depth term of the strong filter's flatness test.</summary>
    private const int StrongShift = 5;

    /// <summary>The weight total of the strong filter.</summary>
    private const int StrongTotal = 64;

    /// <summary>The shift of the strong filter's weights.</summary>
    private const int StrongRampShift = 6;

    /// <summary>The unit, in luma samples, in which availability is known.</summary>
    private const int UnitLog2 = 2;

    /// <summary>The shift that turns a block side into the length of one arm of the line: the left column below and the top row to the right both run for two block sides.</summary>
    private const int ArmShift = 1;

    /// <summary>The rounding added to the weights of the smoothing filter.</summary>
    private const int SmoothRound = 2;

    /// <summary>The shift of the weights of the smoothing filter.</summary>
    private const int SmoothShift = 2;

    /// <summary>Gets the distance from the horizontal and vertical modes above which a block of each size is smoothed; a size that is never smoothed holds 255.</summary>
    private static ReadOnlySpan<byte> FilterThresholds => [0xFF, 0xFF, 0xFF, 0x07, 0x01, 0x00];

    /// <summary>Gets the neighbouring samples of a block.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">Receives 4n+1 samples.</param>
    /// <param name="available">Scratch of the same length that receives whether each sample was available.</param>
    internal static void Gather(H265SliceContext context, in H265IntraBlock block, Span<int> line, Span<bool> available)
    {
        var picture = context.Picture;
        var sps = context.Sps;
        var component = block.CIdx;
        var subW = component == 0 ? 1 : sps.SubWidthC;
        var subH = component == 0 ? 1 : sps.SubHeightC;
        var source = new H265NeighbourSource(
            picture.PlaneOf(component),
            picture.StrideOf(component),
            subW,
            subH,
            block.X * subW,
            block.Y * subH,
            context.Pps.Flags.ConstrainedIntraPred);
        var any = GatherLeft(context, source, block, line, available);
        any |= GatherCorner(context, source, block, line, available);
        any |= GatherTop(context, source, block, line, available);
        Substitute(line, available, any, 1 << ((component == 0 ? sps.BitDepthLuma : sps.BitDepthChroma) - 1));
    }

    /// <summary>Smooths the neighbouring samples when the block size and mode ask for it (8.4.4.2.3).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">The neighbouring samples, replaced by the smoothed samples.</param>
    /// <param name="scratch">Scratch of the same length.</param>
    internal static void Filter(H265SliceContext context, in H265IntraBlock block, Span<int> line, Span<int> scratch)
    {
        var sps = context.Sps;
        if (sps.Range.IntraSmoothingDisabled || (block.CIdx != 0 && sps.ChromaArrayType != ChromaSampling.Yuv444))
        {
            return;
        }

        if (!NeedsFilter(block.Mode, block.Log2Size))
        {
            return;
        }

        if (UsesStrongFilter(context, block, line))
        {
            StrongFilter(line);
            return;
        }

        var last = ((1 << block.Log2Size) << ArmShift) << 1;
        line[..(last + 1)].CopyTo(scratch);
        for (var k = 1; k < last; k++)
        {
            line[k] = (scratch[k - 1] + (scratch[k] << 1) + scratch[k + 1] + SmoothRound) >> SmoothShift;
        }
    }

    /// <summary>Reads the left column from the bottom up, and records which samples are available.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="source">Where the samples come from.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">Receives the samples.</param>
    /// <param name="available">Receives whether each sample was available.</param>
    /// <returns><see langword="true"/> when at least one sample was available.</returns>
    private static bool GatherLeft(H265SliceContext context, in H265NeighbourSource source, in H265IntraBlock block, Span<int> line, Span<bool> available)
    {
        var arm = (1 << block.Log2Size) << ArmShift;
        var unit = Math.Max(1, (1 << UnitLog2) / source.SubH);
        var any = false;
        for (var y = 0; y < arm; y += unit)
        {
            var ok = IsUsable(context, source, block.X - 1, block.Y + y);
            any |= ok;
            for (var j = 0; j < unit; j++)
            {
                var k = arm - 1 - (y + j);
                available[k] = ok;
                line[k] = ok ? source.Plane[((block.Y + y + j) * source.Stride) + block.X - 1] : 0;
            }
        }

        return any;
    }

    /// <summary>Reads the corner sample.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="source">Where the samples come from.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">Receives the sample.</param>
    /// <param name="available">Receives whether the sample was available.</param>
    /// <returns><see langword="true"/> when the sample was available.</returns>
    private static bool GatherCorner(H265SliceContext context, in H265NeighbourSource source, in H265IntraBlock block, Span<int> line, Span<bool> available)
    {
        var arm = (1 << block.Log2Size) << ArmShift;
        var ok = IsUsable(context, source, block.X - 1, block.Y - 1);
        available[arm] = ok;
        line[arm] = ok ? source.Plane[((block.Y - 1) * source.Stride) + block.X - 1] : 0;
        return ok;
    }

    /// <summary>Reads the top row from left to right, and records which samples are available.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="source">Where the samples come from.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">Receives the samples.</param>
    /// <param name="available">Receives whether each sample was available.</param>
    /// <returns><see langword="true"/> when at least one sample was available.</returns>
    private static bool GatherTop(H265SliceContext context, in H265NeighbourSource source, in H265IntraBlock block, Span<int> line, Span<bool> available)
    {
        var arm = (1 << block.Log2Size) << ArmShift;
        var unit = Math.Max(1, (1 << UnitLog2) / source.SubW);
        var any = false;
        for (var x = 0; x < arm; x += unit)
        {
            var ok = IsUsable(context, source, block.X + x, block.Y - 1);
            any |= ok;
            for (var i = 0; i < unit; i++)
            {
                var k = arm + 1 + x + i;
                available[k] = ok;
                line[k] = ok ? source.Plane[((block.Y - 1) * source.Stride) + block.X + x + i] : 0;
            }
        }

        return any;
    }

    /// <summary>Tells whether the mode and size are among those whose neighbouring samples are smoothed.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="log2Size">The base 2 logarithm of the block side.</param>
    /// <returns><see langword="true"/> when the filter applies.</returns>
    private static bool NeedsFilter(int mode, int log2Size)
    {
        var distance = Math.Min(Math.Abs(mode - H265IntraModes.Vertical), Math.Abs(mode - H265IntraModes.Horizontal));
        return mode != H265IntraModes.Dc && distance > FilterThresholds[log2Size];
    }

    /// <summary>Tells whether a block of 32 luma samples with smooth edges takes the strong filter (the biIntFlag of 8.4.4.2.3).</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    /// <param name="line">The neighbouring samples.</param>
    /// <returns><see langword="true"/> when the strong filter applies.</returns>
    private static bool UsesStrongFilter(H265SliceContext context, in H265IntraBlock block, ReadOnlySpan<int> line)
    {
        if (!context.Sps.Tools.StrongIntraSmoothing || block.CIdx != 0 || block.Log2Size != StrongLog2)
        {
            return false;
        }

        var threshold = 1 << (context.Sps.BitDepthLuma - StrongShift);
        var corner = line[StrongCorner];
        var top = Math.Abs(corner + line[StrongEnd] - (line[StrongTopMiddle] << 1));
        var left = Math.Abs(corner + line[0] - (line[StrongSide] << 1));
        return top < threshold && left < threshold;
    }

    /// <summary>Replaces the neighbouring samples by a linear ramp between the corner and the two far ends (8-36 to 8-40).</summary>
    /// <param name="line">The neighbouring samples of a 32x32 block.</param>
    private static void StrongFilter(Span<int> line)
    {
        var corner = line[StrongCorner];
        var bottom = line[0];
        var right = line[StrongEnd];
        for (var i = 0; i < StrongTotal - 1; i++)
        {
            line[StrongCorner - 1 - i] = (((StrongTotal - 1 - i) * corner) + ((i + 1) * bottom) + StrongSide) >> StrongRampShift;
            line[StrongCorner + 1 + i] = (((StrongTotal - 1 - i) * corner) + ((i + 1) * right) + StrongSide) >> StrongRampShift;
        }
    }

    /// <summary>Tells whether a neighbouring sample can be used to predict from.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="source">Where the samples come from.</param>
    /// <param name="neighbourX">The neighbour's column in the plane.</param>
    /// <param name="neighbourY">The neighbour's row in the plane.</param>
    /// <returns><see langword="true"/> when the sample is available for intra prediction.</returns>
    private static bool IsUsable(H265SliceContext context, in H265NeighbourSource source, int neighbourX, int neighbourY)
    {
        var lumaX = neighbourX * source.SubW;
        var lumaY = neighbourY * source.SubH;
        if (!H265Availability.IsAvailable(context, source.CurrentX, source.CurrentY, lumaX, lumaY))
        {
            return false;
        }

        return !source.Constrained || context.Picture.Kind[context.Picture.GridIndex(lumaX, lumaY)] == H265Picture.Intra;
    }

    /// <summary>Fills in the samples that are not available (8.4.4.2.2).</summary>
    /// <param name="line">The neighbouring samples.</param>
    /// <param name="available">Whether each sample is available.</param>
    /// <param name="any">Whether at least one sample is available.</param>
    /// <param name="middle">The value to use when none is available.</param>
    private static void Substitute(Span<int> line, ReadOnlySpan<bool> available, bool any, int middle)
    {
        if (!any)
        {
            line.Fill(middle);
            return;
        }

        if (!available[0])
        {
            var k = 1;
            while (!available[k])
            {
                k++;
            }

            line[0] = line[k];
        }

        for (var k = 1; k < line.Length; k++)
        {
            if (!available[k])
            {
                line[k] = line[k - 1];
            }
        }
    }
}

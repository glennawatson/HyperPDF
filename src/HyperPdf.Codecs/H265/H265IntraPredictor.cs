// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Predicts an intra block from its neighbouring samples: planar, DC and the angular modes (8.4.4.2.4 to 8.4.4.2.6). The prediction is written to the picture.</summary>
internal static class H265IntraPredictor
{
    /// <summary>The samples in the line of neighbours of the largest block: four sides and the corner.</summary>
    private const int MaxLine = (MaxSide * SidesPerBlock) + 1;

    /// <summary>The longest block side.</summary>
    private const int MaxSide = 32;

    /// <summary>The block sides the neighbouring line covers: the left column twice and the top row twice.</summary>
    private const int SidesPerBlock = 4;

    /// <summary>The samples in the main reference line of the largest block, with the projected samples before it.</summary>
    private const int MaxReference = (MaxSide * 3) + 2;

    /// <summary>The bits of the fraction of an angle step.</summary>
    private const int FractionBits = 5;

    /// <summary>The mask of the fraction of an angle step.</summary>
    private const int FractionMask = (1 << FractionBits) - 1;

    /// <summary>The weight total of the two reference samples an angular prediction blends.</summary>
    private const int FractionTotal = 1 << FractionBits;

    /// <summary>The rounding of the angular blend.</summary>
    private const int FractionRound = 1 << (FractionBits - 1);

    /// <summary>The rounding of the inverse angle projection.</summary>
    private const int InverseRound = 128;

    /// <summary>The shift of the inverse angle projection.</summary>
    private const int InverseShift = 8;

    /// <summary>The rounding of the DC edge smoothing.</summary>
    private const int DcRound = 2;

    /// <summary>The shift of the DC edge smoothing.</summary>
    private const int DcShift = 2;

    /// <summary>The largest block side that gets the boundary smoothing of the DC, horizontal and vertical modes.</summary>
    private const int BoundarySmoothingBelow = MaxSide;

    /// <summary>The first mode that predicts from the top row.</summary>
    private const int FirstVerticalMode = 18;

    /// <summary>Predicts a block and writes the prediction to the picture.</summary>
    /// <param name="context">The slice context.</param>
    /// <param name="block">The block.</param>
    internal static void Predict(H265SliceContext context, in H265IntraBlock block)
    {
        Span<int> line = stackalloc int[MaxLine];
        Span<int> scratch = stackalloc int[MaxLine];
        Span<bool> available = stackalloc bool[MaxLine];
        var size = 1 << block.Log2Size;
        var used = (size * SidesPerBlock) + 1;
        H265IntraReference.Gather(context, block, line[..used], available[..used]);
        H265IntraReference.Filter(context, block, line[..used], scratch[..used]);
        var neighbours = new H265IntraNeighbours(line[..used], size);
        var picture = context.Picture;
        var stride = picture.StrideOf(block.CIdx);
        var target = new H265PredictionTarget(picture.PlaneOf(block.CIdx), stride, (block.Y * stride) + block.X);
        var bitDepth = block.CIdx == 0 ? context.Sps.BitDepthLuma : context.Sps.BitDepthChroma;
        if (block.Mode == H265IntraModes.Planar)
        {
            PredictPlanar(neighbours, target, block.Log2Size);
        }
        else if (block.Mode == H265IntraModes.Dc)
        {
            PredictDc(neighbours, target, block.Log2Size, block.CIdx == 0 && size < BoundarySmoothingBelow);
        }
        else
        {
            PredictAngular(neighbours, target, block, bitDepth, context.Sps.Range.ImplicitRdpcm && block.Bypass);
        }
    }

    /// <summary>Predicts with the planar mode (8-46).</summary>
    /// <param name="p">The neighbouring samples.</param>
    /// <param name="target">Where the prediction is written.</param>
    /// <param name="log2Size">The base 2 logarithm of the block side.</param>
    private static void PredictPlanar(H265IntraNeighbours p, H265PredictionTarget target, int log2Size)
    {
        var size = 1 << log2Size;
        var topRight = p.Top(size);
        var bottomLeft = p.Left(size);
        for (var y = 0; y < size; y++)
        {
            var offset = target.Offset + (y * target.Stride);
            for (var x = 0; x < size; x++)
            {
                var value = ((size - 1 - x) * p.Left(y)) + ((x + 1) * topRight) + ((size - 1 - y) * p.Top(x)) + ((y + 1) * bottomLeft) + size;
                target.Plane[offset + x] = (ushort)(value >> (log2Size + 1));
            }
        }
    }

    /// <summary>Predicts with the DC mode (8-47 to 8-52).</summary>
    /// <param name="p">The neighbouring samples.</param>
    /// <param name="target">Where the prediction is written.</param>
    /// <param name="log2Size">The base 2 logarithm of the block side.</param>
    /// <param name="smoothEdges">Whether the first row and column are blended with the neighbours.</param>
    private static void PredictDc(H265IntraNeighbours p, H265PredictionTarget target, int log2Size, bool smoothEdges)
    {
        var size = 1 << log2Size;
        var sum = size;
        for (var i = 0; i < size; i++)
        {
            sum += p.Top(i) + p.Left(i);
        }

        var dc = sum >> (log2Size + 1);
        for (var y = 0; y < size; y++)
        {
            target.Plane.AsSpan(target.Offset + (y * target.Stride), size).Fill((ushort)dc);
        }

        if (!smoothEdges)
        {
            return;
        }

        var weighted = (dc << 1) + dc;
        target.Plane[target.Offset] = (ushort)((p.Left(0) + (dc << 1) + p.Top(0) + DcRound) >> DcShift);
        for (var x = 1; x < size; x++)
        {
            target.Plane[target.Offset + x] = (ushort)((p.Top(x) + weighted + DcRound) >> DcShift);
        }

        for (var y = 1; y < size; y++)
        {
            target.Plane[target.Offset + (y * target.Stride)] = (ushort)((p.Left(y) + weighted + DcRound) >> DcShift);
        }
    }

    /// <summary>Predicts with an angular mode (8.4.4.2.6).</summary>
    /// <param name="p">The neighbouring samples.</param>
    /// <param name="target">Where the prediction is written.</param>
    /// <param name="block">The block.</param>
    /// <param name="bitDepth">The bit depth of the colour component.</param>
    /// <param name="disableBoundaryFilter">Whether the boundary smoothing of the pure horizontal and vertical modes is off.</param>
    private static void PredictAngular(H265IntraNeighbours p, H265PredictionTarget target, in H265IntraBlock block, int bitDepth, bool disableBoundaryFilter)
    {
        var size = 1 << block.Log2Size;
        var mode = block.Mode;
        var angle = H265IntraTables.AngleOf(mode);
        var vertical = mode >= FirstVerticalMode;
        Span<int> reference = stackalloc int[MaxReference];
        BuildReference(p, reference, size, mode, angle, vertical);
        var origin = size;
        var along = vertical ? 1 : target.Stride;
        var across = vertical ? target.Stride : 1;
        for (var j = 0; j < size; j++)
        {
            var index = ((j + 1) * angle) >> FractionBits;
            var fraction = ((j + 1) * angle) & FractionMask;
            for (var i = 0; i < size; i++)
            {
                var position = origin + i + index + 1;
                var first = reference[position];
                var value = fraction != 0
                    ? (((FractionTotal - fraction) * first) + (fraction * reference[position + 1]) + FractionRound) >> FractionBits
                    : first;
                target.Plane[target.Offset + (j * across) + (i * along)] = (ushort)value;
            }
        }

        if (block.CIdx == 0 && size < BoundarySmoothingBelow && !disableBoundaryFilter)
        {
            SmoothBoundary(p, target, size, mode, bitDepth);
        }
    }

    /// <summary>Builds the main reference line of an angular mode, extended with projected samples for a negative angle (8-53 to 8-63). Entry x is stored at x + size.</summary>
    /// <param name="p">The neighbouring samples.</param>
    /// <param name="reference">Receives the line.</param>
    /// <param name="size">The block side.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="angle">The prediction angle.</param>
    /// <param name="vertical">Whether the mode predicts from the top row.</param>
    private static void BuildReference(H265IntraNeighbours p, Span<int> reference, int size, int mode, int angle, bool vertical)
    {
        var origin = size;
        reference[origin] = p.Corner;
        for (var x = 1; x <= size << 1; x++)
        {
            reference[origin + x] = vertical ? p.Top(x - 1) : p.Left(x - 1);
        }

        var last = (size * angle) >> FractionBits;
        if (angle >= 0 || last >= -1)
        {
            return;
        }

        var inverse = H265IntraTables.InverseAngleOf(mode);
        for (var x = -1; x >= last; x--)
        {
            var projected = -1 + (((x * inverse) + InverseRound) >> InverseShift);
            reference[origin + x] = vertical ? p.Left(projected) : p.Top(projected);
        }
    }

    /// <summary>Smooths the first column of the vertical mode or the first row of the horizontal mode towards the neighbouring samples (8-60 and 8-68).</summary>
    /// <param name="p">The neighbouring samples.</param>
    /// <param name="target">Where the prediction is written.</param>
    /// <param name="size">The block side.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="bitDepth">The bit depth of the colour component.</param>
    private static void SmoothBoundary(H265IntraNeighbours p, H265PredictionTarget target, int size, int mode, int bitDepth)
    {
        var maximum = (1 << bitDepth) - 1;
        if (mode == H265IntraModes.Vertical)
        {
            for (var y = 0; y < size; y++)
            {
                target.Plane[target.Offset + (y * target.Stride)] = (ushort)Math.Clamp(p.Top(0) + ((p.Left(y) - p.Corner) >> 1), 0, maximum);
            }
        }
        else if (mode == H265IntraModes.Horizontal)
        {
            for (var x = 0; x < size; x++)
            {
                target.Plane[target.Offset + x] = (ushort)Math.Clamp(p.Left(0) + ((p.Top(x) - p.Corner) >> 1), 0, maximum);
            }
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The four chroma prediction modes of clause 8.3.4 of ITU-T H.264, for a chroma block of 8x8 samples (4:2:0 video).</summary>
internal static class H264IntraChroma
{
    /// <summary>The DC mode.</summary>
    internal const int Dc = 0;

    /// <summary>The horizontal mode.</summary>
    internal const int Horizontal = 1;

    /// <summary>The vertical mode.</summary>
    internal const int Vertical = 2;

    /// <summary>The plane mode.</summary>
    internal const int Plane = 3;

    /// <summary>The size of the block.</summary>
    private const int Size = 8;

    /// <summary>Half the size of the block.</summary>
    private const int Half = 4;

    /// <summary>The prediction of a block with no neighbours.</summary>
    private const int Mid = 128;

    /// <summary>The largest sample value.</summary>
    private const int MaxSample = 255;

    /// <summary>The 4x4 blocks along each side of the chroma block.</summary>
    private const int Quads = 2;

    /// <summary>The shift of the mean of eight neighbours.</summary>
    private const int BothShift = 3;

    /// <summary>The shift of the plane prediction.</summary>
    private const int PlaneFinalShift = 5;

    /// <summary>The rounding of the mean of four neighbours.</summary>
    private const int OneRound = 2;

    /// <summary>The shift of the mean of four neighbours.</summary>
    private const int OneShift = 2;

    /// <summary>The distance from the last gradient sample to the first one used on the near side.</summary>
    private const int Back = 2;

    /// <summary>The multiplier of the plane gradients for 4:2:0 video.</summary>
    private const int PlaneScale = 34;

    /// <summary>The rounding of the plane gradients.</summary>
    private const int PlaneRound = 32;

    /// <summary>The shift of the plane gradients.</summary>
    private const int PlaneShift = 6;

    /// <summary>The multiplier of the corner samples, and the rounding of the plane prediction.</summary>
    private const int PlaneCorner = 16;

    /// <summary>The distance from the corner of the block to its centre, for the plane prediction.</summary>
    private const int Center = 3;

    /// <summary>Predicts a chroma block into the picture.</summary>
    /// <param name="plane">The chroma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="mode">The prediction mode.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    internal static void Predict(byte[] plane, int offset, int stride, int mode, H264Neighbours neighbours)
    {
        switch (mode)
        {
            case Vertical:
            {
                for (var y = 0; y < Size; y++)
                {
                    Array.Copy(plane, offset - stride, plane, offset + (y * stride), Size);
                }

                break;
            }

            case Horizontal:
            {
                for (var y = 0; y < Size; y++)
                {
                    plane.AsSpan(offset + (y * stride), Size).Fill(plane[offset + (y * stride) - 1]);
                }

                break;
            }

            case Dc:
            {
                PredictDc(plane, offset, stride, neighbours);
                break;
            }

            default:
            {
                PredictPlane(plane, offset, stride);
                break;
            }
        }
    }

    /// <summary>Predicts with the DC mode, which works on each 4x4 block of the chroma block on its own.</summary>
    /// <param name="plane">The chroma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    private static void PredictDc(byte[] plane, int offset, int stride, H264Neighbours neighbours)
    {
        for (var row = 0; row < Quads; row++)
        {
            for (var column = 0; column < Quads; column++)
            {
                PredictQuad(plane, offset, stride, row, column, neighbours);
            }
        }
    }

    /// <summary>Predicts one 4x4 block of the chroma block with the DC mode.</summary>
    /// <param name="plane">The chroma plane.</param>
    /// <param name="offset">The index of the chroma block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="row">The row of the 4x4 block, 0 or 1.</param>
    /// <param name="column">The column of the 4x4 block, 0 or 1.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    private static void PredictQuad(byte[] plane, int offset, int stride, int row, int column, H264Neighbours neighbours)
    {
        var start = offset + (row * Half * stride) + (column * Half);
        var top = 0;
        var left = 0;
        for (var index = 0; index < Half; index++)
        {
            top += neighbours.Top ? plane[start - (row * Half * stride) - stride + index] : 0;
            left += neighbours.Left ? plane[start + (index * stride) - (column * Half) - 1] : 0;
        }

        var value = DcOf(row, column, top, left, neighbours);
        for (var y = 0; y < Half; y++)
        {
            plane.AsSpan(start + (y * stride), Half).Fill((byte)value);
        }
    }

    /// <summary>Chooses the DC value of one 4x4 block of the chroma block.</summary>
    /// <param name="row">The row of the 4x4 block, 0 or 1.</param>
    /// <param name="column">The column of the 4x4 block, 0 or 1.</param>
    /// <param name="top">The sum of the four samples above the chroma block above this column.</param>
    /// <param name="left">The sum of the four samples to the left of the chroma block beside this row.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <returns>The value.</returns>
    private static int DcOf(int row, int column, int top, int left, H264Neighbours neighbours)
    {
        if (row == column && neighbours.Top && neighbours.Left)
        {
            return (top + left + Half) >> BothShift;
        }

        // The block beside the top edge prefers the top samples; the other three prefer the left samples.
        return row == 0 && column == 1 ? OneSided(top, left, neighbours.Top, neighbours.Left) : OneSided(left, top, neighbours.Left, neighbours.Top);
    }

    /// <summary>Takes the mean of one side's samples, or the other's when the first is missing.</summary>
    /// <param name="preferred">The sum of the preferred side's samples.</param>
    /// <param name="other">The sum of the other side's samples.</param>
    /// <param name="preferredAvailable">Whether the preferred side can be used.</param>
    /// <param name="otherAvailable">Whether the other side can be used.</param>
    /// <returns>The value.</returns>
    private static int OneSided(int preferred, int other, bool preferredAvailable, bool otherAvailable)
    {
        if (preferredAvailable)
        {
            return (preferred + OneRound) >> OneShift;
        }

        return otherAvailable ? (other + OneRound) >> OneShift : Mid;
    }

    /// <summary>Predicts with the plane mode.</summary>
    /// <param name="plane">The chroma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    private static void PredictPlane(byte[] plane, int offset, int stride)
    {
        var horizontal = 0;
        var vertical = 0;
        for (var index = 0; index < Half; index++)
        {
            var right = plane[offset - stride + Half + index];
            var leftTop = index == Half - 1 ? plane[offset - stride - 1] : plane[offset - stride + Half - Back - index];
            var bottom = plane[offset + ((Half + index) * stride) - 1];
            var upper = index == Half - 1 ? plane[offset - stride - 1] : plane[offset + ((Half - Back - index) * stride) - 1];
            horizontal += (index + 1) * (right - leftTop);
            vertical += (index + 1) * (bottom - upper);
        }

        var a = PlaneCorner * (plane[offset + ((Size - 1) * stride) - 1] + plane[offset - stride + Size - 1]);
        var b = ((PlaneScale * horizontal) + PlaneRound) >> PlaneShift;
        var c = ((PlaneScale * vertical) + PlaneRound) >> PlaneShift;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var value = (a + (b * (x - Center)) + (c * (y - Center)) + PlaneCorner) >> PlaneFinalShift;
                plane[offset + (y * stride) + x] = (byte)Math.Clamp(value, 0, MaxSample);
            }
        }
    }
}

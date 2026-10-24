// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The four Intra 16x16 prediction modes of clause 8.3.3 of ITU-T H.264, for a luma block of 16x16 samples.</summary>
internal static class H264Intra16x16
{
    /// <summary>The vertical mode.</summary>
    internal const int Vertical = 0;

    /// <summary>The horizontal mode.</summary>
    internal const int Horizontal = 1;

    /// <summary>The DC mode.</summary>
    internal const int Dc = 2;

    /// <summary>The plane mode.</summary>
    internal const int Plane = 3;

    /// <summary>The size of the block.</summary>
    private const int Size = 16;

    /// <summary>The prediction of a block with no neighbours.</summary>
    private const int Mid = 128;

    /// <summary>Half the size of the block.</summary>
    private const int Half = 8;

    /// <summary>The largest sample value.</summary>
    private const int MaxSample = 255;

    /// <summary>The shift of the mean of 32 neighbours, and of the plane prediction.</summary>
    private const int BothShift = 5;

    /// <summary>The shift of the mean of 16 neighbours.</summary>
    private const int OneShift = 4;

    /// <summary>The distance from the last gradient sample to the first one used on the near side.</summary>
    private const int Back = 2;

    /// <summary>The multiplier of the plane gradients.</summary>
    private const int PlaneScale = 5;

    /// <summary>The rounding of the plane gradients.</summary>
    private const int PlaneRound = 32;

    /// <summary>The shift of the plane gradients.</summary>
    private const int PlaneShift = 6;

    /// <summary>The distance from the corner of the block to its centre, for the plane prediction.</summary>
    private const int Center = 7;

    /// <summary>Predicts a 16x16 block into the picture.</summary>
    /// <param name="plane">The luma plane.</param>
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
                var value = DcValue(plane, offset, stride, neighbours);
                for (var y = 0; y < Size; y++)
                {
                    plane.AsSpan(offset + (y * stride), Size).Fill((byte)value);
                }

                break;
            }

            default:
            {
                PlanePredict(plane, offset, stride);
                break;
            }
        }
    }

    /// <summary>Works out the DC prediction.</summary>
    /// <param name="plane">The luma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    /// <param name="neighbours">Which neighbours may be used.</param>
    /// <returns>The value every sample takes.</returns>
    private static int DcValue(byte[] plane, int offset, int stride, H264Neighbours neighbours)
    {
        var top = 0;
        var left = 0;
        for (var index = 0; index < Size; index++)
        {
            top += neighbours.Top ? plane[offset - stride + index] : 0;
            left += neighbours.Left ? plane[offset + (index * stride) - 1] : 0;
        }

        if (neighbours.Top && neighbours.Left)
        {
            return (top + left + Size) >> BothShift;
        }

        if (neighbours.Left)
        {
            return (left + Half) >> OneShift;
        }

        return neighbours.Top ? (top + Half) >> OneShift : Mid;
    }

    /// <summary>Predicts with the plane mode.</summary>
    /// <param name="plane">The luma plane.</param>
    /// <param name="offset">The index of the block's first sample.</param>
    /// <param name="stride">The bytes in a row.</param>
    private static void PlanePredict(byte[] plane, int offset, int stride)
    {
        var horizontal = 0;
        var vertical = 0;
        for (var index = 0; index < Half; index++)
        {
            var topRight = plane[offset - stride + Half + index];
            var topLeft = index == Half - 1 ? plane[offset - stride - 1] : plane[offset - stride + Half - Back - index];
            var bottom = plane[offset + ((Half + index) * stride) - 1];
            var upper = index == Half - 1 ? plane[offset - stride - 1] : plane[offset + ((Half - Back - index) * stride) - 1];
            horizontal += (index + 1) * (topRight - topLeft);
            vertical += (index + 1) * (bottom - upper);
        }

        var a = Size * (plane[offset + ((Size - 1) * stride) - 1] + plane[offset - stride + Size - 1]);
        var b = ((PlaneScale * horizontal) + PlaneRound) >> PlaneShift;
        var c = ((PlaneScale * vertical) + PlaneRound) >> PlaneShift;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var value = (a + (b * (x - Center)) + (c * (y - Center)) + Size) >> BothShift;
                plane[offset + (y * stride) + x] = (byte)Math.Clamp(value, 0, MaxSample);
            }
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Builds and applies alpha planes: stencil coverage, nearest-neighbour resampling and premultiplication.</summary>
internal static class ImageAlpha
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The highest bit of a byte.</summary>
    private const int HighBit = 0x80;

    /// <summary>The bias that makes the divide by 255 round to nearest.</summary>
    private const int RoundingBias = 128;

    /// <summary>Half a pixel, the offset from a pixel's corner to its centre; also the rounding bias of a float to byte.</summary>
    private const float CenterOffset = 0.5F;

    /// <summary>Two, which doubles an index to work in half pixels.</summary>
    private const int PairFactor = 2;

    /// <summary>The index of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>Expands one row of 1-bit stencil samples to coverage bytes.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="coverage">Receives 255 where the mask paints and 0 elsewhere.</param>
    /// <param name="paintOnes">Whether 1 bits paint (an inverted /Decode); otherwise 0 bits paint.</param>
    internal static void ExpandStencilRow(ReadOnlySpan<byte> row, Span<byte> coverage, bool paintOnes)
    {
        var paintBit = paintOnes ? HighBit : 0;
        for (var x = 0; x < coverage.Length; x++)
        {
            var bit = (row[x / ByteBits] << (x % ByteBits)) & HighBit;
            coverage[x] = bit == paintBit ? (byte)PixelConverter.MaxByte : (byte)0;
        }
    }

    /// <summary>
    /// Resamples a plane to another size. An axis that grows reads the source pixel under each target pixel's centre; an
    /// axis that shrinks averages the source pixels the target pixel covers.
    /// </summary>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="target">Receives the resampled plane.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    internal static void ResamplePlane(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var rowLow = RangeStart(y, sourceHeight, height);
            var rowHigh = RangeEnd(y, sourceHeight, height, rowLow);
            for (var x = 0; x < width; x++)
            {
                var columnLow = RangeStart(x, sourceWidth, width);
                var columnHigh = RangeEnd(x, sourceWidth, width, columnLow);
                target[(y * width) + x] = Average(source, sourceWidth, columnLow, columnHigh, rowLow, rowHigh);
            }
        }
    }

    /// <summary>Enlarges a BGRA image with bilinear interpolation between pixel centres.</summary>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="target">Receives the enlarged pixels.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    internal static void UpscaleBgra(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Max(((y + CenterOffset) * sourceHeight / height) - CenterOffset, 0);
            var y0 = Math.Min((int)sourceY, sourceHeight - 1);
            var y1 = Math.Min(y0 + 1, sourceHeight - 1);
            var fractionY = sourceY - y0;
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Max(((x + CenterOffset) * sourceWidth / width) - CenterOffset, 0);
                var x0 = Math.Min((int)sourceX, sourceWidth - 1);
                var x1 = Math.Min(x0 + 1, sourceWidth - 1);
                var fractionX = sourceX - x0;
                for (var c = 0; c < PixelConverter.BytesPerPixel; c++)
                {
                    var top = Lerp(source[(((y0 * sourceWidth) + x0) * PixelConverter.BytesPerPixel) + c], source[(((y0 * sourceWidth) + x1) * PixelConverter.BytesPerPixel) + c], fractionX);
                    var bottom = Lerp(source[(((y1 * sourceWidth) + x0) * PixelConverter.BytesPerPixel) + c], source[(((y1 * sourceWidth) + x1) * PixelConverter.BytesPerPixel) + c], fractionX);
                    target[(((y * width) + x) * PixelConverter.BytesPerPixel) + c] = (byte)(Lerp(top, bottom, fractionY) + CenterOffset);
                }
            }
        }
    }

    /// <summary>
    /// Replaces colours that were blended with a matte colour by the soft mask: c = m + (c' - m) * 255 / alpha (ISO 32000-1
    /// section 11.6.5.3, the Matte entry). Pixels with zero or full alpha are left alone.
    /// </summary>
    /// <param name="bgra">The opaque, pre-blended pixels.</param>
    /// <param name="alpha">One alpha byte per pixel.</param>
    /// <param name="matte">The matte colour.</param>
    internal static void RemoveMatte(Span<byte> bgra, ReadOnlySpan<byte> alpha, MatteColor matte)
    {
        for (var p = 0; p < alpha.Length; p++)
        {
            var a = alpha[p];
            if (a is 0 or PixelConverter.MaxByte)
            {
                continue;
            }

            var pixel = bgra.Slice(p * PixelConverter.BytesPerPixel, PixelConverter.BytesPerPixel);
            pixel[0] = Unblend(pixel[0], matte.Blue, a);
            pixel[1] = Unblend(pixel[1], matte.Green, a);
            pixel[AlphaIndex - 1] = Unblend(pixel[AlphaIndex - 1], matte.Red, a);
        }
    }

    /// <summary>Multiplies premultiplied BGRA pixels by an alpha plane.</summary>
    /// <param name="bgra">The pixels.</param>
    /// <param name="alpha">One alpha byte per pixel.</param>
    internal static void Multiply(Span<byte> bgra, ReadOnlySpan<byte> alpha)
    {
        for (var p = 0; p < alpha.Length; p++)
        {
            var factor = alpha[p];
            if (factor == PixelConverter.MaxByte)
            {
                continue;
            }

            var pixel = bgra.Slice(p * PixelConverter.BytesPerPixel, PixelConverter.BytesPerPixel);
            if (factor == 0)
            {
                pixel.Clear();
                continue;
            }

            for (var i = 0; i <= AlphaIndex; i++)
            {
                pixel[i] = (byte)Divide255(pixel[i] * factor);
            }
        }
    }

    /// <summary>Gets the first source index a target index covers.</summary>
    /// <param name="index">The target index.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="targetSize">The target size.</param>
    /// <returns>The first source index.</returns>
    private static int RangeStart(int index, int sourceSize, int targetSize) => targetSize < sourceSize
        ? (int)((long)index * sourceSize / targetSize)
        : (int)(((PairFactor * index) + 1L) * sourceSize / (PairFactor * (long)targetSize));

    /// <summary>Gets one past the last source index a target index covers.</summary>
    /// <param name="index">The target index.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="targetSize">The target size.</param>
    /// <param name="start">The first source index.</param>
    /// <returns>The end of the covered range, at least one pixel after <paramref name="start"/>.</returns>
    private static int RangeEnd(int index, int sourceSize, int targetSize, int start) => targetSize < sourceSize
        ? Math.Max(start + 1, (int)((long)(index + 1) * sourceSize / targetSize))
        : start + 1;

    /// <summary>Averages a block of a plane.</summary>
    /// <param name="source">The plane.</param>
    /// <param name="stride">The plane width.</param>
    /// <param name="columnLow">The first column.</param>
    /// <param name="columnHigh">One past the last column.</param>
    /// <param name="rowLow">The first row.</param>
    /// <param name="rowHigh">One past the last row.</param>
    /// <returns>The rounded average.</returns>
    private static byte Average(ReadOnlySpan<byte> source, int stride, int columnLow, int columnHigh, int rowLow, int rowHigh)
    {
        var count = (columnHigh - columnLow) * (rowHigh - rowLow);
        if (count == 1)
        {
            return source[(rowLow * stride) + columnLow];
        }

        long sum = 0;
        for (var row = rowLow; row < rowHigh; row++)
        {
            for (var column = columnLow; column < columnHigh; column++)
            {
                sum += source[(row * stride) + column];
            }
        }

        return (byte)((sum + (count / PairFactor)) / count);
    }

    /// <summary>Interpolates between two values.</summary>
    /// <param name="from">The value at fraction 0.</param>
    /// <param name="to">The value at fraction 1.</param>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The interpolated value.</returns>
    private static float Lerp(float from, float to, float fraction) => from + ((to - from) * fraction);

    /// <summary>Recovers a colour channel blended with a matte: m + (c - m) * 255 / alpha, clamped to a byte.</summary>
    /// <param name="blended">The blended channel.</param>
    /// <param name="matte">The matte channel.</param>
    /// <param name="alpha">The alpha, from 1 to 254.</param>
    /// <returns>The recovered channel.</returns>
    private static byte Unblend(byte blended, byte matte, byte alpha) =>
        (byte)Math.Clamp(matte + (((blended - matte) * PixelConverter.MaxByte) / alpha), 0, PixelConverter.MaxByte);

    /// <summary>Divides by 255, rounding to nearest, for products of two bytes.</summary>
    /// <param name="value">The product.</param>
    /// <returns>The quotient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Divide255(int value)
    {
        var biased = value + RoundingBias;
        return (biased + (biased >> ByteBits)) >> ByteBits;
    }
}

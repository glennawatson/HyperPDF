// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Builds and applies alpha planes: stencil coverage, nearest-neighbour resampling and premultiplication.</summary>
internal static class ImageAlpha
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The pixels tested together for being fully opaque.</summary>
    private const int OpaqueBlock = 64;

    /// <summary>The entries in a stencil expansion table: one for each source byte.</summary>
    private const int StencilTableSize = 256;

    /// <summary>The bias that makes the divide by 255 round to nearest.</summary>
    private const int RoundingBias = 128;

    /// <summary>Half a pixel, the offset from a pixel's corner to its centre; also the rounding bias of a float to byte.</summary>
    private const float CenterOffset = 0.5F;

    /// <summary>Two, which doubles an index to work in half pixels.</summary>
    private const int PairFactor = 2;

    /// <summary>The index of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>Expands a source byte to eight coverage bytes where 1 bits paint.</summary>
    private static readonly ulong[] StencilOnes = BuildStencilTable(true);

    /// <summary>Expands a source byte to eight coverage bytes where 0 bits paint.</summary>
    private static readonly ulong[] StencilZeros = BuildStencilTable(false);

    /// <summary>Expands one row of 1-bit stencil samples to coverage bytes.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="coverage">Receives 255 where the mask paints and 0 elsewhere.</param>
    /// <param name="paintOnes">Whether 1 bits paint (an inverted /Decode); otherwise 0 bits paint.</param>
    internal static void ExpandStencilRow(ReadOnlySpan<byte> row, Span<byte> coverage, bool paintOnes)
    {
        // Eight coverage bytes per source byte come from one table read, with the table chosen by which bit value paints.
        var table = paintOnes ? StencilOnes : StencilZeros;
        var whole = coverage.Length / ByteBits;
        for (var i = 0; i < whole; i++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(coverage.Slice(i * ByteBits, ByteBits), table[row[i]]);
        }

        var rest = coverage.Length - (whole * ByteBits);
        if (rest == 0)
        {
            return;
        }

        Span<byte> last = stackalloc byte[ByteBits];
        BinaryPrimitives.WriteUInt64LittleEndian(last, table[row[whole]]);
        last[..rest].CopyTo(coverage[(whole * ByteBits)..]);
    }

    /// <summary>Builds the table that expands a source byte to eight coverage bytes, the first pixel in the lowest byte.</summary>
    /// <param name="paintOnes">Whether 1 bits paint.</param>
    /// <returns>The 256 entries.</returns>
    internal static ulong[] BuildStencilTable(bool paintOnes)
    {
        var table = new ulong[StencilTableSize];
        for (var value = 0; value < table.Length; value++)
        {
            ulong entry = 0;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                var set = ((value >> (ByteBits - 1 - bit)) & 1) == 1;
                if (set == paintOnes)
                {
                    entry |= (ulong)PixelConverter.MaxByte << (bit * ByteBits);
                }
            }

            table[value] = entry;
        }

        return table;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ResamplePlane(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int width, int height) =>
        PlaneResampler.Resample(source, sourceWidth, sourceHeight, target, width, height);

    /// <summary>Enlarges a BGRA image with bilinear interpolation between pixel centres.</summary>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="target">Receives the enlarged pixels.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    internal static void UpscaleBgra(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int width, int height)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            BgraUpscaler.Upscale(source, sourceWidth, sourceHeight, target, width, height);
            return;
        }

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
        // Masks are mostly opaque, so a block of pixels whose alpha is all 255 is skipped with one vectorised test.
        for (var start = 0; start < alpha.Length; start += OpaqueBlock)
        {
            var block = alpha.Slice(start, Math.Min(OpaqueBlock, alpha.Length - start));
            if (block.IndexOfAnyExcept((byte)PixelConverter.MaxByte) >= 0)
            {
                MultiplyBlock(bgra, block, start);
            }
        }
    }

    /// <summary>Multiplies the pixels of one block by their alpha.</summary>
    /// <param name="bgra">The pixels of the whole image.</param>
    /// <param name="block">The alpha of the block.</param>
    /// <param name="start">The index of the block's first pixel.</param>
    internal static void MultiplyBlock(Span<byte> bgra, ReadOnlySpan<byte> block, int start)
    {
        for (var p = 0; p < block.Length; p++)
        {
            var factor = block[p];
            if (factor == PixelConverter.MaxByte)
            {
                continue;
            }

            var pixel = bgra.Slice((start + p) * PixelConverter.BytesPerPixel, PixelConverter.BytesPerPixel);
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

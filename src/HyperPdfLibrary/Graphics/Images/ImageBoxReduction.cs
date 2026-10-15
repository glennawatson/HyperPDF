// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Reduces a decoded image by a power of two with an area (box) average, so every source pixel that an output pixel covers
/// counts equally. Colours are averaged as light (sRGB values are converted to linear light, averaged, and converted back),
/// so a fine black and white pattern reduces to the grey the eye sees rather than a darker one. Coverage masks are averaged
/// as they are, and colours with transparency are weighted by their alpha.
/// </summary>
internal static class ImageBoxReduction
{
    /// <summary>The most halvings one reduction applies.</summary>
    internal const int MaxLevels = 8;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The largest value of a linear light sample: 15 bits.</summary>
    private const int LinearMax = 32_767;

    /// <summary>The divisor that turns a total into a rounding offset.</summary>
    private const int Half = 2;

    /// <summary>The largest 8-bit sample.</summary>
    private const int ByteMax = 255;

    /// <summary>The value that rounds a division by 255.</summary>
    private const int HalfByte = 127;

    /// <summary>The sRGB transfer function's linear segment limit for encoded values.</summary>
    private const double SrgbKnee = 0.04045;

    /// <summary>The sRGB transfer function's linear segment slope.</summary>
    private const double SrgbSlope = 12.92;

    /// <summary>The sRGB transfer function's offset.</summary>
    private const double SrgbOffset = 0.055;

    /// <summary>The sRGB transfer function's gamma.</summary>
    private const double SrgbGamma = 2.4;

    /// <summary>The sRGB transfer function's divisor for the power segment.</summary>
    private const double SrgbScale = 1.055;

    /// <summary>The linear light limit of the sRGB linear segment.</summary>
    private const double LinearKnee = 0.0031308;

    /// <summary>The number of channels the averaging keeps for a BGRA pixel: blue, green, red and alpha.</summary>
    private const int BgraChannels = 4;

    /// <summary>The bits dropped from an alpha-weighted linear sample so block sums fit an int.</summary>
    private const int WeightShift = 4;

    /// <summary>The index of green in a BGRA pixel.</summary>
    private const int GreenIndex = 1;

    /// <summary>The index of red in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The index of alpha in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The linear light values of the 256 sRGB values.</summary>
    private static readonly short[] ToLinear = CreateToLinear();

    /// <summary>The weighted linear light of each sRGB value at full alpha, so an opaque pixel adds one lookup for each channel.</summary>
    private static readonly int[] OpaqueWeighted = CreateOpaqueWeighted();

    /// <summary>The sRGB values of the linear light values from 0 to <see cref="LinearMax"/>.</summary>
    private static readonly byte[] ToSrgb = CreateToSrgb();

    /// <summary>The identity-scaled values of a coverage mask in the same 15 bit range.</summary>
    private static readonly short[] CoverageToLinear = CreateCoverage();

    /// <summary>The coverage of a 15 bit value.</summary>
    private static readonly byte[] CoverageFromLinear = CreateCoverageBack();

    /// <summary>Gets the size one dimension has after a reduction.</summary>
    /// <param name="size">The source size.</param>
    /// <param name="levels">The halvings.</param>
    /// <returns>The reduced size, at least one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ReducedSize(int size, int levels) => Math.Max(1, (size + (1 << levels) - 1) >> levels);

    /// <summary>Reduces an image by a number of halvings.</summary>
    /// <param name="source">The decoded image: gray, a stencil mask or premultiplied BGRA.</param>
    /// <param name="levels">The halvings, so each output pixel covers a square of <c>2^levels</c> source pixels.</param>
    /// <returns>The reduced image, or <paramref name="source"/> when nothing can be reduced.</returns>
    internal static PdfImageData Reduce(PdfImageData source, int levels)
    {
        ArgumentNullException.ThrowIfNull(source);
        var channels = source.IsGray || source.IsStencilMask ? 1 : BgraChannels;
        if (levels <= 0 || source.UnsupportedCodec != PdfImageCodec.None || (long)source.Width * source.Height * channels != source.Pixels.Length)
        {
            return source;
        }

        levels = Math.Min(levels, MaxLevels);
        var step = 1 << levels;
        var width = ReducedSize(source.Width, levels);
        var height = ReducedSize(source.Height, levels);
        var pixels = PixelMemory.AllocateUninitialized((long)width * height * channels, out var pinned);
        var rowBytes = source.Width * channels;
        for (var y = 0; y < height; y++)
        {
            var rows = Math.Min(step, source.Height - (y * step));
            var block = source.Pixels.AsSpan(y * step * rowBytes, rows * rowBytes);
            var output = pixels.AsSpan(y * width * channels, width * channels);
            if (channels == 1)
            {
                ReduceGrayBlock(block, source.Width, rows, step, source.IsStencilMask, output);
            }
            else
            {
                ReduceColorBlock(block, source.Width, rows, step, output);
            }
        }

        return new(width, height, pixels, source.IsStencilMask, source.Interpolate, PdfImageCodec.None) { IsGray = source.IsGray, IsPinned = pinned };
    }

    /// <summary>Averages the source rows of one output row of a premultiplied BGRA image.</summary>
    /// <param name="block">The source rows of this block row, one after another.</param>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="rows">The source rows in the block.</param>
    /// <param name="step">The source pixels across one output pixel.</param>
    /// <param name="output">The output row, fully written.</param>
    internal static void ReduceColorBlock(ReadOnlySpan<byte> block, int width, int rows, int step, Span<byte> output)
    {
        var rowBytes = width * BgraBytes;
        var outputWidth = output.Length / BgraBytes;
        for (var i = 0; i < outputWidth; i++)
        {
            var first = i * step * BgraBytes;
            var columns = Math.Min(step, width - (i * step));
            var sums = default(BlockSums);
            for (var row = 0; row < rows; row++)
            {
                AddRun(block.Slice((row * rowBytes) + first, columns * BgraBytes), ref sums);
            }

            WritePixel(sums, columns * rows, output.Slice(i * BgraBytes, BgraBytes));
        }
    }

    /// <summary>Averages the source rows of one output row of a gray or coverage image.</summary>
    /// <param name="block">The source rows of this block row, one after another.</param>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="rows">The source rows in the block.</param>
    /// <param name="step">The source pixels across one output pixel.</param>
    /// <param name="isCoverage">Whether the samples are stencil coverage, which is averaged without the sRGB curve.</param>
    /// <param name="output">The output row, fully written.</param>
    private static void ReduceGrayBlock(ReadOnlySpan<byte> block, int width, int rows, int step, bool isCoverage, Span<byte> output)
    {
        var toLinear = isCoverage ? CoverageToLinear : ToLinear;
        var fromLinear = isCoverage ? CoverageFromLinear : ToSrgb;
        for (var i = 0; i < output.Length; i++)
        {
            var first = i * step;
            var columns = Math.Min(step, width - first);
            var sum = 0;
            for (var row = 0; row < rows; row++)
            {
                var run = block.Slice((row * width) + first, columns);
                for (var x = 0; x < run.Length; x++)
                {
                    sum += toLinear[run[x]];
                }
            }

            output[i] = fromLinear[sum / (columns * rows)];
        }
    }

    /// <summary>Adds the premultiplied pixels of one run to the sums, weighting each straight colour by its alpha.</summary>
    /// <param name="run">The BGRA pixels of one row of a block.</param>
    /// <param name="sums">The sums to add to.</param>
    private static void AddRun(ReadOnlySpan<byte> run, ref BlockSums sums)
    {
        var (blue, green, red, alphaSum) = sums;
        var opaque = OpaqueWeighted;
        for (var p = 0; p < run.Length; p += BgraBytes)
        {
            int alpha = run[p + AlphaIndex];
            if (alpha == ByteMax)
            {
                blue += opaque[run[p]];
                green += opaque[run[p + GreenIndex]];
                red += opaque[run[p + RedIndex]];
                alphaSum += ByteMax;
            }
            else if (alpha != 0)
            {
                alphaSum += alpha;
                blue += Weighted(run[p], alpha);
                green += Weighted(run[p + GreenIndex], alpha);
                red += Weighted(run[p + RedIndex], alpha);
            }
        }

        sums = new(blue, green, red, alphaSum);
    }

    /// <summary>Gets the linear light of a premultiplied sample weighted by its pixel's alpha.</summary>
    /// <param name="premultiplied">The premultiplied channel value.</param>
    /// <param name="alpha">The pixel's alpha, from 1 to 254.</param>
    /// <returns>The weighted linear value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Weighted(int premultiplied, int alpha)
    {
        var straight = Math.Min(ByteMax, ((premultiplied * ByteMax) + (alpha / Half)) / alpha);

        // The linear value is at most 15 bits and alpha 8 bits; dropping the low bits of the product keeps a sum over a
        // large block inside an int.
        return (ToLinear[straight] * alpha) >> WeightShift;
    }

    /// <summary>Writes the average of a block as a premultiplied pixel.</summary>
    /// <param name="sums">The weighted sums of the block.</param>
    /// <param name="count">The pixels in the block.</param>
    /// <param name="pixel">The output pixel.</param>
    private static void WritePixel(in BlockSums sums, int count, Span<byte> pixel)
    {
        if (sums.Alpha == 0)
        {
            pixel.Clear();
            return;
        }

        var alpha = (sums.Alpha + (count / Half)) / count;
        pixel[0] = Channel(sums.Blue, sums.Alpha, alpha);
        pixel[GreenIndex] = Channel(sums.Green, sums.Alpha, alpha);
        pixel[RedIndex] = Channel(sums.Red, sums.Alpha, alpha);
        pixel[AlphaIndex] = (byte)alpha;
    }

    /// <summary>Converts one weighted linear sum to a premultiplied sRGB channel.</summary>
    /// <param name="sum">The weighted linear sum.</param>
    /// <param name="alphaSum">The alpha sum of the block.</param>
    /// <param name="alpha">The average alpha.</param>
    /// <returns>The channel value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Channel(int sum, int alphaSum, int alpha)
    {
        var linear = (int)Math.Min(LinearMax, ((long)sum << WeightShift) / alphaSum);
        return (byte)(((ToSrgb[linear] * alpha) + HalfByte) / ByteMax);
    }

    /// <summary>Creates the weighted linear light of each sRGB value at full alpha.</summary>
    /// <returns>256 values.</returns>
    private static int[] CreateOpaqueWeighted()
    {
        var table = new int[ByteMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (ToLinear[i] * ByteMax) >> WeightShift;
        }

        return table;
    }

    /// <summary>Creates the sRGB to linear light table.</summary>
    /// <returns>256 values from 0 to <see cref="LinearMax"/>.</returns>
    private static short[] CreateToLinear()
    {
        var table = new short[ByteMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            var encoded = i / (double)ByteMax;
            var linear = encoded <= SrgbKnee ? encoded / SrgbSlope : Math.Pow((encoded + SrgbOffset) / SrgbScale, SrgbGamma);
            table[i] = (short)Math.Round(linear * LinearMax);
        }

        return table;
    }

    /// <summary>Creates the linear light to sRGB table.</summary>
    /// <returns>One value for each linear light step.</returns>
    private static byte[] CreateToSrgb()
    {
        var table = new byte[LinearMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            var linear = i / (double)LinearMax;
            var encoded = linear <= LinearKnee ? linear * SrgbSlope : (SrgbScale * Math.Pow(linear, 1 / SrgbGamma)) - SrgbOffset;
            table[i] = (byte)Math.Clamp((int)Math.Round(encoded * ByteMax), 0, ByteMax);
        }

        return table;
    }

    /// <summary>Creates the table that spreads coverage over the 15 bit range.</summary>
    /// <returns>256 values.</returns>
    private static short[] CreateCoverage()
    {
        var table = new short[ByteMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (short)Math.Round(i * (double)LinearMax / ByteMax);
        }

        return table;
    }

    /// <summary>Creates the table that brings a 15 bit coverage back to a byte.</summary>
    /// <returns>One value for each step.</returns>
    private static byte[] CreateCoverageBack()
    {
        var table = new byte[LinearMax + 1];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = (byte)Math.Round(i * (double)ByteMax / LinearMax);
        }

        return table;
    }

    /// <summary>The running sums of one output pixel's block.</summary>
    /// <param name="Blue">The alpha-weighted linear blue sum.</param>
    /// <param name="Green">The alpha-weighted linear green sum.</param>
    /// <param name="Red">The alpha-weighted linear red sum.</param>
    /// <param name="Alpha">The alpha sum.</param>
    private readonly record struct BlockSums(int Blue, int Green, int Red, int Alpha);
}

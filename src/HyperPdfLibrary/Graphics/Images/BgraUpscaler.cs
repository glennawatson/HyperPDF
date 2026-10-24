// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Enlarges a BGRA image with bilinear interpolation between pixel centres, a pixel at a time: the four channels sit in
/// one vector, and the source columns and their weights are worked out once instead of once for every row.
/// </summary>
internal static class BgraUpscaler
{
    /// <summary>Half a pixel, the offset from a pixel's corner to its centre; also the rounding bias of a float to byte.</summary>
    private const float CenterOffset = 0.5F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The offsets kept for each target column: the left source column and the right.</summary>
    private const int ColumnPair = 2;

    /// <summary>Enlarges an image; the result matches interpolating each channel separately.</summary>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="target">Receives the enlarged pixels.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    internal static void Upscale(ReadOnlySpan<byte> source, int sourceWidth, int sourceHeight, Span<byte> target, int width, int height)
    {
        var offsets = ArrayPool<int>.Shared.Rent(width * ColumnPair);
        var fractions = ArrayPool<float>.Shared.Rent(width);
        try
        {
            MapColumns(sourceWidth, width, offsets, fractions);
            for (var y = 0; y < height; y++)
            {
                var sourceY = Math.Max(((y + CenterOffset) * sourceHeight / height) - CenterOffset, 0);
                var y0 = Math.Min((int)sourceY, sourceHeight - 1);
                var y1 = Math.Min(y0 + 1, sourceHeight - 1);
                UpscaleRow(
                    source.Slice(y0 * sourceWidth * PixelBytes, sourceWidth * PixelBytes),
                    source.Slice(y1 * sourceWidth * PixelBytes, sourceWidth * PixelBytes),
                    target.Slice(y * width * PixelBytes, width * PixelBytes),
                    offsets,
                    fractions,
                    Vector128.Create(sourceY - y0));
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(offsets);
            ArrayPool<float>.Shared.Return(fractions);
        }
    }

    /// <summary>Finds the two source columns and the weight between them for each target column.</summary>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="width">The target width.</param>
    /// <param name="offsets">Receives the byte offsets of the left and right columns, two for each target column.</param>
    /// <param name="fractions">Receives the weight of the right column.</param>
    private static void MapColumns(int sourceWidth, int width, int[] offsets, float[] fractions)
    {
        for (var x = 0; x < width; x++)
        {
            var sourceX = Math.Max(((x + CenterOffset) * sourceWidth / width) - CenterOffset, 0);
            var x0 = Math.Min((int)sourceX, sourceWidth - 1);
            offsets[x * ColumnPair] = x0 * PixelBytes;
            offsets[(x * ColumnPair) + 1] = Math.Min(x0 + 1, sourceWidth - 1) * PixelBytes;
            fractions[x] = sourceX - x0;
        }
    }

    /// <summary>Interpolates one row of the target from two rows of the source.</summary>
    /// <param name="upper">The source row above the target row's centre.</param>
    /// <param name="lower">The source row below.</param>
    /// <param name="row">Receives the target row.</param>
    /// <param name="offsets">The byte offsets of the source columns.</param>
    /// <param name="fractions">The weights between the columns.</param>
    /// <param name="fractionY">The weight of the lower row, in every lane.</param>
    private static void UpscaleRow(ReadOnlySpan<byte> upper, ReadOnlySpan<byte> lower, Span<byte> row, int[] offsets, float[] fractions, Vector128<float> fractionY)
    {
        ref var upperStart = ref MemoryMarshal.GetReference(upper);
        ref var lowerStart = ref MemoryMarshal.GetReference(lower);
        ref var output = ref MemoryMarshal.GetReference(row);
        var half = Vector128.Create(CenterOffset);
        for (var x = 0; x < row.Length / PixelBytes; x++)
        {
            var left = offsets[x * ColumnPair];
            var right = offsets[(x * ColumnPair) + 1];
            var fractionX = Vector128.Create(fractions[x]);
            var top = Lerp(Load(ref upperStart, left), Load(ref upperStart, right), fractionX);
            var bottom = Lerp(Load(ref lowerStart, left), Load(ref lowerStart, right), fractionX);
            PixelLanes.Store(ref Unsafe.Add(ref output, x * PixelBytes), Vector128.ConvertToInt32(Lerp(top, bottom, fractionY) + half), PixelBytes);
        }
    }

    /// <summary>Reads a BGRA pixel as four floats.</summary>
    /// <param name="start">The start of the row.</param>
    /// <param name="offset">The byte offset of the pixel.</param>
    /// <returns>The channels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Load(ref byte start, int offset) => Vector128.ConvertToSingle(PixelLanes.Widen(ref Unsafe.Add(ref start, offset)));

    /// <summary>Interpolates between two values in every lane.</summary>
    /// <param name="from">The values at fraction 0.</param>
    /// <param name="to">The values at fraction 1.</param>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The interpolated values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Lerp(Vector128<float> from, Vector128<float> to, Vector128<float> fraction) => from + ((to - from) * fraction);
}

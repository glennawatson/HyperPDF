// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Decodes an opaque colour image straight to its area-reduced size. Each block of source rows is converted to BGRA in a small
/// reused buffer and averaged by <see cref="ImageBoxReduction"/>, so the full-size pixel array is never allocated, cleared or
/// written, and the result equals decoding in full and then reducing.
/// </summary>
internal static class ReducedColorDecoder
{
    /// <summary>Decodes rows of samples to BGRA through the colour space and averages them down by the header's halvings.</summary>
    /// <typeparam name="TRows">The row source.</typeparam>
    /// <param name="header">The image header; <see cref="ImageHeader.BoxLevels"/> is above zero.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="space">The colour space.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The reduced image.</returns>
    internal static PdfImageData Decode<TRows>(in ImageHeader header, TRows rows, PdfColorSpace space, in SampleUnpacker unpacker, ScratchPools scratchPools)
        where TRows : IImageRows, allows ref struct
    {
        var levels = Math.Min(header.BoxLevels, ImageBoxReduction.MaxLevels);
        var step = 1 << levels;
        var width = header.Width;
        var rowBytes = width * PixelConverter.BytesPerPixel;
        var outputWidth = ImageBoxReduction.ReducedSize(width, levels);
        var outputHeight = ImageBoxReduction.ReducedSize(header.Height, levels);
        var outputRowBytes = outputWidth * PixelConverter.BytesPerPixel;
        var pixels = PixelMemory.AllocateUninitialized((long)outputRowBytes * outputHeight, out var pinned);
        var pool = scratchPools.Get<byte>();
        var samples = pool.Rent(width * space.Components);
        var rowBuffer = rows.UsesBuffer ? pool.Rent(header.RowBytes) : null;
        var block = pool.Rent(Math.Min(step, header.Height) * rowBytes);

        // A colour key makes pixels clear from their source samples, so each row gets its own alpha as it is unpacked.
        var keyAlpha = unpacker.HasColorKey ? pool.Rent(width) : null;
        try
        {
            for (var y = 0; y < outputHeight; y++)
            {
                var first = y * step;
                var count = Math.Min(step, header.Height - first);
                for (var r = 0; r < count; r++)
                {
                    var row = rows.GetRow(first + r, rowBuffer is null ? [] : rowBuffer.AsSpan(0, header.RowBytes));
                    var rowAlpha = keyAlpha is null ? [] : keyAlpha.AsSpan(0, width);
                    if (!unpacker.IsDirect)
                    {
                        unpacker.Unpack(row, samples, rowAlpha, width);
                        row = samples;
                    }

                    var converted = block.AsSpan(r * rowBytes, rowBytes);
                    space.ConvertRow(row, converted, width);
                    ApplyAlpha(converted, rowAlpha, header, first + r);
                }

                ImageBoxReduction.ReduceColorBlock(block.AsSpan(0, count * rowBytes), width, count, step, pixels.AsSpan(y * outputRowBytes, outputRowBytes));
            }

            return new(outputWidth, outputHeight, pixels, false, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned, IsBoxReduced = true };
        }
        finally
        {
            pool.Return(samples);
            pool.Return(block);
            ReturnIfRented(pool, rowBuffer);
            ReturnIfRented(pool, keyAlpha);
        }
    }

    /// <summary>Gives back a buffer that was only rented when the image needed it.</summary>
    /// <param name="pool">The pool the buffer came from.</param>
    /// <param name="buffer">The buffer, or <see langword="null"/> when none was rented.</param>
    private static void ReturnIfRented(ScratchPool<byte> pool, byte[]? buffer)
    {
        if (buffer is not null)
        {
            pool.Return(buffer);
        }
    }

    /// <summary>
    /// Applies the alpha of one converted row in the order a full decode does: the colour key first, then the image's mask
    /// as <see cref="ImageMasks.Apply"/> does to a whole image.
    /// </summary>
    /// <param name="row">The opaque BGRA pixels of the row.</param>
    /// <param name="keyAlpha">The colour key's alpha for the row, or empty when the image has no colour key.</param>
    /// <param name="header">The image header, which holds the mask's alpha plane and matte colour.</param>
    /// <param name="y">The row's index in the image.</param>
    private static void ApplyAlpha(Span<byte> row, ReadOnlySpan<byte> keyAlpha, in ImageHeader header, int y)
    {
        if (!keyAlpha.IsEmpty)
        {
            ImageAlpha.Multiply(row, keyAlpha);
        }

        if (header.AlphaPlane is not { } plane)
        {
            return;
        }

        var alpha = plane.AsSpan(y * header.Width, header.Width);
        if (header.Matte is { } color)
        {
            ImageAlpha.RemoveMatte(row, alpha, color);
        }

        ImageAlpha.Multiply(row, alpha);
    }
}

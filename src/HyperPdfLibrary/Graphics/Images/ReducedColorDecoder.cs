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
        try
        {
            for (var y = 0; y < outputHeight; y++)
            {
                var first = y * step;
                var count = Math.Min(step, header.Height - first);
                for (var r = 0; r < count; r++)
                {
                    var row = rows.GetRow(first + r, rowBuffer is null ? [] : rowBuffer.AsSpan(0, header.RowBytes));
                    if (!unpacker.IsDirect)
                    {
                        unpacker.Unpack(row, samples, [], width);
                        row = samples;
                    }

                    space.ConvertRow(row, block.AsSpan(r * rowBytes, rowBytes), width);
                }

                ImageBoxReduction.ReduceColorBlock(block.AsSpan(0, count * rowBytes), width, count, step, pixels.AsSpan(y * outputRowBytes, outputRowBytes));
            }

            return new(outputWidth, outputHeight, pixels, false, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned, IsBoxReduced = true };
        }
        finally
        {
            pool.Return(samples);
            if (rowBuffer is not null)
            {
                pool.Return(rowBuffer);
            }

            pool.Return(block);
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Tests that the optimizer's pixel conversions and block averaging match a plain per-pixel reference at every size around the vector step.</summary>
public sealed class ImagePixelsConversionTests
{
    /// <summary>The bytes of an RGB pixel.</summary>
    private const int Rgb = 3;

    /// <summary>The bytes of an RGBX pixel.</summary>
    private const int Rgbx = 4;

    /// <summary>The largest pixel count tried one by one.</summary>
    private const int MaxPixels = 40;

    /// <summary>A pixel count well past the vector steps.</summary>
    private const int ManyPixels = 1003;

    /// <summary>The width of the image averaged in blocks; a multiple of the block side, so the reduced size is the target and no resample runs.</summary>
    private const int BlockWidth = 40;

    /// <summary>The height of the image averaged in blocks; a multiple of the block side.</summary>
    private const int BlockHeight = 32;

    /// <summary>The side of the averaged blocks.</summary>
    private const int BlockSide = 4;

    /// <summary>Verifies RGB widens to RGBX with an opaque fourth byte, for every pixel count around the vector step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidensRgbToOpaqueRgbx()
    {
        for (var count = 0; count <= MaxPixels; count++)
        {
            await AssertWidens(count);
        }

        await AssertWidens(ManyPixels);
    }

    /// <summary>Verifies RGBX narrows to RGB, for every pixel count around the vector step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NarrowsRgbxToRgb()
    {
        for (var count = 0; count <= MaxPixels; count++)
        {
            await AssertNarrows(count);
        }

        await AssertNarrows(ManyPixels);
    }

    /// <summary>Verifies block averaging rounds each channel as the plain sum divided by the count of pixels in the block does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AveragesBlocksOfBothLayouts()
    {
        await AssertBlocks(1);
        await AssertBlocks(Rgb);
    }

    /// <summary>Checks one widening.</summary>
    /// <param name="count">The pixels.</param>
    /// <returns>A task.</returns>
    private static async Task AssertWidens(int count)
    {
        var samples = Random(count * Rgb);
        var expected = new byte[count * Rgbx];
        for (var i = 0; i < count; i++)
        {
            samples.AsSpan(i * Rgb, Rgb).CopyTo(expected.AsSpan(i * Rgbx));
            expected[(i * Rgbx) + Rgb] = byte.MaxValue;
        }

        await Assert.That(Widen(samples, count)).IsEquivalentTo(expected);
    }

    /// <summary>Widens samples with the optimizer's conversion.</summary>
    /// <param name="samples">The RGB samples.</param>
    /// <param name="count">The pixels.</param>
    /// <returns>The RGBX pixels.</returns>
    private static byte[] Widen(byte[] samples, int count)
    {
        var pixels = default(PooledBuffer);
        try
        {
            ImagePixels.FromSamples(samples, Rgb, count, ref pixels);
            return pixels.WrittenSpan.ToArray();
        }
        finally
        {
            pixels.Dispose();
        }
    }

    /// <summary>Checks one narrowing.</summary>
    /// <param name="count">The pixels.</param>
    /// <returns>A task.</returns>
    private static async Task AssertNarrows(int count)
    {
        var pixels = Random(count * Rgbx);
        var expected = new byte[count * Rgb];
        for (var i = 0; i < count; i++)
        {
            pixels.AsSpan(i * Rgbx, Rgb).CopyTo(expected.AsSpan(i * Rgb));
        }

        await Assert.That(Narrow(pixels, count)).IsEquivalentTo(expected);
    }

    /// <summary>Narrows pixels with the optimizer's conversion.</summary>
    /// <param name="pixels">The RGBX pixels.</param>
    /// <param name="count">The pixels.</param>
    /// <returns>The RGB samples.</returns>
    private static byte[] Narrow(byte[] pixels, int count)
    {
        var samples = default(PooledBuffer);
        try
        {
            ImagePixels.ToSamples(pixels, Rgb, count, ref samples);
            return samples.WrittenSpan.ToArray();
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>Checks block averaging of an image.</summary>
    /// <param name="components">One for grey, three for RGBX.</param>
    /// <returns>A task.</returns>
    private static async Task AssertBlocks(int components)
    {
        var bytes = ImagePixels.PixelBytes(components);
        var source = Random(BlockWidth * BlockHeight * bytes);
        const int width = (BlockWidth + BlockSide - 1) / BlockSide;
        const int height = (BlockHeight + BlockSide - 1) / BlockSide;
        var expected = new byte[width * height * bytes];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                AverageBlock(source, bytes, x, y, expected.AsSpan(((y * width) + x) * bytes, bytes));
            }
        }

        await Assert.That(Reduce(source, components, width, height)).IsEquivalentTo(expected);
    }

    /// <summary>Reduces an image by whole blocks with the optimizer's conversion.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="components">One for grey, three for RGBX.</param>
    /// <param name="width">The reduced width.</param>
    /// <param name="height">The reduced height.</param>
    /// <returns>The reduced pixels.</returns>
    private static byte[] Reduce(byte[] source, int components, int width, int height)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = ImagePixels.Downsample(null!, source, new(BlockWidth, BlockHeight, components), width, height, ref output);
            return output.WrittenSpan.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Averages one block the plain way.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="bytes">The bytes of a pixel.</param>
    /// <param name="x">The block column.</param>
    /// <param name="y">The block row.</param>
    /// <param name="target">Receives the averaged pixel.</param>
    private static void AverageBlock(byte[] source, int bytes, int x, int y, Span<byte> target)
    {
        var left = x * BlockSide;
        var top = y * BlockSide;
        var right = Math.Min(left + BlockSide, BlockWidth);
        var bottom = Math.Min(top + BlockSide, BlockHeight);
        var count = (right - left) * (bottom - top);
        for (var channel = 0; channel < bytes; channel++)
        {
            var sum = 0;
            for (var row = top; row < bottom; row++)
            {
                for (var column = left; column < right; column++)
                {
                    sum += source[(((row * BlockWidth) + column) * bytes) + channel];
                }
            }

            target[channel] = (byte)((sum + (count >> 1)) / count);
        }
    }

    /// <summary>Makes random bytes.</summary>
    /// <param name="length">How many.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Random(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }
}

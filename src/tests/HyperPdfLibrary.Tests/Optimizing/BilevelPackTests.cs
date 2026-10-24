// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks that packing and unpacking 1-bit rows gives what a plain bit by bit version gives, for widths either side of the vector size.</summary>
public sealed class BilevelPackTests
{
    /// <summary>The seed of the random samples, so a failure can be repeated.</summary>
    private const int Seed = 20_261_025;

    /// <summary>The height of the test images.</summary>
    private const int Height = 5;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of a pixel's bit index within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>The sample value from which a pixel is white.</summary>
    private const int WhiteFrom = 128;

    /// <summary>The value of a white sample.</summary>
    private const byte White = 0xFF;

    /// <summary>The number of values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>Checks packing at widths that leave no tail, a short tail and a tail of a few bytes.</summary>
    /// <param name="width">The width.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(15)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(31)]
    [Arguments(32)]
    [Arguments(33)]
    [Arguments(200)]
    public async Task Pack_MatchesTheBitByBitVersion(int width)
    {
        var samples = RandomSamples(width);
        await Assert.That(PackWithEncoder(samples, width)).IsEquivalentTo(PackPlainly(samples, width));
    }

    /// <summary>Checks unpacking at the same widths.</summary>
    /// <param name="width">The width.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(7)]
    [Arguments(8)]
    [Arguments(15)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(31)]
    [Arguments(32)]
    [Arguments(33)]
    [Arguments(200)]
    public async Task Unpack_MatchesTheBitByBitVersion(int width)
    {
        var stride = (width + BitMask) / ByteBits;
        var random = new SeededRandom(Seed);
        var rows = new byte[stride * Height];
        for (var index = 0; index < rows.Length; index++)
        {
            rows[index] = (byte)random.Next(ByteValues);
        }

        await Assert.That(UnpackWithEncoder(rows, width)).IsEquivalentTo(UnpackPlainly(rows, width));
    }

    /// <summary>Packs with the encoder under test.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="width">The width.</param>
    /// <returns>The packed rows.</returns>
    private static byte[] PackWithEncoder(byte[] samples, int width)
    {
        var packed = default(PooledBuffer);
        try
        {
            BilevelEncoder.Pack(samples, width, Height, ref packed);
            return packed.ToArray();
        }
        finally
        {
            packed.Dispose();
        }
    }

    /// <summary>Unpacks with the encoder under test.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="width">The width.</param>
    /// <returns>The samples.</returns>
    private static byte[] UnpackWithEncoder(byte[] rows, int width)
    {
        var samples = default(PooledBuffer);
        try
        {
            BilevelEncoder.Unpack(rows, width, Height, ref samples);
            return samples.ToArray();
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>Makes random samples, a mix of black, white and greys either side of the threshold.</summary>
    /// <param name="width">The width.</param>
    /// <returns>The samples.</returns>
    private static byte[] RandomSamples(int width)
    {
        var random = new SeededRandom(Seed);
        var samples = new byte[width * Height];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (byte)random.Next(ByteValues);
        }

        return samples;
    }

    /// <summary>Packs one pixel at a time.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="width">The width.</param>
    /// <returns>The packed rows.</returns>
    private static byte[] PackPlainly(byte[] samples, int width)
    {
        var stride = (width + BitMask) / ByteBits;
        var packed = new byte[stride * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (samples[(y * width) + x] >= WhiteFrom)
                {
                    packed[(y * stride) + (x / ByteBits)] |= (byte)(1 << (BitMask - (x % ByteBits)));
                }
            }
        }

        return packed;
    }

    /// <summary>Unpacks one pixel at a time.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <param name="width">The width.</param>
    /// <returns>The samples.</returns>
    private static byte[] UnpackPlainly(byte[] rows, int width)
    {
        var stride = (width + BitMask) / ByteBits;
        var samples = new byte[width * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var set = (rows[(y * stride) + (x / ByteBits)] & (1 << (BitMask - (x % ByteBits)))) != 0;
                samples[(y * width) + x] = set ? White : (byte)0;
            }
        }

        return samples;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>Checks <see cref="SfntChecksum"/> against the definition: the sum of big-endian words, the last padded with zeros.</summary>
public sealed class SfntChecksumTests
{
    /// <summary>The bytes in a checksum word.</summary>
    private const int WordBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The longest length checked one by one, enough to cover the vector body, its tail and the padded word.</summary>
    private const int LongestShort = 100;

    /// <summary>A length that is not a multiple of a vector or a word.</summary>
    private const int LargeLength = 100_003;

    /// <summary>A prime that scatters the byte positions into varied values.</summary>
    private const uint Scatter = 2_654_435_761;

    /// <summary>The shift that mixes the high bits of the scattered position down.</summary>
    private const int MixShift = 15;

    /// <summary>Every length up to a hundred gives the checksum the definition gives.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortLengthsMatchTheDefinition()
    {
        var data = Pattern(LongestShort);
        for (var length = 0; length <= LongestShort; length++)
        {
            await Assert.That(SfntChecksum.Compute(data.AsSpan(0, length))).IsEqualTo(Reference(data.AsSpan(0, length)));
        }
    }

    /// <summary>A large table whose sum wraps many times still matches the definition.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeTableMatchesTheDefinition()
    {
        var data = Pattern(LargeLength);
        Array.Fill(data, byte.MaxValue, 0, data.Length / WordBytes);
        await Assert.That(SfntChecksum.Compute(data)).IsEqualTo(Reference(data));
    }

    /// <summary>A start that is not aligned to a vector gives the same sum.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnalignedStartMatchesTheDefinition()
    {
        var data = Pattern(LongestShort + 1);
        await Assert.That(SfntChecksum.Compute(data.AsSpan(1))).IsEqualTo(Reference(data.AsSpan(1)));
    }

    /// <summary>Makes bytes that vary from position to position.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Pattern(int length)
    {
        var data = new byte[length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(((uint)i * Scatter) >> MixShift);
        }

        return data;
    }

    /// <summary>Adds up big-endian words one byte at a time, as the definition says.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The checksum.</returns>
    private static uint Reference(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (var i = 0; i < data.Length; i++)
        {
            sum += (uint)data[i] << (ByteBits * (WordBytes - 1 - (i % WordBytes)));
        }

        return sum;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Graphics.Shadings;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests that <see cref="MeshBitReader"/> reads the same fields as a reader that takes one bit at a time, including past the end of the data.</summary>
public sealed class MeshBitReaderTests
{
    /// <summary>The starting state of the number sequence.</summary>
    private const uint Seed = 362_436_069;

    /// <summary>The data sets compared.</summary>
    private const int DataSets = 400;

    /// <summary>The longest data, in bytes.</summary>
    private const int LongestData = 40;

    /// <summary>The reads made from each data set.</summary>
    private const int ReadsPerSet = 30;

    /// <summary>The widest field plus the values just outside the allowed range.</summary>
    private const int WidestRequest = 35;

    /// <summary>The values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>The chance, out of this many, that a read is followed by a skip to the next byte.</summary>
    private const int AlignOdds = 4;

    /// <summary>Verifies fields of every width, at every bit offset, read the same as one bit at a time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsMatchTheBitByBitReader()
    {
        var numbers = new NumberSequence(Seed);
        var differences = 0;
        for (var set = 0; set < DataSets; set++)
        {
            var data = new byte[numbers.Next(LongestData + 1)];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)numbers.Next(ByteValues);
            }

            differences += Differs(data, numbers) ? 1 : 0;
        }

        await Assert.That(differences).IsEqualTo(0);
    }

    /// <summary>Reads the same widths from the reader and the reference and compares what they return and how many bits are left.</summary>
    /// <param name="data">The data.</param>
    /// <param name="numbers">The numbers that choose the widths.</param>
    /// <returns>Whether they differ.</returns>
    private static bool Differs(byte[] data, NumberSequence numbers)
    {
        var reader = new MeshBitReader(data);
        var reference = new BitByBitReader(data);
        for (var read = 0; read < ReadsPerSet; read++)
        {
            var bits = numbers.Next(WidestRequest);
            if (reader.Read(bits) != reference.Read(bits) || reader.Remaining != reference.Remaining)
            {
                return true;
            }

            if (numbers.Next(AlignOdds) != 0)
            {
                continue;
            }

            reader.Align();
            reference.Align();
        }

        return false;
    }

    /// <summary>The reader as it was, taking one bit at a time.</summary>
    /// <param name="data">The data.</param>
    private sealed class BitByBitReader(byte[] data)
    {
        /// <summary>The bits in a byte.</summary>
        private const int ByteBits = 8;

        /// <summary>The widest field a read returns.</summary>
        private const int MaxBits = 32;

        /// <summary>The position of the next bit.</summary>
        private long _position;

        /// <summary>Gets the bits left.</summary>
        internal long Remaining => ((long)data.Length * ByteBits) - _position;

        /// <summary>Reads a field.</summary>
        /// <param name="bits">The width, from 0 to 32.</param>
        /// <returns>The value; bits past the end read as zero.</returns>
        internal uint Read(int bits)
        {
            if (bits is <= 0 or > MaxBits)
            {
                return 0;
            }

            ulong value = 0;
            for (var i = 0; i < bits; i++)
            {
                var index = (int)(_position >> 3);
                var bit = index < data.Length ? (data[index] >> (ByteBits - 1 - (int)(_position & (ByteBits - 1)))) & 1 : 0;
                value = (value << 1) | (uint)bit;
                _position++;
            }

            return (uint)value;
        }

        /// <summary>Skips to the next byte boundary.</summary>
        internal void Align() => _position = (_position + ByteBits - 1) / ByteBits * ByteBits;
    }
}

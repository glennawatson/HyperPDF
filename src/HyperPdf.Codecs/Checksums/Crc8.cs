// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Checksums;

/// <summary>The 8 bit cyclic redundancy check with the polynomial x^8 + x^2 + x + 1, as FLAC uses for its frame headers.</summary>
public static class Crc8
{
    /// <summary>The polynomial without its top bit.</summary>
    private const byte Polynomial = 0x07;

    /// <summary>The number of values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The top bit of a byte.</summary>
    private const int TopBit = 0x80;

    /// <summary>The check of every byte value, which is the remainder of dividing that byte by the polynomial.</summary>
    private static readonly byte[] Table = BuildTable();

    /// <summary>Computes the check of some bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The check, which is 0 when the data ends with its own check.</returns>
    public static byte Compute(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (var value in data)
        {
            crc = Table[crc ^ value];
        }

        return crc;
    }

    /// <summary>Builds the table.</summary>
    /// <returns>The remainders.</returns>
    private static byte[] BuildTable()
    {
        var table = new byte[ByteValues];
        for (var value = 0; value < table.Length; value++)
        {
            var remainder = value;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                remainder = (remainder & TopBit) != 0 ? (remainder << 1) ^ Polynomial : remainder << 1;
            }

            table[value] = (byte)remainder;
        }

        return table;
    }
}

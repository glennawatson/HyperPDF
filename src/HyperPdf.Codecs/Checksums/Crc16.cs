// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Checksums;

/// <summary>
/// The 16 bit cyclic redundancy check with the polynomial x^16 + x^15 + x^2 + 1, not reflected and starting at zero, as FLAC uses for
/// its frames. The data is taken eight bytes at a time through eight tables so the check keeps up with the decoder.
/// </summary>
public static class Crc16
{
    /// <summary>The polynomial without its top bit.</summary>
    private const ushort Polynomial = 0x8005;

    /// <summary>The number of values a byte holds.</summary>
    private const int ByteValues = 256;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The top bit of the 16 bit register.</summary>
    private const int TopBit = 0x8000;

    /// <summary>The number of bytes taken at once.</summary>
    private const int Stride = 8;

    /// <summary>The mask of a byte.</summary>
    private const int ByteMask = 0xFF;

    /// <summary>The offset of the third byte of a group.</summary>
    private const int Third = 2;

    /// <summary>The offset of the fourth byte of a group.</summary>
    private const int Fourth = 3;

    /// <summary>The offset of the fifth byte of a group.</summary>
    private const int Fifth = 4;

    /// <summary>The offset of the sixth byte of a group.</summary>
    private const int Sixth = 5;

    /// <summary>The offset of the seventh byte of a group.</summary>
    private const int Seventh = 6;

    /// <summary>The offset of the eighth byte of a group.</summary>
    private const int Eighth = 7;

    /// <summary>Where row 1 starts in the tables.</summary>
    private const int Row1 = ByteValues;

    /// <summary>Where row 2 starts in the tables.</summary>
    private const int Row2 = Row1 + ByteValues;

    /// <summary>Where row 3 starts in the tables.</summary>
    private const int Row3 = Row2 + ByteValues;

    /// <summary>Where row 4 starts in the tables.</summary>
    private const int Row4 = Row3 + ByteValues;

    /// <summary>Where row 5 starts in the tables.</summary>
    private const int Row5 = Row4 + ByteValues;

    /// <summary>Where row 6 starts in the tables.</summary>
    private const int Row6 = Row5 + ByteValues;

    /// <summary>Where row 7 starts in the tables.</summary>
    private const int Row7 = Row6 + ByteValues;

    /// <summary>The tables, one row of 256 values for each of the eight places a byte can have in a group.</summary>
    private static readonly ushort[] Tables = BuildTables();

    /// <summary>Computes the check of some bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The check, which is 0 when the data ends with its own check.</returns>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0;
        var index = 0;
        for (; index + Stride <= data.Length; index += Stride)
        {
            // The register is folded into the first two bytes, then each byte is looked up in the row for its place.
            var first = data[index] ^ (crc >> ByteBits);
            var second = data[index + 1] ^ (crc & ByteMask);
            crc = Tables[Row7 + first]
                ^ Tables[Row6 + second]
                ^ Tables[Row5 + data[index + Third]]
                ^ Tables[Row4 + data[index + Fourth]]
                ^ Tables[Row3 + data[index + Fifth]]
                ^ Tables[Row2 + data[index + Sixth]]
                ^ Tables[Row1 + data[index + Seventh]]
                ^ Tables[data[index + Eighth]];
        }

        for (; index < data.Length; index++)
        {
            crc = ((crc << ByteBits) & ushort.MaxValue) ^ Tables[(crc >> ByteBits) ^ data[index]];
        }

        return (ushort)crc;
    }

    /// <summary>Builds the tables.</summary>
    /// <returns>The eight rows.</returns>
    private static ushort[] BuildTables()
    {
        var tables = new ushort[Stride * ByteValues];
        for (var value = 0; value < ByteValues; value++)
        {
            var remainder = value << ByteBits;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                remainder = (remainder & TopBit) != 0 ? ((remainder << 1) ^ Polynomial) & ushort.MaxValue : (remainder << 1) & ushort.MaxValue;
            }

            tables[value] = (ushort)remainder;
        }

        // Row n is row 0 with the byte followed by n more zero bytes, so a byte's effect is known for every place it can take.
        for (var row = 1; row < Stride; row++)
        {
            for (var value = 0; value < ByteValues; value++)
            {
                var previous = tables[((row - 1) * ByteValues) + value];
                tables[(row * ByteValues) + value] = (ushort)((previous << ByteBits) & ushort.MaxValue ^ tables[previous >> ByteBits]);
            }
        }

        return tables;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;

namespace HyperPdf.TestAssets;

/// <summary>Writes small PNG pictures for the textures of test models, so no picture file has to be stored.</summary>
public static class U3dPng
{
    /// <summary>The bytes of a red, green, blue, alpha pixel.</summary>
    private const int RgbaBytes = 4;

    /// <summary>The bytes of a red, green, blue pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes of a chunk length or checksum.</summary>
    private const int WordBytes = 4;

    /// <summary>The colour type of a picture with red, green, blue and alpha.</summary>
    private const byte RgbaColorType = 6;

    /// <summary>The colour type of a picture with red, green and blue.</summary>
    private const byte RgbColorType = 2;

    /// <summary>The colour type of a grey picture.</summary>
    private const byte GrayColorType = 0;

    /// <summary>The bit depth of a colour or grey channel.</summary>
    private const byte EightBits = 8;

    /// <summary>The bit depth of a bilevel picture.</summary>
    private const byte OneBit = 1;

    /// <summary>The bytes of the header chunk's data.</summary>
    private const int HeaderBytes = 13;

    /// <summary>The offset of the height in the header chunk's data.</summary>
    private const int HeightOffset = 4;

    /// <summary>The offset of the bit depth in the header chunk's data.</summary>
    private const int DepthOffset = 8;

    /// <summary>The offset of the colour type in the header chunk's data.</summary>
    private const int ColorTypeOffset = 9;

    /// <summary>The number of bits in the checksum polynomial's table index.</summary>
    private const int TableBits = 8;

    /// <summary>The polynomial of the checksum.</summary>
    private const uint Polynomial = 0xEDB88320;

    /// <summary>The checksum of each byte value.</summary>
    private static readonly uint[] ChecksumTable = MakeTable();

    /// <summary>Writes a picture of red, green, blue and alpha pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="rgba">The pixels, four bytes each, row by row from the top.</param>
    /// <returns>The PNG file.</returns>
    public static byte[] Rgba(int width, int height, byte[] rgba) => Encode(width, height, RgbaColorType, EightBits, rgba, width * RgbaBytes);

    /// <summary>Writes a picture of red, green and blue pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="rgb">The pixels, three bytes each, row by row from the top.</param>
    /// <returns>The PNG file.</returns>
    public static byte[] Rgb(int width, int height, byte[] rgb) => Encode(width, height, RgbColorType, EightBits, rgb, width * RgbBytes);

    /// <summary>Writes a grey picture.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="grey">The pixels, one byte each, row by row from the top.</param>
    /// <returns>The PNG file.</returns>
    public static byte[] Gray(int width, int height, byte[] grey) => Encode(width, height, GrayColorType, EightBits, grey, width);

    /// <summary>Writes a black and white picture.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="rows">The pixels, one bit each with the leftmost in the top bit and each row padded to whole bytes; a set bit is white.</param>
    /// <returns>The PNG file.</returns>
    public static byte[] Bilevel(int width, int height, byte[] rows) => Encode(width, height, GrayColorType, OneBit, rows, (width + EightBits - 1) / EightBits);

    /// <summary>Makes the checksum table.</summary>
    /// <returns>The table.</returns>
    private static uint[] MakeTable()
    {
        var table = new uint[1 << TableBits];
        for (uint n = 0; n < table.Length; n++)
        {
            var value = n;
            for (var bit = 0; bit < TableBits; bit++)
            {
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            }

            table[n] = value;
        }

        return table;
    }

    /// <summary>Writes a picture.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="colorType">The PNG colour type.</param>
    /// <param name="depth">The bit depth.</param>
    /// <param name="data">The pixels, row by row from the top.</param>
    /// <param name="rowBytes">The bytes in each row.</param>
    /// <returns>The PNG file.</returns>
    private static byte[] Encode(int width, int height, byte colorType, byte depth, byte[] data, int rowBytes)
    {
        var rows = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            rows.WriteByte(0);
            rows.Write(data, y * rowBytes, rowBytes);
        }

        var header = new byte[HeaderBytes];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(HeightOffset), height);
        header[DepthOffset] = depth;
        header[ColorTypeOffset] = colorType;
        using var file = new MemoryStream();
        file.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(file, "IHDR"u8, header);
        Chunk(file, "IDAT"u8, Deflate(rows.ToArray()));
        Chunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    /// <summary>Compresses data in the zlib format.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The compressed data.</returns>
    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    /// <summary>Writes a chunk: its length, type, data and checksum.</summary>
    /// <param name="file">Receives the chunk.</param>
    /// <param name="type">The four-letter type.</param>
    /// <param name="data">The data.</param>
    private static void Chunk(MemoryStream file, ReadOnlySpan<byte> type, byte[] data)
    {
        Span<byte> word = stackalloc byte[WordBytes];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        file.Write(word);
        file.Write(type);
        file.Write(data);
        var crc = uint.MaxValue;
        foreach (var value in type)
        {
            crc = ChecksumTable[(crc ^ value) & 0xFF] ^ (crc >> TableBits);
        }

        foreach (var value in data)
        {
            crc = ChecksumTable[(crc ^ value) & 0xFF] ^ (crc >> TableBits);
        }

        BinaryPrimitives.WriteUInt32BigEndian(word, ~crc);
        file.Write(word);
    }
}

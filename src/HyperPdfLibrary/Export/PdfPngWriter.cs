// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Export;

/// <summary>
/// Writes decoded page images as PNG files: opaque grey images as 8-bit grey, opaque colour images as 8-bit RGB, and images
/// with transparency as 8-bit RGBA. The renderer's pixels are premultiplied, so colour is divided by alpha on the way out.
/// A stencil mask is written as grey with painted pixels black.
/// </summary>
internal static class PdfPngWriter
{
    /// <summary>The PNG colour type for 8-bit grey.</summary>
    private const byte GreyType = 0;

    /// <summary>The PNG colour type for 8-bit RGB.</summary>
    private const byte RgbType = 2;

    /// <summary>The PNG colour type for 8-bit RGBA.</summary>
    private const byte RgbaType = 6;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bit depth of every channel written.</summary>
    private const byte BitDepth = 8;

    /// <summary>The most a channel can be.</summary>
    private const int ChannelMax = 255;

    /// <summary>The length of the IHDR chunk's data.</summary>
    private const int HeaderLength = 13;

    /// <summary>The offset of the height in the IHDR chunk's data.</summary>
    private const int HeightOffset = 4;

    /// <summary>The offset of the bit depth in the IHDR chunk's data.</summary>
    private const int DepthOffset = 8;

    /// <summary>The offset of the colour type in the IHDR chunk's data.</summary>
    private const int TypeOffset = 9;

    /// <summary>The chunks a file has: header, data and end.</summary>
    private const int ChunkCount = 3;

    /// <summary>The bytes a chunk adds around its data: length, type and checksum.</summary>
    private const int ChunkOverhead = 12;

    /// <summary>The CRC-32 polynomial, reflected.</summary>
    private const uint Polynomial = 0xEDB88320;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The entries of the CRC table.</summary>
    private const int TableSize = 256;

    /// <summary>The divisor that halves a number, to round a division to the nearest.</summary>
    private const int Halve = 2;

    /// <summary>The 8 byte signature every PNG file starts with.</summary>
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The table the CRC-32 of each chunk is computed from, built once.</summary>
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Encodes an image as a PNG file.</summary>
    /// <param name="image">The decoded image.</param>
    /// <returns>The file's bytes.</returns>
    internal static byte[] Encode(PdfImageData image)
    {
        var (type, channels) = ColorOf(image);
        var rows = Rows(image, type, channels);
        var data = default(PooledBuffer);
        try
        {
            ZLibCodec.Compress(rows, PdfCompressionLevel.Fastest, ref data);
            return Assemble(image.Width, image.Height, type, data.WrittenSpan);
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>Chooses the PNG colour type for an image.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The colour type and the channels per pixel.</returns>
    private static PngColor ColorOf(PdfImageData image)
    {
        if (image.IsGray || image.IsStencilMask)
        {
            return new(GreyType, 1);
        }

        return IsOpaque(image.Pixels) ? new(RgbType, RgbBytes) : new(RgbaType, BgraBytes);
    }

    /// <summary>Determines whether every pixel of a BGRA image is fully opaque.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <returns><see langword="true"/> when no pixel has transparency.</returns>
    private static bool IsOpaque(byte[] pixels)
    {
        for (var i = BgraBytes - 1; i < pixels.Length; i += BgraBytes)
        {
            if (pixels[i] != ChannelMax)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds the filtered scan lines: a filter byte of 0 and then the row's samples.</summary>
    /// <param name="image">The image.</param>
    /// <param name="type">The PNG colour type.</param>
    /// <param name="channels">The channels per pixel.</param>
    /// <returns>The scan lines.</returns>
    private static byte[] Rows(PdfImageData image, byte type, int channels)
    {
        var stride = (image.Width * channels) + 1;
        var rows = new byte[stride * image.Height];
        for (var y = 0; y < image.Height; y++)
        {
            WriteRow(image, type, y, rows.AsSpan((y * stride) + 1, stride - 1));
        }

        return rows;
    }

    /// <summary>Writes one row of samples.</summary>
    /// <param name="image">The image.</param>
    /// <param name="type">The PNG colour type.</param>
    /// <param name="y">The row index.</param>
    /// <param name="samples">Receives the row's samples.</param>
    private static void WriteRow(PdfImageData image, byte type, int y, Span<byte> samples)
    {
        var width = image.Width;
        if (type == GreyType)
        {
            var source = image.Pixels.AsSpan(y * width, width);
            for (var x = 0; x < width; x++)
            {
                samples[x] = image.IsStencilMask ? (byte)(ChannelMax - source[x]) : source[x];
            }

            return;
        }

        var step = type == RgbType ? RgbBytes : BgraBytes;
        var bgra = image.Pixels.AsSpan(y * width * BgraBytes, width * BgraBytes);
        for (var x = 0; x < width; x++)
        {
            WritePixel(bgra.Slice(x * BgraBytes, BgraBytes), type, samples[(x * step)..]);
        }
    }

    /// <summary>Writes one colour pixel, dividing the premultiplied colour by its alpha.</summary>
    /// <param name="bgra">The premultiplied BGRA pixel.</param>
    /// <param name="type">The PNG colour type.</param>
    /// <param name="destination">Receives the RGB or RGBA samples.</param>
    private static void WritePixel(ReadOnlySpan<byte> bgra, byte type, Span<byte> destination)
    {
        var alpha = bgra[BgraBytes - 1];
        destination[0] = Straight(bgra[2], alpha);
        destination[1] = Straight(bgra[1], alpha);
        destination[2] = Straight(bgra[0], alpha);
        if (type == RgbaType)
        {
            destination[RgbBytes] = alpha;
        }
    }

    /// <summary>Divides a premultiplied channel by its alpha.</summary>
    /// <param name="value">The premultiplied channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The straight channel; 0 for a fully transparent pixel.</returns>
    private static byte Straight(byte value, byte alpha) => alpha switch
    {
        0 => 0,
        ChannelMax => value,
        _ => (byte)Math.Min(ChannelMax, ((value * ChannelMax) + (alpha / Halve)) / alpha),
    };

    /// <summary>Puts the signature and chunks of a PNG file together.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="type">The colour type.</param>
    /// <param name="compressed">The zlib-compressed scan lines.</param>
    /// <returns>The file.</returns>
    private static byte[] Assemble(int width, int height, byte type, ReadOnlySpan<byte> compressed)
    {
        var header = new byte[HeaderLength];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(HeightOffset), height);
        header[DepthOffset] = BitDepth;
        header[TypeOffset] = type;
        using var file = new MemoryStream(Signature.Length + (ChunkOverhead * ChunkCount) + HeaderLength + compressed.Length);
        file.Write(Signature);
        WriteChunk(file, "IHDR"u8, header);
        WriteChunk(file, "IDAT"u8, compressed);
        WriteChunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    /// <summary>Writes one chunk.</summary>
    /// <param name="output">The file.</param>
    /// <param name="type">The four type bytes.</param>
    /// <param name="data">The chunk's data.</param>
    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[BgraBytes];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        output.Write(number);
        output.Write(type);
        output.Write(data);
        var crc = Crc(Crc(uint.MaxValue, type), data) ^ uint.MaxValue;
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        output.Write(number);
    }

    /// <summary>Continues a CRC-32 over more bytes.</summary>
    /// <param name="crc">The running value.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The new running value.</returns>
    private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & ChannelMax] ^ (crc >> ByteBits);
        }

        return crc;
    }

    /// <summary>Builds the CRC-32 table.</summary>
    /// <returns>The table.</returns>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[TableSize];
        for (uint n = 0; n < TableSize; n++)
        {
            var value = n;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            }

            table[n] = value;
        }

        return table;
    }

    /// <summary>A PNG colour type and its samples per pixel.</summary>
    /// <param name="Type">The colour type.</param>
    /// <param name="Channels">The samples per pixel.</param>
    private readonly record struct PngColor(byte Type, int Channels);
}

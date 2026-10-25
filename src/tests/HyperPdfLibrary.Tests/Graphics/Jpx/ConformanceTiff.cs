// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Reads the reference images of the JP2 file tests of the T.803 conformance data: baseline TIFF files with 8 or 16 bits per sample,
/// uncompressed or LZW compressed, with or without the horizontal differencing predictor, in strips.
/// </summary>
internal static class ConformanceTiff
{
    /// <summary>The tag of the image width.</summary>
    private const int WidthTag = 256;

    /// <summary>The tag of the image height.</summary>
    private const int HeightTag = 257;

    /// <summary>The tag of the bits per sample.</summary>
    private const int BitsTag = 258;

    /// <summary>The tag of the compression.</summary>
    private const int CompressionTag = 259;

    /// <summary>The tag of the strip offsets.</summary>
    private const int StripOffsetsTag = 273;

    /// <summary>The tag of the samples per pixel.</summary>
    private const int SamplesTag = 277;

    /// <summary>The tag of the rows per strip.</summary>
    private const int RowsPerStripTag = 278;

    /// <summary>The tag of the strip byte counts.</summary>
    private const int StripCountsTag = 279;

    /// <summary>The tag of the predictor.</summary>
    private const int PredictorTag = 317;

    /// <summary>The compression value of uncompressed data.</summary>
    private const int Uncompressed = 1;

    /// <summary>The compression value of LZW.</summary>
    private const int Lzw = 5;

    /// <summary>The predictor value of horizontal differencing.</summary>
    private const int Differencing = 2;

    /// <summary>The bytes of a directory entry.</summary>
    private const int EntryBytes = 12;

    /// <summary>The offset of the field type in a directory entry.</summary>
    private const int TypeOffset = 2;

    /// <summary>The offset of the count in a directory entry.</summary>
    private const int CountOffset = 4;

    /// <summary>The offset of the value in a directory entry.</summary>
    private const int ValueOffset = 8;

    /// <summary>The field type of a 16-bit value.</summary>
    private const int ShortType = 3;

    /// <summary>The field type of a 32-bit value.</summary>
    private const int LongType = 4;

    /// <summary>The bytes of a 16-bit value.</summary>
    private const int ShortBytes = 2;

    /// <summary>The bytes of a 32-bit value.</summary>
    private const int LongBytes = 4;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The offset of the first directory in the header.</summary>
    private const int DirectoryPointer = 4;

    /// <summary>Reads an image.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The image, or <see langword="null"/> when the file uses a feature the reader does not.</returns>
    internal static TiffImage? Read(string path)
    {
        var data = File.ReadAllBytes(path);
        var big = data[0] == (byte)'M';
        var tags = ReadTags(data, big);
        if (!tags.TryGetValue(WidthTag, out var width) || !tags.TryGetValue(HeightTag, out var height) || !tags.TryGetValue(BitsTag, out var bits)
            || !tags.ContainsKey(StripOffsetsTag) || !tags.ContainsKey(StripCountsTag))
        {
            return null;
        }

        var compression = Tag(tags, CompressionTag, Uncompressed);
        if (compression is not (Uncompressed or Lzw) || bits[0] is not (ByteBits or (ByteBits * ShortBytes)))
        {
            return null;
        }

        var components = Tag(tags, SamplesTag, 1);
        var image = new TiffImage((int)width[0], (int)height[0], components, (int)bits[0], new int[(int)(width[0] * height[0] * components)]);
        ReadStrips(image, data, tags, compression, big);
        return image;
    }

    /// <summary>Reads the strips into an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="data">The file.</param>
    /// <param name="tags">The tags.</param>
    /// <param name="compression">The compression.</param>
    /// <param name="big">Whether the file is big-endian.</param>
    private static void ReadStrips(TiffImage image, byte[] data, Dictionary<int, long[]> tags, int compression, bool big)
    {
        var rows = Tag(tags, RowsPerStripTag, image.Height);
        var row = 0;
        for (var s = 0; s < tags[StripOffsetsTag].Length && row < image.Height; s++)
        {
            var strips = Math.Min(rows, image.Height - row);
            var length = image.Width * image.Components * (image.Bits / ByteBits) * strips;
            var bytes = Unpack(data.AsSpan((int)tags[StripOffsetsTag][s], (int)tags[StripCountsTag][s]), compression, length);
            Fill(image, bytes, row, strips, Tag(tags, PredictorTag, 1) == Differencing, big);
            row += strips;
        }
    }

    /// <summary>Reads the tags of the first directory.</summary>
    /// <param name="data">The file.</param>
    /// <param name="big">Whether the file is big-endian.</param>
    /// <returns>The values of each tag.</returns>
    private static Dictionary<int, long[]> ReadTags(byte[] data, bool big)
    {
        var directory = (int)ReadValue(data, DirectoryPointer, LongBytes, big);
        var count = (int)ReadValue(data, directory, ShortBytes, big);
        var tags = new Dictionary<int, long[]>();
        for (var i = 0; i < count; i++)
        {
            var entry = directory + ShortBytes + (i * EntryBytes);
            tags[(int)ReadValue(data, entry, ShortBytes, big)] = ReadEntry(data, entry, big);
        }

        return tags;
    }

    /// <summary>Gets the first value of a tag.</summary>
    /// <param name="tags">The tags.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="fallback">The value when the tag is missing.</param>
    /// <returns>The value.</returns>
    private static int Tag(Dictionary<int, long[]> tags, int tag, int fallback) => tags.TryGetValue(tag, out var values) ? (int)values[0] : fallback;

    /// <summary>Reads a number.</summary>
    /// <param name="data">The file.</param>
    /// <param name="offset">Where the number is.</param>
    /// <param name="size">Its bytes: 2 or 4.</param>
    /// <param name="big">Whether the file is big-endian.</param>
    /// <returns>The value.</returns>
    private static long ReadValue(ReadOnlySpan<byte> data, int offset, int size, bool big)
    {
        if (size == ShortBytes)
        {
            return big ? BinaryPrimitives.ReadUInt16BigEndian(data[offset..]) : BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
        }

        return big ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..]) : BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
    }

    /// <summary>Reads the values of a directory entry, which are 16 or 32 bits and sit in the entry itself when they fit.</summary>
    /// <param name="data">The file.</param>
    /// <param name="entry">Where the entry is.</param>
    /// <param name="big">Whether the file is big-endian.</param>
    /// <returns>The values.</returns>
    private static long[] ReadEntry(ReadOnlySpan<byte> data, int entry, bool big)
    {
        var type = ReadValue(data, entry + TypeOffset, ShortBytes, big);
        if (type is not (ShortType or LongType))
        {
            return [];
        }

        var size = type == ShortType ? ShortBytes : LongBytes;
        var count = (int)ReadValue(data, entry + CountOffset, LongBytes, big);
        var start = count * size <= LongBytes ? entry + ValueOffset : (int)ReadValue(data, entry + ValueOffset, LongBytes, big);
        var values = new long[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = ReadValue(data, start + (i * size), size, big);
        }

        return values;
    }

    /// <summary>Decompresses a strip.</summary>
    /// <param name="raw">The strip's bytes.</param>
    /// <param name="compression">The compression.</param>
    /// <param name="length">The bytes the strip holds.</param>
    /// <returns>The strip's samples as bytes.</returns>
    private static byte[] Unpack(ReadOnlySpan<byte> raw, int compression, int length)
    {
        var bytes = new byte[length];
        if (compression != Lzw)
        {
            raw[..Math.Min(length, raw.Length)].CopyTo(bytes);
            return bytes;
        }

        var output = default(PooledBuffer);
        try
        {
            LzwFilter.Decode(raw, 1, ref output);
            output.WrittenSpan[..Math.Min(length, output.Length)].CopyTo(bytes);
        }
        finally
        {
            output.Dispose();
        }

        return bytes;
    }

    /// <summary>Copies a strip's samples into the image, adding each sample to the one before it in its row when the predictor says so.</summary>
    /// <param name="image">The image.</param>
    /// <param name="bytes">The strip's bytes.</param>
    /// <param name="firstRow">The first row of the strip.</param>
    /// <param name="rows">The rows of the strip.</param>
    /// <param name="differenced">Whether the strip uses the horizontal differencing predictor.</param>
    /// <param name="big">Whether 16-bit samples are big-endian.</param>
    private static void Fill(TiffImage image, byte[] bytes, int firstRow, int rows, bool differenced, bool big)
    {
        var sampleBytes = image.Bits / ByteBits;
        var perRow = image.Width * image.Components;
        var mask = (1 << image.Bits) - 1;
        for (var y = 0; y < rows; y++)
        {
            for (var i = 0; i < perRow; i++)
            {
                var offset = ((y * perRow) + i) * sampleBytes;
                var value = sampleBytes == 1 ? bytes[offset] : ReadValue(bytes, offset, ShortBytes, big);
                var target = ((firstRow + y) * perRow) + i;
                var before = differenced && i >= image.Components ? image.Values[target - image.Components] : 0;
                image.Values[target] = (int)((value + before) & mask);
            }
        }
    }

    /// <summary>A reference image: its size, components, depth and interleaved samples.</summary>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    /// <param name="Components">The samples per pixel.</param>
    /// <param name="Bits">The bits per sample.</param>
    /// <param name="Values">The samples, interleaved, row by row.</param>
    internal sealed record TiffImage(int Width, int Height, int Components, int Bits, int[] Values);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.ImageFiles;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.ImageFiles;

/// <summary>Tests that palette bitmaps of 1, 4 and 8 bits decode to the colours their indexes name, stored either way up.</summary>
public sealed class BmpPaletteTests
{
    /// <summary>The seed of the random indexes.</summary>
    private const uint Seed = 99_991;

    /// <summary>The height of each test image.</summary>
    private const int Height = 4;

    /// <summary>The bytes in the file header.</summary>
    private const int FileHeaderBytes = 14;

    /// <summary>The bytes in the bitmap info header.</summary>
    private const int InfoHeaderBytes = 40;

    /// <summary>The bytes in a palette entry in the file.</summary>
    private const int EntryBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in a word, to which rows are padded.</summary>
    private const int WordBits = 32;

    /// <summary>The bytes in a word.</summary>
    private const int WordBytes = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The offset of the third colour sample in a pixel or palette entry.</summary>
    private const int BlueIndex = 2;

    /// <summary>The offset of the planes field.</summary>
    private const int PlanesOffset = 26;

    /// <summary>The offset of the bits per pixel field.</summary>
    private const int BitCountOffset = 28;

    /// <summary>The offset of the colours used field.</summary>
    private const int ColoursUsedOffset = 46;

    /// <summary>The offset of the height field.</summary>
    private const int HeightOffset = 22;

    /// <summary>The offset of the width field.</summary>
    private const int WidthOffset = 18;

    /// <summary>The offset of the pixel data offset field.</summary>
    private const int DataOffsetField = 10;

    /// <summary>The offset of the info header size field.</summary>
    private const int InfoSizeOffset = 14;

    /// <summary>The widths tried, around the pixels in a byte and the padding of rows.</summary>
    private static readonly int[] Widths = [1, 3, 7, 8, 9, 17, 33];

    /// <summary>The bit depths tried.</summary>
    private static readonly int[] Depths = [1, 4, 8];

    /// <summary>A palette of colours that are not grey.</summary>
    private static readonly byte[][] Colours = [[10, 20, 30], [200, 100, 50], [0, 255, 0], [7, 8, 9], [255, 255, 0]];

    /// <summary>Random palette bitmaps decode to the colour of each index, bottom up or top down.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColourPalettes_DecodeToTheirColours()
    {
        var random = new SampleRandom(Seed);
        foreach (var depth in Depths)
        {
            foreach (var width in Widths)
            {
                foreach (var topDown in new[] { false, true })
                {
                    // The palette is shorter than the index range at 4 and 8 bits, so indexes past it must read the last colour.
                    var entries = Math.Min(Colours.Length, 1 << depth);
                    var indexes = Indexes(random, width, depth);
                    var picture = PdfImageFiles.Decode(Build(width, depth, topDown, entries, indexes), CancellationToken.None);

                    await Assert.That(picture.Format).IsEqualTo(PdfRasterFormat.Rgb24).Because($"{depth} bits, width {width}");
                    var pixels = picture.Pixels.ToArray();
                    for (var i = 0; i < indexes.Length; i++)
                    {
                        var colour = Colours[Math.Min(indexes[i], entries - 1)];
                        var at = i * RgbBytes;
                        await Assert.That(new[] { pixels[at], pixels[at + 1], pixels[at + BlueIndex] }).IsEquivalentTo(colour).Because($"{depth} bits, width {width}, topDown {topDown}, pixel {i}");
                    }
                }
            }
        }
    }

    /// <summary>Builds a bitmap file whose rows hold the given indexes top row first.</summary>
    /// <param name="width">The width.</param>
    /// <param name="depth">The bits per pixel.</param>
    /// <param name="topDown">Whether the rows are stored from the top.</param>
    /// <param name="entries">The palette entries to write.</param>
    /// <param name="indexes">The indexes, top row first.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int width, int depth, bool topDown, int entries, byte[] indexes)
    {
        var stride = (((width * depth) + WordBits - 1) / WordBits) * WordBytes;
        var dataOffset = FileHeaderBytes + InfoHeaderBytes + (entries * EntryBytes);
        var file = new byte[dataOffset + (stride * Height)];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(DataOffsetField), dataOffset);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(InfoSizeOffset), InfoHeaderBytes);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(WidthOffset), width);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(HeightOffset), topDown ? -Height : Height);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(PlanesOffset), 1);
        BinaryPrimitives.WriteInt16LittleEndian(file.AsSpan(BitCountOffset), (short)depth);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(ColoursUsedOffset), entries);
        for (var i = 0; i < entries; i++)
        {
            var at = FileHeaderBytes + InfoHeaderBytes + (i * EntryBytes);
            file[at] = Colours[i][BlueIndex];
            file[at + 1] = Colours[i][1];
            file[at + BlueIndex] = Colours[i][0];
        }

        for (var row = 0; row < Height; row++)
        {
            var stored = topDown ? row : Height - 1 - row;
            for (var x = 0; x < width; x++)
            {
                var bit = x * depth;
                file[dataOffset + (stored * stride) + (bit / ByteBits)] |= (byte)(indexes[(row * width) + x] << (ByteBits - depth - (bit % ByteBits)));
            }
        }

        return file;
    }

    /// <summary>Makes random indexes for every pixel of an image, some past the end of the palette.</summary>
    /// <param name="random">The generator.</param>
    /// <param name="width">The width.</param>
    /// <param name="depth">The bits per pixel.</param>
    /// <returns>The indexes, top row first.</returns>
    private static byte[] Indexes(SampleRandom random, int width, int depth)
    {
        var indexes = new byte[width * Height];
        for (var i = 0; i < indexes.Length; i++)
        {
            indexes[i] = (byte)random.Next(1 << depth);
        }

        return indexes;
    }
}

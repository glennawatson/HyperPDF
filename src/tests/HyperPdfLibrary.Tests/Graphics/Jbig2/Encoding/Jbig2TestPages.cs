// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;
using HyperPdfLibrary.Tests.Robustness;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>Builds deterministic 1-bit test pages in the PDF layout: packed rows, most significant bit first, 0 for black.</summary>
internal static class Jbig2TestPages
{
    /// <summary>The seed of the noise pages.</summary>
    internal const int NoiseSeed = 3;

    /// <summary>The share of black pixels in a noise page.</summary>
    internal const int NoisePercent = 30;

    /// <summary>The seed of the text pages.</summary>
    internal const int TextSeed = 5;

    /// <summary>The pixels per glyph pixel in a default text page.</summary>
    internal const int TextScale = 2;

    /// <summary>The stray pixels added to a default text page.</summary>
    internal const int TextNoise = 3;

    /// <summary>The columns of every test glyph.</summary>
    private const int GlyphColumns = 5;

    /// <summary>The rows of every test glyph.</summary>
    private const int GlyphRows = 7;

    /// <summary>The share of glyphs drawn one glyph pixel lower, as on a descending line.</summary>
    private const int DropOneIn = 3;

    /// <summary>The percent scale.</summary>
    private const int Percent = 100;

    /// <summary>The bits in a byte, as a shift.</summary>
    private const int ByteShift = 3;

    /// <summary>The mask of a pixel's bit within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>The bit of the first pixel in a byte.</summary>
    private const int FirstBit = 0x80;

    /// <summary>The gap, in glyph pixels, between glyphs on a line.</summary>
    private const int GlyphGap = 2;

    /// <summary>The gap, in glyph pixels, between lines.</summary>
    private const int LineGap = 4;

    /// <summary>The glyph pixel rows of the test glyphs, five columns wide.</summary>
    private static readonly string[][] Glyphs =
    [
        [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
        ["####.", "#...#", "####.", "#...#", "#...#", "#...#", "####."],
        [".####", "#....", "#....", "#....", "#....", "#....", ".####"],
        ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
        ["#####", "#....", "####.", "#....", "#....", "#....", "#####"],
        ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    ];

    /// <summary>Creates a page of a kind.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="size">The page size.</param>
    /// <returns>The rows.</returns>
    internal static byte[] Create(Jbig2TestPageKind kind, Jbig2Size size) => kind switch
    {
        Jbig2TestPageKind.Noise => Noise(size.Width, size.Height, NoiseSeed, NoisePercent),
        Jbig2TestPageKind.Text => Text(size.Width, size.Height, TextScale, TextSeed, TextNoise),
        _ => White(size.Width, size.Height),
    };

    /// <summary>Creates a blank page: every sample 1, which is white.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The rows.</returns>
    internal static byte[] White(int width, int height)
    {
        var rows = new byte[Jbig2Decoder.GetRowBytes(width) * height];
        rows.AsSpan().Fill(byte.MaxValue);
        return rows;
    }

    /// <summary>Creates a page of random black pixels.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="seed">The random seed.</param>
    /// <param name="percentBlack">The share of pixels that are black, 0 to 100.</param>
    /// <returns>The rows.</returns>
    internal static byte[] Noise(int width, int height, int seed, int percentBlack)
    {
        var rows = White(width, height);
        var random = new SeededRandom(seed);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (random.Next(Percent) < percentBlack)
                {
                    SetBlack(rows, width, x, y);
                }
            }
        }

        return rows;
    }

    /// <summary>Creates a page of text-like lines: the test glyphs at scale, with a little vertical jitter.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="scale">The pixels per glyph pixel.</param>
    /// <param name="seed">The random seed.</param>
    /// <param name="noisePixels">The number of stray black pixels added.</param>
    /// <returns>The rows.</returns>
    internal static byte[] Text(int width, int height, int scale, int seed, int noisePixels)
    {
        var rows = White(width, height);
        var random = new SeededRandom(seed);
        var glyphWidth = GlyphColumns * scale;
        var glyphHeight = GlyphRows * scale;
        var pitch = glyphWidth + (GlyphGap * scale);
        for (var top = scale; top + glyphHeight + (GlyphGap * scale) < height; top += glyphHeight + (LineGap * scale))
        {
            for (var left = scale; left + glyphWidth < width; left += pitch)
            {
                var glyph = Glyphs[random.Next(Glyphs.Length)];
                var drop = random.Next(DropOneIn) == 0 ? scale : 0;
                Draw(rows, new(width, height), glyph, new(left, top + drop), scale);
            }
        }

        for (var i = 0; i < noisePixels; i++)
        {
            SetBlack(rows, width, random.Next(width), random.Next(height));
        }

        return rows;
    }

    /// <summary>Creates a page of text-like lines.</summary>
    /// <param name="page">The page size.</param>
    /// <param name="style">The scale, seed and stray pixels.</param>
    /// <returns>The rows.</returns>
    internal static byte[] Text(Jbig2Size page, Jbig2TestTextStyle style) => Text(page.Width, page.Height, style.Scale, style.Seed, style.StrayPixels);

    /// <summary>Draws a filled rectangle.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The page width.</param>
    /// <param name="rectangle">The rectangle.</param>
    internal static void Fill(byte[] rows, int width, Jbig2RegionBox rectangle)
    {
        for (var y = rectangle.Y; y < rectangle.Y + rectangle.Height; y++)
        {
            for (var x = rectangle.X; x < rectangle.X + rectangle.Width; x++)
            {
                SetBlack(rows, width, x, y);
            }
        }
    }

    /// <summary>Clears a rectangle to white.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The page width.</param>
    /// <param name="rectangle">The rectangle.</param>
    internal static void Clear(byte[] rows, int width, Jbig2RegionBox rectangle)
    {
        var stride = Jbig2Decoder.GetRowBytes(width);
        for (var y = rectangle.Y; y < rectangle.Y + rectangle.Height; y++)
        {
            for (var x = rectangle.X; x < rectangle.X + rectangle.Width; x++)
            {
                rows[(y * stride) + (x >> ByteShift)] |= (byte)(FirstBit >> (x & BitMask));
            }
        }
    }

    /// <summary>Flips a pixel.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The page width.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    internal static void Flip(byte[] rows, int width, int x, int y) =>
        rows[(y * Jbig2Decoder.GetRowBytes(width)) + (x >> ByteShift)] ^= (byte)(FirstBit >> (x & BitMask));

    /// <summary>Gets a pixel.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when black.</returns>
    internal static bool IsBlack(byte[] rows, int width, int x, int y) =>
        (rows[(y * Jbig2Decoder.GetRowBytes(width)) + (x >> ByteShift)] & (FirstBit >> (x & BitMask))) == 0;

    /// <summary>Counts the pixels where two pages differ.</summary>
    /// <param name="first">The first page.</param>
    /// <param name="second">The second page.</param>
    /// <param name="size">The page size.</param>
    /// <returns>The number of differing pixels.</returns>
    internal static int CountDifferences(byte[] first, byte[] second, Jbig2Size size)
    {
        var differences = 0;
        for (var y = 0; y < size.Height; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                differences += IsBlack(first, size.Width, x, y) == IsBlack(second, size.Width, x, y) ? 0 : 1;
            }
        }

        return differences;
    }

    /// <summary>Decodes coded data with the library's decoder.</summary>
    /// <param name="data">The page data.</param>
    /// <param name="globals">The globals, or null.</param>
    /// <param name="size">The page size.</param>
    /// <returns>The rows, or null when nothing decoded.</returns>
    internal static byte[]? Decode(byte[] data, byte[]? globals, Jbig2Size size)
    {
        var rows = new byte[Jbig2Decoder.GetRowBytes(size.Width) * size.Height];
        return Jbig2Decoder.TryDecode(data, globals, size.Width, size.Height, rows) ? rows : null;
    }

    /// <summary>Sets a pixel black.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    private static void SetBlack(byte[] rows, int width, int x, int y) =>
        rows[(y * Jbig2Decoder.GetRowBytes(width)) + (x >> ByteShift)] &= (byte)~(FirstBit >> (x & BitMask));

    /// <summary>Draws a scaled glyph.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="page">The page size.</param>
    /// <param name="glyph">The glyph rows.</param>
    /// <param name="origin">The glyph's top left corner.</param>
    /// <param name="scale">The pixels per glyph pixel.</param>
    private static void Draw(byte[] rows, Jbig2Size page, string[] glyph, Jbig2Point origin, int scale)
    {
        for (var gy = 0; gy < glyph.Length; gy++)
        {
            for (var gx = 0; gx < glyph[gy].Length; gx++)
            {
                if (glyph[gy][gx] == '#')
                {
                    FillClipped(rows, page, new(origin.X + (gx * scale), origin.Y + (gy * scale)), scale);
                }
            }
        }
    }

    /// <summary>Fills a square, clipped to the page.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="page">The page size.</param>
    /// <param name="corner">The square's top left corner.</param>
    /// <param name="side">The side in pixels.</param>
    private static void FillClipped(byte[] rows, Jbig2Size page, Jbig2Point corner, int side)
    {
        for (var y = corner.Y; y < Math.Min(corner.Y + side, page.Height); y++)
        {
            for (var x = corner.X; x < Math.Min(corner.X + side, page.Width); x++)
            {
                SetBlack(rows, page.Width, x, y);
            }
        }
    }
}

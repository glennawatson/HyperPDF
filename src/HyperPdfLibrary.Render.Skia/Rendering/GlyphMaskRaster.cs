// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Draws a glyph outline once into the pixels of a glyph mask.</summary>
internal static class GlyphMaskRaster
{
    /// <summary>The largest side of a mask in pixels; bigger glyphs are filled from their outline.</summary>
    internal const int MaxSide = 96;

    /// <summary>The pixels of padding around the glyph's bounds, so softened edges are not cut.</summary>
    private const int Padding = 1;

    /// <summary>The sub-pixel columns drawn for each pixel of an LCD mask.</summary>
    private const int SubPixels = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The most a channel can be.</summary>
    private const int ChannelMax = 255;

    /// <summary>The bits in a colour channel.</summary>
    private const int ChannelBits = 8;

    /// <summary>The sub-pixels each side of a channel the filter reaches.</summary>
    private const int FilterReach = 2;

    /// <summary>The weight total of the filter.</summary>
    private const int FilterTotal = 256;

    /// <summary>The weights of the five sub-pixels around a channel, which add up to 256. They soften the colour fringes of LCD text.</summary>
    private static readonly int[] LcdFilter = [8, 77, 86, 77, 8];

    /// <summary>Makes a mask for a glyph.</summary>
    /// <param name="path">The glyph's outline in its own space.</param>
    /// <param name="key">What the mask is for.</param>
    /// <param name="mask">Receives the mask; <see langword="null"/> when the glyph paints nothing.</param>
    /// <returns><see langword="false"/> when the glyph is too large to draw from a mask.</returns>
    internal static bool TryCreate(SKPath path, in GlyphMaskKey key, out GlyphMask? mask)
    {
        mask = null;
        var matrix = new SKMatrix(
            MathF.ScaleB(key.A, -key.Shift),
            MathF.ScaleB(key.C, -key.Shift),
            key.SubX / (float)GlyphMaskPlacement.PixelSteps,
            MathF.ScaleB(key.B, -key.Shift),
            MathF.ScaleB(key.D, -key.Shift),
            key.SubY / (float)GlyphMaskPlacement.PixelSteps,
            0,
            0,
            1);
        var bounds = matrix.MapRect(path.Bounds);
        if (bounds.IsEmpty || !float.IsFinite(bounds.Width) || !float.IsFinite(bounds.Height))
        {
            return true;
        }

        var left = (int)MathF.Floor(bounds.Left) - Padding;
        var top = (int)MathF.Floor(bounds.Top) - Padding;
        var width = (int)MathF.Ceiling(bounds.Right) + Padding - left;
        var height = (int)MathF.Ceiling(bounds.Bottom) + Padding - top;
        if (width > MaxSide || height > MaxSide)
        {
            return false;
        }

        var lcd = key.Edges == GlyphMaskEdges.Lcd;
        var columns = lcd ? SubPixels : 1;
        var coverage = DrawCoverage(path, matrix, new(left, top, width * columns, height), key, columns);
        mask = lcd ? MakeLcd(coverage, new(left, top, width, height), key.Color) : MakeGrey(coverage, new(left, top, width, height));
        return true;
    }

    /// <summary>Draws a glyph into a byte for each sub-pixel of coverage.</summary>
    /// <param name="path">The glyph's outline.</param>
    /// <param name="matrix">The matrix to the mask's pixels before its offset.</param>
    /// <param name="area">The mask's offset from the glyph's pixel in pixels, and its size in sub-pixel columns and rows.</param>
    /// <param name="key">What the mask is for.</param>
    /// <param name="columns">The sub-pixel columns in each pixel.</param>
    /// <returns>The coverage, a row after row of bytes.</returns>
    private static byte[] DrawCoverage(SKPath path, SKMatrix matrix, in MaskArea area, in GlyphMaskKey key, int columns)
    {
        using var bitmap = new SKBitmap(new(area.Width, area.Height, SKColorType.Alpha8, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(columns, 1);
            canvas.Translate(-area.Left, -area.Top);
            canvas.Concat(in matrix);
            using var paint = new SKPaint { IsAntialias = key.Edges != GlyphMaskEdges.Sharp, Color = SKColors.White };
            canvas.DrawPath(path, paint);
        }

        var pixels = bitmap.GetPixelSpan().ToArray();
        GlyphMaskContrast.Apply(pixels, key.Contrast);
        return pixels;
    }

    /// <summary>Makes a grey coverage mask.</summary>
    /// <param name="coverage">The coverage.</param>
    /// <param name="area">The mask's offset from the glyph's pixel, and its size in pixels.</param>
    /// <returns>The mask.</returns>
    private static GlyphMask MakeGrey(byte[] coverage, in MaskArea area)
    {
        var info = new SKImageInfo(area.Width, area.Height, SKColorType.Alpha8, SKAlphaType.Premul);
        var image = SKImage.FromPixelCopy(info, coverage);
        return new(image, null, area.Left, area.Top, coverage.Length);
    }

    /// <summary>Makes an LCD mask from sub-pixel coverage.</summary>
    /// <param name="coverage">The coverage, three columns for each pixel.</param>
    /// <param name="area">The mask's offset from the glyph's pixel, and its size in pixels.</param>
    /// <param name="color">The text colour as 0xRRGGBB.</param>
    /// <returns>The mask.</returns>
    private static GlyphMask MakeLcd(byte[] coverage, in MaskArea area, uint color)
    {
        var added = new byte[area.Width * area.Height * BgraBytes];
        var cut = new byte[added.Length];
        var rowLength = area.Width * SubPixels;
        for (var y = 0; y < area.Height; y++)
        {
            var row = coverage.AsSpan(y * rowLength, rowLength);
            for (var x = 0; x < area.Width; x++)
            {
                var offset = ((y * area.Width) + x) * BgraBytes;
                WritePixel(row, x, color, added.AsSpan(offset, BgraBytes), cut.AsSpan(offset, BgraBytes));
            }
        }

        var info = new SKImageInfo(area.Width, area.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        return new(SKImage.FromPixelCopy(info, added), SKImage.FromPixelCopy(info, cut), area.Left, area.Top, added.Length + cut.Length);
    }

    /// <summary>Works out one LCD pixel: the colour it adds and how much of the page's colour it leaves.</summary>
    /// <param name="row">The sub-pixel coverage of the pixel's row.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="color">The text colour as 0xRRGGBB.</param>
    /// <param name="added">Receives the BGRA colour the glyph adds.</param>
    /// <param name="cut">Receives the BGRA share of the page's colour that stays.</param>
    private static void WritePixel(ReadOnlySpan<byte> row, int x, uint color, Span<byte> added, Span<byte> cut)
    {
        for (var channel = 0; channel < SubPixels; channel++)
        {
            var covered = Filtered(row, (x * SubPixels) + channel);
            var slot = SubPixels - 1 - channel;
            var text = (int)((color >> (slot * ChannelBits)) & ChannelMax);
            added[slot] = (byte)((text * covered) / ChannelMax);
            cut[slot] = (byte)(ChannelMax - covered);
        }

        added[SubPixels] = ChannelMax;
        cut[SubPixels] = ChannelMax;
    }

    /// <summary>Softens the coverage around a sub-pixel with the LCD filter.</summary>
    /// <param name="row">The sub-pixel coverage of a row.</param>
    /// <param name="center">The sub-pixel to soften.</param>
    /// <returns>The coverage, 0 to 255.</returns>
    private static int Filtered(ReadOnlySpan<byte> row, int center)
    {
        var total = 0;
        for (var tap = 0; tap < LcdFilter.Length; tap++)
        {
            var index = center + tap - FilterReach;
            if ((uint)index < (uint)row.Length)
            {
                total += LcdFilter[tap] * row[index];
            }
        }

        return Math.Min(ChannelMax, total / FilterTotal);
    }

    /// <summary>Where a mask sits and how big it is.</summary>
    /// <param name="Left">The offset of the mask's left side from the glyph's pixel.</param>
    /// <param name="Top">The offset of the mask's top from the glyph's pixel.</param>
    /// <param name="Width">The width in the units of the pixels being made.</param>
    /// <param name="Height">The height in pixels.</param>
    private readonly record struct MaskArea(int Left, int Top, int Width, int Height);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// Makes the picture of a signature that came from a picture file, as the signature window shows it: the picture scaled
/// to fit, on white paper, so what the user sees is what goes on the page.
/// </summary>
internal static class MarkFrames
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Where red sits in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The largest value of a channel.</summary>
    private const int ChannelMax = 255;

    /// <summary>What a rounded division by <see cref="ChannelMax"/> adds first.</summary>
    private const int HalfChannel = 127;

    /// <summary>The share of the room the picture fills, which leaves a margin of paper around it.</summary>
    private const double Fill = 0.9;

    /// <summary>Gets the picture of a mark, making it again only when the mark or the room changed.</summary>
    /// <param name="surfaces">Makes the picture's surface.</param>
    /// <param name="frame">The picture made last time.</param>
    /// <param name="mark">The mark to show, or <see langword="null"/>.</param>
    /// <param name="room">The room the picture has, in device pixels.</param>
    /// <returns>The frame, whose picture is <see langword="null"/> when the mark is not a picture.</returns>
    internal static MarkFrame Get(IRenderSurfaceFactory surfaces, MarkFrame frame, SignatureMark? mark, SurfaceSize room)
    {
        if (mark is not { Style: SignatureMarkStyle.Image, IsValid: true } || room.Width <= 0 || room.Height <= 0)
        {
            Release(frame);
            return MarkFrame.Empty;
        }

        if (ReferenceEquals(frame.Mark, mark) && frame.Surface is { } kept && kept.Width == room.Width && kept.Height == room.Height)
        {
            return frame;
        }

        Release(frame);
        var surface = surfaces.Create(room.Width, room.Height);
        return surface.Write(mark, static (RenderTarget target, in SignatureMark shown) => Draw(shown, target))
            ? new(surface, mark)
            : Dropped(surface);
    }

    /// <summary>Gives the picture's surface back.</summary>
    /// <param name="frame">The frame.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Release(MarkFrame frame) => frame.Surface?.Dispose();

    /// <summary>Gives back a surface that could not be written.</summary>
    /// <param name="surface">The surface.</param>
    /// <returns>The frame with no picture.</returns>
    private static MarkFrame Dropped(IRenderSurface surface)
    {
        surface.Dispose();
        return MarkFrame.Empty;
    }

    /// <summary>Draws the mark's picture, scaled to fit and centred, over white paper.</summary>
    /// <param name="mark">The mark, whose pixels are BGRA with straight alpha.</param>
    /// <param name="target">The surface's pixels.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool Draw(SignatureMark mark, RenderTarget target)
    {
        var sourceWidth = (int)mark.Width;
        var sourceHeight = (int)mark.Height;
        var source = mark.Pixels.Span;
        var scale = Math.Min(target.Width * Fill / sourceWidth, target.Height * Fill / sourceHeight);
        var width = Math.Max(1, (int)(sourceWidth * scale));
        var height = Math.Max(1, (int)(sourceHeight * scale));
        var left = (target.Width - width) >> 1;
        var top = (target.Height - height) >> 1;
        for (var y = 0; y < target.Height; y++)
        {
            var row = target.Pixels.Slice(y * target.Stride, target.Width * BytesPerPixel);
            row.Fill(ChannelMax);
            if (y < top || y >= top + height)
            {
                continue;
            }

            // Each pixel shown is the mean of the block of the picture it covers, so a large scan keeps its thin strokes.
            var firstRow = (y - top) * sourceHeight / height;
            var lastRow = Math.Max(firstRow + 1, (y - top + 1) * sourceHeight / height);
            for (var x = 0; x < width; x++)
            {
                var firstColumn = x * sourceWidth / width;
                var lastColumn = Math.Max(firstColumn + 1, (x + 1) * sourceWidth / width);
                WriteOverPaper(row.Slice((left + x) * BytesPerPixel, BytesPerPixel), source, sourceWidth, new(firstColumn, firstRow, lastColumn, lastRow));
            }
        }

        return true;
    }

    /// <summary>Writes the mean of a block of the picture over white paper.</summary>
    /// <param name="pixel">The four bytes of the pixel shown.</param>
    /// <param name="source">The picture's pixels.</param>
    /// <param name="sourceWidth">The picture's width in pixels.</param>
    /// <param name="block">The block of the picture the pixel covers.</param>
    private static void WriteOverPaper(Span<byte> pixel, ReadOnlySpan<byte> source, int sourceWidth, in PixelBlock block)
    {
        long blue = 0;
        long green = 0;
        long red = 0;
        long alpha = 0;
        for (var y = block.Top; y < block.Bottom; y++)
        {
            for (var x = block.Left; x < block.Right; x++)
            {
                var at = ((y * sourceWidth) + x) * BytesPerPixel;
                int a = source[at + BytesPerPixel - 1];
                blue += source[at] * a;
                green += source[at + 1] * a;
                red += source[at + RedOffset] * a;
                alpha += a;
            }
        }

        // The sums are colour times alpha, so dividing by the count gives the colour already weighted, which goes over the paper.
        var count = (long)(block.Right - block.Left) * (block.Bottom - block.Top);
        var paper = ChannelMax - (alpha / count);
        pixel[0] = OverPaper(blue, count, paper);
        pixel[1] = OverPaper(green, count, paper);
        pixel[RedOffset] = OverPaper(red, count, paper);
        pixel[BytesPerPixel - 1] = ChannelMax;
    }

    /// <summary>Puts one weighted channel over white paper.</summary>
    /// <param name="weighted">The sum of the channel times alpha over the block.</param>
    /// <param name="count">The pixels in the block.</param>
    /// <param name="paper">How much of the paper shows through, from 0 to 255.</param>
    /// <returns>The channel shown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte OverPaper(long weighted, long count, long paper) =>
        (byte)Math.Min(ChannelMax, (((weighted / count) + HalfChannel) / ChannelMax) + paper);

    /// <summary>A block of a picture's pixels.</summary>
    /// <param name="Left">The first column.</param>
    /// <param name="Top">The first row.</param>
    /// <param name="Right">The column after the last.</param>
    /// <param name="Bottom">The row after the last.</param>
    private readonly record struct PixelBlock(int Left, int Top, int Right, int Bottom);
}

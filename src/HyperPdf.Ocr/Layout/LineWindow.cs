// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Layout;

/// <summary>The part of a page cut out to be read as one line.</summary>
/// <param name="Left">The left edge, in pixels.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("LineWindow: ({Left},{Top}) {Width} by {Height}")]
public readonly record struct LineWindow(int Left, int Top, int Width, int Height)
{
    /// <summary>The share of a line's height added as a margin on each side.</summary>
    private const double MarginShare = 0.1;

    /// <summary>The smallest margin, in pixels.</summary>
    private const int MinMargin = 2;

    /// <summary>The largest margin, in pixels.</summary>
    private const int MaxMargin = 12;

    /// <summary>Gets the number of pixels in the window.</summary>
    public int Pixels => Width * Height;

    /// <summary>
    /// Makes the window for a line: the line's box with a margin, and at least as tall as a line of ordinary text so a
    /// line of short letters is not blown up when it is scaled to the height the model reads.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="minimumHeight">The shortest the window may be, in pixels.</param>
    /// <param name="pageWidth">The page width, in pixels.</param>
    /// <param name="pageHeight">The page height, in pixels.</param>
    /// <returns>The window, kept inside the page.</returns>
    public static LineWindow For(TextLine line, int minimumHeight, int pageWidth, int pageHeight)
    {
        var margin = Math.Clamp((int)(line.Height * MarginShare), MinMargin, MaxMargin);
        var top = line.Top - margin;
        var bottom = line.Bottom + margin;
        var shortfall = minimumHeight - (bottom - top + 1);
        if (shortfall > 0)
        {
            top -= shortfall >> 1;
            bottom += shortfall - (shortfall >> 1);
        }

        var left = Math.Max(0, line.Left - margin);
        var right = Math.Min(pageWidth - 1, line.Right + margin);
        top = Math.Max(0, top);
        bottom = Math.Min(pageHeight - 1, bottom);
        return new(left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>Copies the window's pixels out of a page.</summary>
    /// <param name="page">The page's pixels.</param>
    /// <param name="pageWidth">The page width.</param>
    /// <param name="destination">Receives <see cref="Pixels"/> bytes.</param>
    public void CopyFrom(ReadOnlySpan<byte> page, int pageWidth, Span<byte> destination)
    {
        for (var y = 0; y < Height; y++)
        {
            page.Slice(((Top + y) * pageWidth) + Left, Width).CopyTo(destination.Slice(y * Width, Width));
        }
    }
}

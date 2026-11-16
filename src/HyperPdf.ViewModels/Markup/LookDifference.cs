// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Compares how two pages look by drawing both small and counting the pixels that differ.</summary>
internal static class LookDifference
{
    /// <summary>The width, in pixels, both pages are drawn at.</summary>
    internal const int DrawnWidth = 240;

    /// <summary>The bytes of one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The bytes of one row of the drawn pages.</summary>
    private const int Stride = DrawnWidth * BytesPerPixel;

    /// <summary>How far a channel must differ, out of 255, for the pixel to count as changed; it ignores the faint differences of smoothing.</summary>
    private const int ChannelThreshold = 40;

    /// <summary>How many points two pages' sizes may differ by before the pages count as different sizes.</summary>
    private const float SizeTolerance = 1F;

    /// <summary>Measures how much of a page looks different.</summary>
    /// <param name="current">The open document.</param>
    /// <param name="other">The document to compare it with.</param>
    /// <param name="page">The zero based page, which both documents have.</param>
    /// <returns>The share of the page's pixels that differ, from 0 to 1; 1 when the pages are different sizes.</returns>
    internal static double Measure(IDocument current, IDocument other, int page)
    {
        var size = current.GetPageSize(page);
        var otherSize = other.GetPageSize(page);
        if (MathF.Abs(size.Width - otherSize.Width) > SizeTolerance || MathF.Abs(size.Height - otherSize.Height) > SizeTolerance)
        {
            return 1;
        }

        if (!(size.Width > 0) || !(size.Height > 0))
        {
            return 0;
        }

        var scale = DrawnWidth / size.Width;
        var height = Math.Max(1, (int)MathF.Ceiling(size.Height * scale));
        var first = new byte[Stride * height];
        var second = new byte[Stride * height];
        var info = new PageRenderInfo(page, scale, PageRotation.None, 0, 0, RenderFlags.None);
        var target = new RenderTarget(first, DrawnWidth, height, Stride);
        var otherTarget = new RenderTarget(second, DrawnWidth, height, Stride);
        var drawn = current.Render(info, target) && other.Render(info, otherTarget);
        return drawn ? ShareChanged(first, second) : 0;
    }

    /// <summary>Counts the pixels two pictures differ in.</summary>
    /// <param name="first">The pixels of the first picture, as BGRA.</param>
    /// <param name="second">The pixels of the second picture, as BGRA.</param>
    /// <returns>The share of pixels in which any channel differs by more than the threshold, from 0 to 1.</returns>
    internal static double ShareChanged(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var pixels = Math.Min(first.Length, second.Length) / BytesPerPixel;
        if (pixels == 0)
        {
            return 0;
        }

        var changed = 0;
        for (var i = 0; i < pixels; i++)
        {
            var at = i * BytesPerPixel;
            if (IsChanged(first.Slice(at, BytesPerPixel), second.Slice(at, BytesPerPixel)))
            {
                changed++;
            }
        }

        return (double)changed / pixels;
    }

    /// <summary>Tells whether two pixels differ.</summary>
    /// <param name="first">The first pixel's four channels.</param>
    /// <param name="second">The second pixel's four channels.</param>
    /// <returns><see langword="true"/> when a channel differs by more than the threshold.</returns>
    private static bool IsChanged(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        for (var channel = 0; channel < BytesPerPixel; channel++)
        {
            if (Math.Abs(first[channel] - second[channel]) > ChannelThreshold)
            {
                return true;
            }
        }

        return false;
    }
}

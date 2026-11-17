// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Windowing.Rendering;

/// <summary>A clip rectangle in whole pixels inside a frame, as the scissor of a GPU or a loop over rows wants it.</summary>
/// <param name="Left">The first column.</param>
/// <param name="Top">The first row.</param>
/// <param name="Right">One past the last column.</param>
/// <param name="Bottom">One past the last row.</param>
[DebuggerDisplay("PixelClip ({Left}, {Top}) to ({Right}, {Bottom})")]
public readonly record struct PixelClip(int Left, int Top, int Right, int Bottom)
{
    /// <summary>Gets the width of the rectangle.</summary>
    public int Width => Right - Left;

    /// <summary>Gets the height of the rectangle.</summary>
    public int Height => Bottom - Top;

    /// <summary>Makes the pixels a clip covers: the whole pixels it touches, kept inside the frame.</summary>
    /// <param name="area">The clip in frame pixels.</param>
    /// <param name="width">The frame width.</param>
    /// <param name="height">The frame height.</param>
    /// <returns>The pixels, which may be empty when the clip lies outside the frame.</returns>
    public static PixelClip Of(in LayoutRect area, int width, int height)
    {
        var left = Math.Clamp((int)Math.Floor(area.X), 0, width);
        var top = Math.Clamp((int)Math.Floor(area.Y), 0, height);
        var right = Math.Clamp((int)Math.Ceiling(area.X + area.Width), left, width);
        var bottom = Math.Clamp((int)Math.Ceiling(area.Y + area.Height), top, height);
        return new(left, top, right, bottom);
    }
}

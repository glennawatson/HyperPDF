// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>The size of a decoded video picture.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("FrameSize: {Width}x{Height}")]
public readonly record struct FrameSize(int Width, int Height)
{
    /// <summary>The bytes in each pixel: blue, green, red and alpha.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The widest picture decoded, in pixels. A larger video is scaled down.</summary>
    private const int MaxFrameWidth = 640;

    /// <summary>The smallest side of a decoded picture, in pixels.</summary>
    private const int MinSide = 2;

    /// <summary>Gets a value indicating whether the picture has no pixels.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Gets the bytes in one picture.</summary>
    public int ByteCount => IsEmpty ? 0 : Width * Height * BytesPerPixel;

    /// <summary>Works out the size of the pictures to show: the video's size, scaled down to fit and made even.</summary>
    /// <param name="width">The video's width.</param>
    /// <param name="height">The video's height.</param>
    /// <returns>The size to show; empty when the video has no size.</returns>
    public static FrameSize Fit(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return default;
        }

        var scale = Math.Min(1.0, (double)MaxFrameWidth / width);
        return new(Even((int)(width * scale)), Even((int)(height * scale)));
    }

    /// <summary>Rounds a side down to an even number, because the colour planes of a picture have half the samples.</summary>
    /// <param name="side">The side.</param>
    /// <returns>An even side of at least two pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Even(int side) => Math.Max(MinSide, side & ~1);
}

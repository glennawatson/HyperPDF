// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>An owned, tightly packed top-left BGRA32 premultiplied page raster on a white background.</summary>
internal sealed record ComparisonRaster
{
    /// <summary>The channels in each pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>Initializes a new instance of the <see cref="ComparisonRaster"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="pixels">The tightly packed BGRA32 bytes to copy.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is not positive.</exception>
    /// <exception cref="ArgumentException">The pixel length does not match the dimensions.</exception>
    /// <exception cref="OverflowException">The dimensions exceed the supported buffer length.</exception>
    internal ComparisonRaster(int width, int height, ReadOnlyMemory<byte> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != checked(width * height * BytesPerPixel))
        {
            throw new ArgumentException("Pixel length must match the BGRA32 dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels.ToArray();
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the owned BGRA32 snapshot.</summary>
    internal ReadOnlyMemory<byte> Pixels { get; }
}

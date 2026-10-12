// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A locked 32-bit BGRA pixel buffer the engine renders into.</summary>
[DebuggerDisplay("RenderTarget: {Width} x {Height}")]
public readonly ref struct RenderTarget
{
    /// <summary>The size of a BGRA pixel in bytes.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Initializes a new instance of the <see cref="RenderTarget"/> struct.</summary>
    /// <param name="pixels">The pixel memory, at least <paramref name="stride"/> * <paramref name="height"/> bytes.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The number of bytes per scan line.</param>
    public RenderTarget(Span<byte> pixels, int width, int height, int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, width * BytesPerPixel);
        ArgumentOutOfRangeException.ThrowIfLessThan(pixels.Length, stride * height);
        Pixels = pixels;
        Width = width;
        Height = height;
        Stride = stride;
    }

    /// <summary>Gets the pixel memory.</summary>
    public Span<byte> Pixels { get; }

    /// <summary>Gets the width in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    public int Height { get; }

    /// <summary>Gets the number of bytes per scan line.</summary>
    public int Stride { get; }
}

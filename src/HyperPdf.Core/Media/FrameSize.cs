// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>The size of a decoded video picture.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("FrameSize: {Width}x{Height}")]
public readonly record struct FrameSize(int Width, int Height)
{
    /// <summary>The bytes in each pixel: blue, green, red and alpha.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Gets a value indicating whether the picture has no pixels.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Gets the bytes in one picture.</summary>
    public int ByteCount => IsEmpty ? 0 : Width * Height * BytesPerPixel;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Tests;

/// <summary>A box on a test page.</summary>
/// <param name="Left">The left column.</param>
/// <param name="Top">The top row.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
internal readonly record struct Region(int Left, int Top, int Width, int Height)
{
    /// <summary>Gets the last column of the box.</summary>
    public int Right => Left + Width - 1;

    /// <summary>Gets the last row of the box.</summary>
    public int Bottom => Top + Height - 1;

    /// <summary>Gets the number of pixels in the box.</summary>
    public int Pixels => Width * Height;
}

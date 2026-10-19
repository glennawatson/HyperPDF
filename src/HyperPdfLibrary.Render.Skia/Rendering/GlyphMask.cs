// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>A glyph drawn once into pixels, ready to be placed on a page at any whole pixel.</summary>
/// <param name="Image">For grey coverage, an alpha-only image the text colour tints. For LCD, the colour the glyph adds to each channel.</param>
/// <param name="Cut">For LCD, the image that dims each channel of the page where the glyph covers it; <see langword="null"/> for grey coverage.</param>
/// <param name="Left">The offset of the image's left side from the glyph's pixel, which can be negative.</param>
/// <param name="Top">The offset of the image's top from the glyph's pixel, which can be negative.</param>
/// <param name="Bytes">The memory the images hold.</param>
internal sealed record GlyphMask(SKImage Image, SKImage? Cut, int Left, int Top, long Bytes)
{
    /// <summary>Gets the width of the mask in pixels.</summary>
    internal int Width => Image.Width;

    /// <summary>Gets the height of the mask in pixels.</summary>
    internal int Height => Image.Height;
}

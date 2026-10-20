// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>How a glyph mask is drawn.</summary>
/// <param name="Quality">The quality settings.</param>
/// <param name="Color">The text colour with its opacity.</param>
/// <param name="Blend">The blend mode.</param>
/// <param name="AllowLcd">Whether LCD edges may be used, which needs a plain opaque colour on the page with no group around it.</param>
internal readonly record struct GlyphMaskStyle(PdfRenderQuality Quality, SKColor Color, SKBlendMode Blend, bool AllowLcd)
{
    /// <summary>The bits that shift the red channel of 0xRRGGBB into place.</summary>
    private const int RedShift = 16;

    /// <summary>The bits that shift the green channel of 0xRRGGBB into place.</summary>
    private const int GreenShift = 8;

    /// <summary>Gets the text colour as 0xRRGGBB.</summary>
    internal uint Rgb => ((uint)Color.Red << RedShift) | ((uint)Color.Green << GreenShift) | Color.Blue;
}

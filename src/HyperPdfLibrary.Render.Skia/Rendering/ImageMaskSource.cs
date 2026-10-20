// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>An image mask as the source of a glyph mask, such as the bitmap of a Type 3 glyph.</summary>
/// <param name="Image">The image whose alpha is the shape.</param>
/// <param name="Sampling">How the image's pixels are sampled when scaled.</param>
internal readonly record struct ImageMaskSource(SKImage Image, SKSamplingOptions Sampling) : IGlyphMaskSource
{
    /// <inheritdoc/>
    public SKRect Bounds => new(0, 0, Image.Width, Image.Height);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Draw(SKCanvas canvas, SKPaint paint) => canvas.DrawImage(Image, Bounds, Sampling, paint);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// What making glyph masks draws with, made once and reused for every mask: a paint, and a bitmap with a canvas over it that is
/// big enough for the largest mask, so no bitmap or canvas is made for each glyph.
/// </summary>
internal sealed class GlyphMaskScratch : IDisposable
{
    /// <summary>The sub-pixel columns of an LCD mask for each pixel, which sets how wide the bitmap is.</summary>
    private const int SubPixels = 3;

    /// <summary>Initializes a new instance of the <see cref="GlyphMaskScratch"/> class.</summary>
    internal GlyphMaskScratch()
    {
        Paint = new();
        Bitmap = new(new(GlyphMaskRaster.MaxSide * SubPixels, GlyphMaskRaster.MaxSide, SKColorType.Alpha8, SKAlphaType.Premul));
        Canvas = new(Bitmap);
    }

    /// <summary>Gets the paint glyphs are drawn with.</summary>
    internal SKPaint Paint { get; }

    /// <summary>Gets the bitmap glyphs are drawn into.</summary>
    internal SKBitmap Bitmap { get; }

    /// <summary>Gets the canvas over the bitmap.</summary>
    internal SKCanvas Canvas { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Canvas.Dispose();
        Bitmap.Dispose();
        Paint.Dispose();
    }
}

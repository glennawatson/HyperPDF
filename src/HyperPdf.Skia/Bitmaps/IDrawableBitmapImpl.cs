// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia.Platform;
using HyperPdf.Skia.Rendering;
using SkiaSharp;

namespace HyperPdf.Skia.Bitmaps;

/// <summary>Internal interface for bitmaps that know how to draw themselves directly to an <see cref = "SKCanvas"/>.</summary>
internal interface IDrawableBitmapImpl : IBitmapImpl
{
    /// <summary>Draws the bitmap to the active drawing context.</summary>
    /// <param name = "context">The drawing context.</param>
    /// <param name = "sourceRect">The source rectangle.</param>
    /// <param name = "destRect">The destination rectangle.</param>
    /// <param name = "samplingOptions">The Skia sampling options.</param>
    /// <param name = "paint">The paint settings.</param>
    void Draw(DrawingContextImpl context, SKRect sourceRect, SKRect destRect, SKSamplingOptions samplingOptions, SKPaint paint);
}

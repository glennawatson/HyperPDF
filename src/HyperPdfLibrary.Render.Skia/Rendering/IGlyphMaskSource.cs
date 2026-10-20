// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>What a glyph mask is drawn from: a glyph outline, or an image mask such as a bitmap glyph of a Type 3 font.</summary>
internal interface IGlyphMaskSource
{
    /// <summary>Gets the area the source covers in its own space.</summary>
    SKRect Bounds { get; }

    /// <summary>Draws the source in white, so the pixels it covers become coverage.</summary>
    /// <param name="canvas">The canvas of the mask being made, set up to map the source's space to the mask's pixels.</param>
    /// <param name="paint">The paint, which smooths edges unless sharp edges were asked for.</param>
    void Draw(SKCanvas canvas, SKPaint paint);
}

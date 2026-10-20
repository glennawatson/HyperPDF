// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>A glyph outline as the source of a glyph mask.</summary>
/// <param name="Path">The outline in the glyph's own space.</param>
internal readonly record struct OutlineMaskSource(SKPath Path) : IGlyphMaskSource
{
    /// <inheritdoc/>
    public SKRect Bounds => Path.Bounds;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Draw(SKCanvas canvas, SKPaint paint) => canvas.DrawPath(Path, paint);
}

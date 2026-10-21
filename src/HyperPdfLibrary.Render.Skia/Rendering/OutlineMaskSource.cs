// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>A glyph outline as the source of a glyph mask. A source made from a managed outline converts it only when a mask is made.</summary>
/// <param name="Path">The native outline, or <see langword="null"/> when the outline is converted on demand.</param>
/// <param name="Device">The device that converts and keeps the outline when there is no native outline.</param>
/// <param name="Font">The font that owns the managed outline.</param>
/// <param name="Code">The character code of the glyph.</param>
/// <param name="Outline">The managed outline.</param>
internal readonly record struct OutlineMaskSource(SKPath? Path, SkiaContentDevice? Device, PdfFont? Font, int Code, PdfPath? Outline) : IGlyphMaskSource
{
    /// <summary>Initializes a new instance of the <see cref="OutlineMaskSource"/> struct for a native outline.</summary>
    /// <param name="path">The outline in the glyph's own space.</param>
    internal OutlineMaskSource(SKPath path)
        : this(path, null, null, 0, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="OutlineMaskSource"/> struct for a managed outline.</summary>
    /// <param name="device">The device that converts and keeps the outline.</param>
    /// <param name="font">The font that owns the outline.</param>
    /// <param name="code">The character code of the glyph.</param>
    /// <param name="outline">The managed outline in the glyph's own space.</param>
    internal OutlineMaskSource(SkiaContentDevice device, PdfFont font, int code, PdfPath outline)
        : this(null, device, font, code, outline)
    {
    }

    /// <inheritdoc/>
    public SKRect Bounds => Path is { } path ? path.Bounds : Device!.OutlineBounds(Font!, Code, Outline!);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Draw(SKCanvas canvas, SKPaint paint)
    {
        if (Path is { } path)
        {
            canvas.DrawPath(path, paint);
            return;
        }

        Device!.DrawOutline(Font!, Code, Outline!, canvas, paint);
    }
}

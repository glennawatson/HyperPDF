// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>What a shape is painted with: one colour, or a shader that gives a colour for each pixel.</summary>
/// <param name="Color">The premultiplied colour with blue in the lowest byte, used when there is no shader.</param>
/// <param name="Shader">Which shader of the canvas gives the colours, or none for one colour.</param>
[DebuggerDisplay("RasterPaint: {Color}")]
internal readonly record struct RasterPaint(uint Color, RasterShaderKind Shader)
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>The shift of the red byte of a colour written as red, green and blue.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte.</summary>
    private const int GreenShift = 8;

    /// <summary>The shift of the alpha byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The mask of one byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>Gets how the paint mixes with what is under it.</summary>
    internal HyperPdfLibrary.Graphics.PdfBlendMode Blend { get; init; }

    /// <summary>Makes a paint of one colour.</summary>
    /// <param name="rgb">The colour as red, green and blue bytes, red highest.</param>
    /// <param name="alpha">How opaque it is, where 1 is fully.</param>
    /// <returns>The paint.</returns>
    internal static RasterPaint Solid(uint rgb, float alpha)
    {
        var opacity = (uint)MathF.Round(Math.Clamp(alpha, 0F, 1F) * Full);
        var pixel = ((rgb >> RedShift) & ByteMask) << RedShift | ((rgb >> GreenShift) & ByteMask) << GreenShift | (rgb & ByteMask) | (ByteMask << AlphaShift);
        return new(SpanBlender.Scale(pixel, opacity), RasterShaderKind.None);
    }
}

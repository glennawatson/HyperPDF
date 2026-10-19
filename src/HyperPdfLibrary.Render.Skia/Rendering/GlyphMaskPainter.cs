// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Draws small text from glyph masks onto a recording that serves one page matrix. A mask is looked up by glyph, size and
/// position within a pixel, made once when missing, and drawn at a whole device pixel, so repeated glyphs cost a lookup and one
/// image draw. Each recording device owns one painter, and the masks it draws from are shared by every device of the backend.
/// </summary>
internal sealed class GlyphMaskPainter : IDisposable
{
    /// <summary>The paint a mask is drawn with.</summary>
    private readonly SKPaint _paint = new() { IsAntialias = false };

    /// <summary>The masks every device of the backend shares.</summary>
    private readonly SkiaGlyphMasks _masks;

    /// <summary>Initializes a new instance of the <see cref="GlyphMaskPainter"/> class.</summary>
    /// <param name="masks">The masks every device of the backend shares.</param>
    internal GlyphMaskPainter(SkiaGlyphMasks masks) => _masks = masks;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _paint.Dispose();

    /// <summary>Draws one glyph from its mask.</summary>
    /// <param name="canvas">The recording's canvas.</param>
    /// <param name="deviceInverse">The matrix from device pixels back to the recording's space.</param>
    /// <param name="glyph">The glyph: its font, code, outline and the matrix from the outline to device pixels.</param>
    /// <param name="style">The quality settings, text colour and how it is drawn.</param>
    /// <returns><see langword="true"/> when the glyph was drawn or has nothing to draw; <see langword="false"/> when it must be filled from its outline.</returns>
    internal bool TryDraw(SKCanvas canvas, SKMatrix deviceInverse, in GlyphMaskGlyph glyph, in GlyphMaskStyle style)
    {
        if (!GlyphMaskPlacement.TryCreate(glyph.ToDevice, style.Quality.TextHinting, out var placement))
        {
            return false;
        }

        var edges = EdgesOf(style);
        var key = new GlyphMaskKey(
            glyph.Font,
            glyph.Code,
            placement.A,
            placement.B,
            placement.C,
            placement.D,
            placement.Shift,
            placement.SubX,
            placement.SubY,
            edges,
            (byte)style.Quality.EffectiveTextContrast,
            edges == GlyphMaskEdges.Lcd ? style.Rgb : 0);
        if (!FindOrMake(glyph, key, out var mask))
        {
            return false;
        }

        if (mask is not null)
        {
            Draw(canvas, deviceInverse, mask, placement, style, edges);
        }

        return true;
    }

    /// <summary>Chooses how a glyph's edges are made.</summary>
    /// <param name="style">The quality settings and how the text is drawn.</param>
    /// <returns>The edges.</returns>
    private static GlyphMaskEdges EdgesOf(in GlyphMaskStyle style) => style.Quality.TextEdges switch
    {
        PdfTextEdges.Sharp => GlyphMaskEdges.Sharp,
        PdfTextEdges.Lcd when style.AllowLcd => GlyphMaskEdges.Lcd,
        _ => GlyphMaskEdges.Smooth,
    };

    /// <summary>Finds a mask, or makes it and keeps it.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="key">What the mask is for.</param>
    /// <param name="mask">Receives the mask; <see langword="null"/> when the glyph paints nothing.</param>
    /// <returns><see langword="false"/> when the glyph is too large to draw from a mask.</returns>
    private bool FindOrMake(in GlyphMaskGlyph glyph, in GlyphMaskKey key, out GlyphMask? mask)
    {
        if (_masks.TryGet(key, out mask))
        {
            return true;
        }

        if (!GlyphMaskRaster.TryCreate(glyph.Path, key, out var created))
        {
            return false;
        }

        mask = created is null ? null : _masks.Add(key, created);
        return true;
    }

    /// <summary>Puts a mask on the recording at its whole device pixel.</summary>
    /// <param name="canvas">The recording's canvas.</param>
    /// <param name="deviceInverse">The matrix from device pixels back to the recording's space.</param>
    /// <param name="mask">The mask.</param>
    /// <param name="placement">Where the glyph's origin falls.</param>
    /// <param name="style">The colour and blend.</param>
    /// <param name="edges">How the mask's edges were made.</param>
    private void Draw(SKCanvas canvas, SKMatrix deviceInverse, GlyphMask mask, in GlyphMaskPlacement placement, in GlyphMaskStyle style, GlyphMaskEdges edges)
    {
        canvas.SetMatrix(deviceInverse);
        var x = placement.OriginX + mask.Left;
        var y = placement.OriginY + mask.Top;
        var area = new SKRect(x, y, x + mask.Width, y + mask.Height);
        var sampling = default(SKSamplingOptions);
        if (edges != GlyphMaskEdges.Lcd || mask.Cut is not { } cut)
        {
            _paint.Reset();
            _paint.Color = style.Color;
            _paint.BlendMode = style.Blend;
            canvas.DrawImage(mask.Image, area, sampling, _paint);
            return;
        }

        // The page's colour is dimmed where the glyph covers each channel, then the text's colour is added in the same places.
        _paint.Reset();
        _paint.BlendMode = SKBlendMode.Modulate;
        canvas.DrawImage(cut, area, sampling, _paint);
        _paint.BlendMode = SKBlendMode.Plus;
        canvas.DrawImage(mask.Image, area, sampling, _paint);
    }
}

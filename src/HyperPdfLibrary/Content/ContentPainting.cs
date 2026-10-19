// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Hands paint to the device after the output-stage keys of the graphics state have acted on it: the transfer function maps
/// the final colour (ISO 32000-2 10.5), overprint preview picks the blend (11.7.4), hidden colorants are left out, and a
/// collector is told which colorants are painted. A graphics state that needs none of these goes to the device untouched.
/// </summary>
internal static class ContentPainting
{
    /// <summary>The mask of a text render mode's fill and stroke bits; modes 4 to 7 repeat 0 to 3 and add a clip.</summary>
    private const int RenderModeMask = 3;

    /// <summary>The text render modes that fill, as a bit set over modes 0 to 3: 0 and 2.</summary>
    private const int FillModes = 0b0101;

    /// <summary>The text render modes that stroke, as a bit set over modes 0 to 3: 1 and 2.</summary>
    private const int StrokeModes = 0b0110;

    /// <summary>Fills a path.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="path">The path in user space.</param>
    /// <param name="evenOdd">Whether the even-odd rule applies.</param>
    /// <param name="state">The graphics state.</param>
    internal static void Fill(ContentInterpreter self, PdfPath path, bool evenOdd, ref GraphicsState state)
    {
        if (ContentPainting.IsPlain(self, ref state))
        {
            self.Device.Fill(path, evenOdd, ref state);
            return;
        }

        var painted = state;
        if (ContentPainting.Prepare(self, ref painted, true, false))
        {
            self.Device.Fill(path, evenOdd, ref painted);
        }
    }

    /// <summary>Strokes a path.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="path">The path in user space.</param>
    /// <param name="state">The graphics state.</param>
    internal static void Stroke(ContentInterpreter self, PdfPath path, ref GraphicsState state)
    {
        if (ContentPainting.IsPlain(self, ref state))
        {
            self.Device.Stroke(path, ref state);
            return;
        }

        var painted = state;
        if (ContentPainting.Prepare(self, ref painted, false, true))
        {
            self.Device.Stroke(path, ref painted);
        }
    }

    /// <summary>Draws a glyph, filled, stroked or both as the text render mode says.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="state">The graphics state.</param>
    internal static void DrawGlyph(ContentInterpreter self, in GlyphEvent glyph, ref GraphicsState state)
    {
        if (ContentPainting.IsPlain(self, ref state))
        {
            self.Device.DrawGlyph(glyph, ref state);
            return;
        }

        var mode = 1 << (state.RenderMode & ContentPainting.RenderModeMask);
        var painted = state;
        if (ContentPainting.Prepare(self, ref painted, (mode & ContentPainting.FillModes) != 0, (mode & ContentPainting.StrokeModes) != 0))
        {
            self.Device.DrawGlyph(glyph, ref painted);
        }
    }

    /// <summary>Draws a stencil mask, which paints the fill colour.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="image">The mask.</param>
    /// <param name="smooth">Whether the mask asks for smoothing.</param>
    /// <param name="state">The graphics state.</param>
    internal static void DrawMask(ContentInterpreter self, IPdfRenderImage image, bool smooth, ref GraphicsState state)
    {
        if (ContentPainting.IsPlain(self, ref state))
        {
            self.Device.DrawImage(image, true, smooth, ref state);
            return;
        }

        var painted = state;
        if (ContentPainting.Prepare(self, ref painted, true, false))
        {
            self.Device.DrawImage(image, true, smooth, ref painted);
        }
    }

    /// <summary>Paints a shading, whose transfer function is already part of its colour space.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="shading">The shading.</param>
    /// <param name="state">The graphics state.</param>
    internal static void PaintShading(ContentInterpreter self, PdfShading shading, ref GraphicsState state)
    {
        if (self.Colorants is null && self.Cache.HiddenColorants.Length == 0)
        {
            self.Device.PaintShading(shading, ref state);
            return;
        }

        self.Colorants?.Painted(shading.ColorSpace, Vector4.One, false);
        if (ContentColorants.AnyVisible(self.Cache.HiddenColorants, shading.ColorSpace))
        {
            self.Device.PaintShading(shading, ref state);
        }
    }

    /// <summary>Determines whether a graphics state reaches the device unchanged.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="state">The graphics state.</param>
    /// <returns><see langword="true"/> when no output-stage key, hidden colorant or collector applies.</returns>
    private static bool IsPlain(ContentInterpreter self, ref GraphicsState state) =>
        self.TextObjects is not null
        || (state.Transfer is null
            && self.Colorants is null
            && self.Cache.HiddenColorants.Length == 0
            && !(self.Cache.SimulateOverprint && (state.FillOverprint || state.StrokeOverprint)));

    /// <summary>Applies the output-stage keys to a copy of the graphics state.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="state">The copy, which changes.</param>
    /// <param name="fills">Whether the paint uses the fill colour.</param>
    /// <param name="strokes">Whether the paint uses the stroke colour.</param>
    /// <returns><see langword="false"/> when hidden colorants leave nothing to paint.</returns>
    private static bool Prepare(ContentInterpreter self, ref GraphicsState state, bool fills, bool strokes)
    {
        var rendering = state.ColorRendering;
        var blend = ContentPainting.Blend(self, ref state, fills, strokes);
        var transfer = state.Transfer;
        if (fills)
        {
            var fill = state.Fill;
            ContentColorants.Report(self, in fill);
            if (!ContentColorants.Hide(self, ref fill, rendering))
            {
                return false;
            }

            state.Fill = ContentPainting.Transfer(transfer, fill);
        }

        if (strokes)
        {
            var stroke = state.Stroke;
            ContentColorants.Report(self, in stroke);
            if (!ContentColorants.Hide(self, ref stroke, rendering))
            {
                return false;
            }

            state.Stroke = ContentPainting.Transfer(transfer, stroke);
        }

        state.BlendMode = blend;
        return true;
    }

    /// <summary>Picks the blend of a paint; a glyph filled and stroked overprints only when both its fill and its stroke do.</summary>
    /// <param name="self">The owned interpreter state.</param>
    /// <param name="state">The graphics state.</param>
    /// <param name="fills">Whether the paint uses the fill colour.</param>
    /// <param name="strokes">Whether the paint uses the stroke colour.</param>
    /// <returns>The blend.</returns>
    private static PdfBlendMode Blend(ContentInterpreter self, ref GraphicsState state, bool fills, bool strokes)
    {
        if (fills && strokes)
        {
            var fillBlend = ContentOverprint.BlendFor(self, ref state, false);
            return fillBlend == ContentOverprint.BlendFor(self, ref state, true) ? fillBlend : state.BlendMode;
        }

        return fills || strokes ? ContentOverprint.BlendFor(self, ref state, strokes) : state.BlendMode;
    }

    /// <summary>Maps a colour through a transfer function.</summary>
    /// <param name="transfer">The transfer function, or <see langword="null"/> for none.</param>
    /// <param name="colour">The colour.</param>
    /// <returns>The colour; a pattern colour is not mapped.</returns>
    private static ColorState Transfer(PdfTransfer? transfer, in ColorState colour) =>
        transfer is not null && colour.Pattern is null ? colour with { Rgb = transfer.Apply(colour.Rgb) } : colour;
}

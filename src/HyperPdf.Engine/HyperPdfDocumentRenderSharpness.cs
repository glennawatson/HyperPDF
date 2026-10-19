// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Engine;

/// <summary>Implements the page sharpness choices over the document's owned state and maps them to the library's render quality.</summary>
internal static class HyperPdfDocumentRenderSharpness
{
    /// <summary>Gets the choices the document draws with.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current choices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static RenderSharpness GetSharpness(HyperPdfDocument self) => Volatile.Read(ref self.Sharpness);

    /// <summary>Sets the choices and makes the next page drawn use them.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="value">The choices; values out of range are brought into range.</param>
    internal static void SetSharpness(HyperPdfDocument self, RenderSharpness value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var normal = value.Normalize();

        // Renders hold the page read lock while they make or use the renderer, so with the write lock no renderer can be
        // made with the old choices and published after the reset below.
        using var access = HyperPdfNavigation.EnterPageWrite(self);
        if (self.IsDisposed)
        {
            return;
        }

        Volatile.Write(ref self.Sharpness, normal);

        // The renderer records its pages with the choices it was made with, so the next page drawn needs a new one.
        HyperPdfRendering.ResetRenderer(self);
    }

    /// <summary>Maps the viewer's choices to the library's render quality.</summary>
    /// <param name="sharpness">The viewer's choices.</param>
    /// <returns>The library quality.</returns>
    internal static PdfRenderQuality ToQuality(RenderSharpness sharpness)
    {
        var normal = sharpness.Normalize();
        return new()
        {
            TextEdges = normal.TextEdges switch
            {
                TextEdgeChoice.Sharp => PdfTextEdges.Sharp,
                TextEdgeChoice.Lcd => PdfTextEdges.Lcd,
                _ => PdfTextEdges.Smooth,
            },
            GlyphMasks = normal.SmallTextMasks,
            TextHinting = normal.TextHinting switch
            {
                TextHintingChoice.Slight => PdfTextHinting.Slight,
                TextHintingChoice.Full => PdfTextHinting.Full,
                _ => PdfTextHinting.None,
            },
            TextContrast = normal.TextDarkness,
            SmoothGraphics = normal.SmoothGraphics,
            SmoothImages = normal.SmoothImages,
            StrokeAdjustment = normal.StrokeAdjustment switch
            {
                StrokeAdjustmentChoice.FollowPage => PdfStrokeAdjustment.Document,
                StrokeAdjustmentChoice.Always => PdfStrokeAdjustment.Always,
                _ => PdfStrokeAdjustment.Off,
            },
            MinimumLineWidth = normal.MinimumLineWidth,
            AreaImageReduction = normal.AreaImageReduction,
        };
    }
}

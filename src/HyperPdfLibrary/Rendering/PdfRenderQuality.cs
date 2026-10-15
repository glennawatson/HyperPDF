// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// How edges, thin lines and scaled images are drawn. The settings are recorded into the page pictures, so renderers with
/// equal settings share their caches and a renderer with other settings records its own pictures.
/// </summary>
[DebuggerDisplay("PdfRenderQuality: text {TextEdges}, lines {StrokeAdjustment}/{MinimumLineWidth}")]
public sealed record PdfRenderQuality
{
    /// <summary>The thickest minimum line width, in device pixels.</summary>
    private const float LineWidthLimit = 8;

    /// <summary>Gets the thickest minimum line width, in device pixels.</summary>
    public static float MaxMinimumLineWidth => LineWidthLimit;

    /// <summary>Gets the default settings: smooth edges everywhere, no line fitting, and area-averaged image reduction.</summary>
    public static PdfRenderQuality Default { get; } = new();

    /// <summary>Gets how the edges of text are drawn.</summary>
    public PdfTextEdges TextEdges { get; init; }

    /// <summary>Gets a value indicating whether the edges of paths, strokes and clips are smoothed. When false they are crisp.</summary>
    public bool SmoothGraphics { get; init; } = true;

    /// <summary>Gets a value indicating whether scaled images are smoothed. When false the nearest source pixel is used, which keeps 1-bit scans crisp.</summary>
    public bool SmoothImages { get; init; } = true;

    /// <summary>Gets whether strokes are fitted to whole pixels (ISO 32000-2 10.7.5).</summary>
    public PdfStrokeAdjustment StrokeAdjustment { get; init; }

    /// <summary>
    /// Gets the thinnest line drawn, in device pixels; 0 keeps the page's own widths (lines under one pixel then also get a
    /// hairline). A value above 0 widens every thinner stroke to this width.
    /// </summary>
    public float MinimumLineWidth { get; init; }

    /// <summary>Gets a value indicating whether images that shrink a lot are reduced by averaging the source pixels each output pixel covers.</summary>
    public bool AreaImageReduction { get; init; } = true;

    /// <summary>Gets the minimum line width clamped to its range, with a non-finite value counting as off.</summary>
    public float EffectiveMinimumLineWidth => float.IsFinite(MinimumLineWidth) ? Math.Clamp(MinimumLineWidth, 0, MaxMinimumLineWidth) : 0;

    /// <summary>Gets a value indicating whether the recording needs to know the exact page-to-device matrix.</summary>
    internal bool NeedsDeviceMatrix => StrokeAdjustment != PdfStrokeAdjustment.Off || MinimumLineWidth > 0;
}

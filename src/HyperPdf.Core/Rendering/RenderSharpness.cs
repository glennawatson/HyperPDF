// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Rendering;

/// <summary>
/// How sharp pages draw: the edges of text, thin lines and scaled images. The defaults match a plain smooth render. The
/// values are constructor parameters with defaults, so a saved file that leaves a choice out keeps that choice's default
/// on every target framework; the generated reader for init-only properties would set an omitted one to its type's
/// default on net10.0.
/// </summary>
/// <param name="TextEdges">How the edges of text are drawn.</param>
/// <param name="SmoothGraphics">Whether the edges of shapes and lines are smoothed.</param>
/// <param name="SmoothImages">Whether scaled images are smoothed; off keeps 1-bit scans crisp.</param>
/// <param name="StrokeAdjustment">Whether lines are fitted to whole pixels.</param>
/// <param name="MinimumLineWidth">The thinnest line drawn in device pixels, 0 (the page's own widths) to 3.</param>
/// <param name="AreaImageReduction">Whether images that shrink a lot are reduced by averaging, which keeps fine detail from sparkling.</param>
/// <param name="SmallTextMasks">Whether small text is drawn from cached glyph masks, which is quicker once a page's letters repeat.</param>
/// <param name="TextHinting">How small text is fitted to the pixel grid; any choice but none draws small text from glyph masks.</param>
/// <param name="TextDarkness">How much darker the edges of small text are drawn, from 0 to 3; above 0 draws small text from glyph masks.</param>
[DebuggerDisplay("RenderSharpness: text {TextEdges}, lines {StrokeAdjustment}/{MinimumLineWidth}")]
public sealed record RenderSharpness(
    TextEdgeChoice TextEdges = TextEdgeChoice.Smooth,
    bool SmoothGraphics = true,
    bool SmoothImages = true,
    StrokeAdjustmentChoice StrokeAdjustment = StrokeAdjustmentChoice.Off,
    int MinimumLineWidth = 0,
    bool AreaImageReduction = true,
    bool SmallTextMasks = false,
    TextHintingChoice TextHinting = TextHintingChoice.None,
    int TextDarkness = 0)
{
    /// <summary>The thickest minimum line width, in device pixels.</summary>
    private const int LineWidthLimit = 3;

    /// <summary>The highest text contrast level.</summary>
    private const int ContrastLimit = 3;

    /// <summary>Gets the default choices.</summary>
    public static RenderSharpness Default { get; } = new();

    /// <summary>Gets the thickest minimum line width in device pixels.</summary>
    public static int MaxMinimumLineWidth => LineWidthLimit;

    /// <summary>Gets the highest text darkness level.</summary>
    public static int MaxTextDarkness => ContrastLimit;

    /// <summary>Brings every value into its range, so a hand-edited or older settings file still gives a working choice.</summary>
    /// <returns>The choices with each value inside its range.</returns>
    public RenderSharpness Normalize() => this with
    {
        TextEdges = Enum.IsDefined(TextEdges) ? TextEdges : TextEdgeChoice.Smooth,
        StrokeAdjustment = Enum.IsDefined(StrokeAdjustment) ? StrokeAdjustment : StrokeAdjustmentChoice.Off,
        MinimumLineWidth = Math.Clamp(MinimumLineWidth, 0, LineWidthLimit),
        TextHinting = Enum.IsDefined(TextHinting) ? TextHinting : TextHintingChoice.None,
        TextDarkness = Math.Clamp(TextDarkness, 0, ContrastLimit),
    };
}

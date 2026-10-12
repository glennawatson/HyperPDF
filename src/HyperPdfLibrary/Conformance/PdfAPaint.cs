// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The parts of the graphics state that decide whether painting is transparent, saved and restored by <c>q</c> and <c>Q</c>.</summary>
/// <param name="FillAlpha">Whether the fill alpha (<c>/ca</c>) is below 1.</param>
/// <param name="StrokeAlpha">Whether the stroke alpha (<c>/CA</c>) is below 1.</param>
/// <param name="Blend">Whether the blend mode is other than Normal.</param>
/// <param name="SoftMask">Whether a soft mask is set.</param>
/// <param name="RenderMode">The text rendering mode.</param>
/// <param name="FillPattern">The tiling pattern fills use, or no name.</param>
/// <param name="StrokePattern">The tiling pattern strokes use, or no name.</param>
/// <param name="Font">The Type 3 font text is shown with, if any.</param>
internal readonly record struct PdfAPaint(
    bool FillAlpha,
    bool StrokeAlpha,
    bool Blend,
    bool SoftMask,
    int RenderMode,
    PdfName FillPattern,
    PdfName StrokePattern,
    PdfAShownFont Font)
{
    /// <summary>Gets a value indicating whether a fill painted now is transparent.</summary>
    internal bool FillsTransparent => FillAlpha || Blend || SoftMask;

    /// <summary>Gets a value indicating whether a stroke painted now is transparent.</summary>
    internal bool StrokesTransparent => StrokeAlpha || Blend || SoftMask;
}

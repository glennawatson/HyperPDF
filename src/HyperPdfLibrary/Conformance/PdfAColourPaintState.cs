// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The graphics state the PDF/A colour walk tracks through a content stream.</summary>
/// <param name="Fill">The fill colour space, or null when it could not be read.</param>
/// <param name="Stroke">The stroke colour space, or null when it could not be read.</param>
/// <param name="FillUnderlying">The underlying space of the last fill pattern space, for uncoloured patterns.</param>
/// <param name="StrokeUnderlying">The underlying space of the last stroke pattern space, for uncoloured patterns.</param>
/// <param name="RenderMode">The text rendering mode.</param>
/// <param name="OverprintStroke">Whether stroking overprints (<c>/OP</c>).</param>
/// <param name="OverprintFill">Whether filling overprints (<c>/op</c>).</param>
/// <param name="OverprintMode">The overprint mode (<c>/OPM</c>).</param>
/// <param name="ProcessColour">Whether colour operators apply; false in uncoloured patterns and in Type 3 glyphs after <c>d1</c>.</param>
/// <param name="Font">The current font resource; its value is null when no font is set.</param>
internal readonly record struct PdfAColourPaintState(
    PdfAColourSpace? Fill,
    PdfAColourSpace? Stroke,
    PdfAColourSpace? FillUnderlying,
    PdfAColourSpace? StrokeUnderlying,
    int RenderMode,
    bool OverprintStroke,
    bool OverprintFill,
    int OverprintMode,
    bool ProcessColour,
    PdfAResource Font);

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>Where and how a text or image watermark is drawn on each page. Sizes are in points (1/72 inch) of physical size.</summary>
[DebuggerDisplay("PdfWatermarkOptions: {Anchor}, {RotationDegrees} degrees, opacity {Opacity}, layer {LayerName}")]
public sealed record PdfWatermarkOptions
{
    /// <summary>The default distance from the page edge to a watermark in a corner, in points.</summary>
    private const float DefaultMargin = 36;

    /// <summary>Gets the options for an opaque, upright watermark in the middle of the page, in front of its content, without a layer.</summary>
    public static PdfWatermarkOptions Default { get; } = new();

    /// <summary>Gets where the watermark sits on the displayed page.</summary>
    public PdfWatermarkAnchor Anchor { get; init; } = PdfWatermarkAnchor.Center;

    /// <summary>Gets the distance from the page edge to a watermark in a corner, in points; the middle ignores it.</summary>
    public float Margin { get; init; } = DefaultMargin;

    /// <summary>Gets the turn of the watermark about its centre, in degrees counterclockwise on the displayed page.</summary>
    public float RotationDegrees { get; init; }

    /// <summary>
    /// Gets how opaque the watermark is, from 0 (clear) to 1 (solid). Less than 1 uses transparency, which a document that
    /// claims PDF/A-1 cannot have, so it is refused there.
    /// </summary>
    public float Opacity { get; init; } = 1;

    /// <summary>Gets whether the watermark is drawn behind the page's content instead of in front of it.</summary>
    public bool Behind { get; init; }

    /// <summary>
    /// Gets the name of an optional content group (ISO 32000-2 8.11) that controls the watermark, so a reader can switch it
    /// on and off; <see langword="null"/> for a watermark that is always drawn. A document that claims PDF/A-1 cannot have
    /// optional content, so this is refused there.
    /// </summary>
    public string? LayerName { get; init; }
}

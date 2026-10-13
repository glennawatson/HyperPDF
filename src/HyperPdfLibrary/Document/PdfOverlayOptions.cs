// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>How pages of another document are drawn on pages of a document.</summary>
[DebuggerDisplay("PdfOverlayOptions: {Placement}, {Fit}, layer {LayerName}")]
public sealed record PdfOverlayOptions
{
    /// <summary>Gets the options for an overlay in front of the page, at its lower left, without annotations or a layer.</summary>
    public static PdfOverlayOptions Default { get; } = new();

    /// <summary>Gets whether the pages are drawn in front of the page's content or behind it.</summary>
    public PdfOverlayPlacement Placement { get; init; } = PdfOverlayPlacement.Overlay;

    /// <summary>Gets how the overlay page is placed and sized on the target page.</summary>
    public PdfOverlayFit Fit { get; init; } = PdfOverlayFit.None;

    /// <summary>Gets the annotations of the overlay page that are drawn into it.</summary>
    public PdfAnnotationFilter Annotations { get; init; } = PdfAnnotationFilter.None;

    /// <summary>
    /// Gets the name of an optional content group (ISO 32000-2 8.11) that controls the overlay, so a reader can switch it
    /// on and off; <see langword="null"/> for an overlay that is always drawn. Overlays with the same name share one group.
    /// A document that claims PDF/A-1 cannot have optional content, so this is refused there.
    /// </summary>
    public string? LayerName { get; init; }
}

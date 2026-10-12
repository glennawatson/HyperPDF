// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>An annotation on a page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The annotation's index on the page; changes when annotations before it are removed.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Bounds">The bounds in page space (points, top-left origin).</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
/// <param name="Contents">The note text, or an empty string.</param>
/// <param name="Author">The author, or an empty string.</param>
[DebuggerDisplay("PageAnnotation: {Kind} on page {PageIndex}")]
public sealed record PageAnnotation(int PageIndex, int Index, AnnotationKind Kind, PageRect Bounds, uint Color, string Contents, string Author)
{
    /// <summary>Gets the line width in points of a drawing or shape, or 0 when it has no line.</summary>
    public float LineWidth { get; init; }

    /// <summary>Gets the text size in points of a text box or callout written here, or 0 when it is not known.</summary>
    public float FontSize { get; init; }

    /// <summary>Gets when the annotation was last changed, or <see langword="null"/> when the file does not say.</summary>
    public DateTimeOffset? Modified { get; init; }

    /// <summary>Gets a value indicating whether the annotation can be moved and resized; text markup follows its text instead.</summary>
    public bool IsMovable => Kind is not (AnnotationKind.Highlight or AnnotationKind.Underline or AnnotationKind.StrikeOut or AnnotationKind.Squiggly);
}

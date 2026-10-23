// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// How a paragraph is laid out. Every property is optional: an unset one is inherited from the style the paragraph uses, and
/// that style's from the style it is based on, so a format only names what it changes.
/// </summary>
[DebuggerDisplay("PdfParagraphFormat: {Alignment}, indent {LeftIndent}")]
public sealed record PdfParagraphFormat
{
    /// <summary>Gets how lines sit between the left and right edges.</summary>
    public PdfTextAlignment? Alignment { get; init; }

    /// <summary>Gets the space in points between the left edge of the text area and every line.</summary>
    public float? LeftIndent { get; init; }

    /// <summary>Gets the space in points between the right edge of the text area and every line.</summary>
    public float? RightIndent { get; init; }

    /// <summary>Gets how far the first line starts to the right of the left indent in points; a negative value hangs it to the left, as a list item's number does.</summary>
    public float? FirstLineIndent { get; init; }

    /// <summary>Gets the space in points above the paragraph. Where two paragraphs meet, the larger of the space after the first and the space before the second is used.</summary>
    public float? SpaceBefore { get; init; }

    /// <summary>Gets the space in points below the paragraph.</summary>
    public float? SpaceAfter { get; init; }

    /// <summary>Gets the line spacing, read by <see cref="LineSpacingRule"/>.</summary>
    public float? LineSpacing { get; init; }

    /// <summary>Gets how <see cref="LineSpacing"/> is read.</summary>
    public PdfLineSpacingRule? LineSpacingRule { get; init; }

    /// <summary>Gets a value indicating whether the paragraph is kept on one page when it fits on one.</summary>
    public bool? KeepTogether { get; init; }

    /// <summary>Gets a value indicating whether the paragraph stays on the same page as the start of the next one.</summary>
    public bool? KeepWithNext { get; init; }

    /// <summary>Gets a value indicating whether the paragraph starts a new page.</summary>
    public bool? PageBreakBefore { get; init; }

    /// <summary>Gets a value indicating whether a page never ends with the first line of a paragraph or starts with its last line alone.</summary>
    public bool? WidowControl { get; init; }

    /// <summary>Gets the lines drawn round the paragraph.</summary>
    public PdfBorders? Borders { get; init; }

    /// <summary>Gets the space between the border and the text.</summary>
    public PdfInsets? Padding { get; init; }

    /// <summary>Gets the colour painted behind the paragraph, inside its border.</summary>
    public PdfCanvasColor? Shading { get; init; }

    /// <summary>Gets the tab stops, which replace the ones the style is based on.</summary>
    public IReadOnlyList<PdfTabStop>? TabStops { get; init; }

    /// <summary>Gets the level of the paragraph in the document outline, 1 to 9, or 0 for body text that is not in the outline.</summary>
    public int? OutlineLevel { get; init; }
}

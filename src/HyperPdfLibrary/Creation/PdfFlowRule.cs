// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// A horizontal rule divider on a line of its own. The rule spans the available content width by default (or an explicit
/// <see cref="Width"/> clamped to the content width) and is wrapped in a layout artifact in tagged documents.
/// </summary>
[DebuggerDisplay("PdfFlowRule: {Thickness} pt, {Alignment}")]
public sealed record PdfFlowRule() : PdfFlowBlock
{
    /// <summary>Gets the stroke thickness of the rule in points; the default is 1 pt.</summary>
    public float Thickness { get; init; } = 1F;

    /// <summary>Gets the stroke colour of the rule; the default is black.</summary>
    public PdfRgbColor Color { get; init; } = PdfRgbColor.Black;

    /// <summary>
    /// Gets the width of the rule in points, or <see langword="null"/> to span the full available content width.
    /// Values wider than the available content width are clamped to fit within the page margins.
    /// </summary>
    public float? Width { get; init; }

    /// <summary>
    /// Gets the horizontal alignment of the rule within the available content width when <see cref="Width"/> is narrower
    /// than the content area; the default is <see cref="PdfTableAlignment.Left"/>.
    /// </summary>
    public PdfTableAlignment Alignment { get; init; } = PdfTableAlignment.Left;

    /// <summary>
    /// Gets the vertical space in points added above the rule when it does not start a page, or <see langword="null"/>
    /// to use the flow's default block spacing.
    /// </summary>
    public float? SpaceBefore { get; init; }

    /// <summary>Gets the vertical space in points added below the rule, or <see langword="null"/> to use the flow's default block spacing.</summary>
    public float? SpaceAfter { get; init; }
}

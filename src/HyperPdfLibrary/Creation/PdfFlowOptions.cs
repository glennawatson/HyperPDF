// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>The page size, margins and type of a flowed document.</summary>
/// <param name="BodyFont">The font of paragraphs and, unless <see cref="HeadingFont"/> is set, of headings.</param>
[DebuggerDisplay("PdfFlowOptions: {PageWidth} x {PageHeight}, body {BodySize} pt")]
public sealed record PdfFlowOptions(PdfCanvasFont BodyFont)
{
    /// <summary>Gets the page width in points; the default is A4.</summary>
    public float PageWidth { get; init; } = 595.28F;

    /// <summary>Gets the page height in points; the default is A4.</summary>
    public float PageHeight { get; init; } = 841.89F;

    /// <summary>Gets the margin on every side in points.</summary>
    public float Margin { get; init; } = 72F;

    /// <summary>Gets the size of body text in points.</summary>
    public float BodySize { get; init; } = 11F;

    /// <summary>Gets the line pitch as a multiple of the font size.</summary>
    public float LineSpacing { get; init; } = 1.4F;

    /// <summary>Gets the font of headings, or <see langword="null"/> to use <see cref="BodyFont"/>.</summary>
    public PdfCanvasFont? HeadingFont { get; init; }

    /// <summary>Gets a value indicating whether each page gets its number at the bottom, as a pagination artifact.</summary>
    public bool PageNumbers { get; init; }
}

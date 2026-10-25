// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>A horizontal line across the text area. It is decoration, not content, so a tagged document marks it as an artifact.</summary>
[DebuggerDisplay("PdfRule: {Thickness} pt")]
public sealed record PdfRule : PdfBlock
{
    /// <summary>The default thickness of the line in points.</summary>
    private const float DefaultThickness = 0.75F;

    /// <summary>The default grey level of the line.</summary>
    private const float DefaultGray = 0.5F;

    /// <summary>The default space above and below the line in points.</summary>
    private const float DefaultSpace = 6F;

    /// <summary>Gets the line's thickness in points.</summary>
    public float Thickness { get; init; } = DefaultThickness;

    /// <summary>Gets the line's colour.</summary>
    public PdfCanvasColor Color { get; init; } = PdfCanvasColor.Gray(DefaultGray);

    /// <summary>Gets the space above the line in points.</summary>
    public float SpaceBefore { get; init; } = DefaultSpace;

    /// <summary>Gets the space below the line in points.</summary>
    public float SpaceAfter { get; init; } = DefaultSpace;
}

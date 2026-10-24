// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>What a figure needs to be sized, aligned and drawn.</summary>
/// <param name="Width">The width asked for in points, or <see langword="null"/> to work it out.</param>
/// <param name="Height">The height asked for in points, or <see langword="null"/> to work it out.</param>
/// <param name="NaturalWidth">The width the figure has by itself in points, or zero when it has none and fills the text area.</param>
/// <param name="NaturalHeight">The height the figure has by itself in points, or zero when it has none.</param>
/// <param name="Alignment">Where the figure sits between the left and right edges.</param>
/// <param name="SpaceBefore">The space above in points.</param>
/// <param name="SpaceAfter">The space below in points.</param>
/// <param name="AlternativeText">The description for people who cannot see the figure, or <see langword="null"/> for decoration.</param>
/// <param name="Create">Makes the command that draws the figure in a box.</param>
[DebuggerDisplay("FigureSpec: {Width} x {Height}")]
internal sealed record FigureSpec(
    float? Width,
    float? Height,
    float NaturalWidth,
    float NaturalHeight,
    PdfTextAlignment Alignment,
    float SpaceBefore,
    float SpaceAfter,
    string? AlternativeText,
    Func<PdfCanvasBox, StructNode?, PdfArtifactKind, DrawCommand> Create);

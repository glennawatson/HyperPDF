// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// A frame: a box of blocks with its own width, borders, padding and shading that sits in the flow, is placed at a position on
/// the page, or floats to one edge with the text wrapping round it. Use one for a pull quote, a sidebar, a stamp or a note.
/// </summary>
/// <param name="Blocks">The frame's content.</param>
[DebuggerDisplay("PdfFrame: {Position}, {Wrap}")]
public sealed record PdfFrame(IReadOnlyList<PdfBlock> Blocks) : PdfBlock
{
    /// <summary>The default space above and below a frame in points.</summary>
    private const float DefaultSpace = 6F;

    /// <summary>Gets what the position of the frame is measured from.</summary>
    public PdfFramePosition Position { get; init; }

    /// <summary>Gets how the text round the frame behaves; a frame that is not in the flow never moves text and is always <see cref="PdfFrameWrap.None"/>.</summary>
    public PdfFrameWrap Wrap { get; init; } = PdfFrameWrap.TopAndBottom;

    /// <summary>Gets the width of the frame in points, or <see langword="null"/> for the whole text area.</summary>
    public float? Width { get; init; }

    /// <summary>Gets the height of the frame in points, or <see langword="null"/> for what its content needs.</summary>
    public float? Height { get; init; }

    /// <summary>Gets how far in points the frame is from the left of what its position is measured from, for a frame that is not in the flow.</summary>
    public float Left { get; init; }

    /// <summary>Gets how far in points the frame is below the top of what its position is measured from, for a frame that is not in the flow.</summary>
    public float Top { get; init; }

    /// <summary>Gets where a frame in the flow with top and bottom wrapping sits between the left and right edges.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Gets the lines drawn round the frame.</summary>
    public PdfBorders Borders { get; init; }

    /// <summary>Gets the space between the border and the content.</summary>
    public PdfInsets Padding { get; init; }

    /// <summary>Gets the colour behind the frame, or <see langword="null"/> for none.</summary>
    public PdfCanvasColor? Shading { get; init; }

    /// <summary>Gets the space above a frame in the flow in points.</summary>
    public float SpaceBefore { get; init; } = DefaultSpace;

    /// <summary>Gets the space below a frame in the flow in points; for a floating frame, the gap between it and the text beside it.</summary>
    public float SpaceAfter { get; init; } = DefaultSpace;
}

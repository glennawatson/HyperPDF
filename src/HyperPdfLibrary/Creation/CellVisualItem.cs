// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>One atomic visual item (a text line, an image, or a code block line) inside a table cell.</summary>
[DebuggerDisplay("CellVisualItem: {Kind}, {Width}x{Height} pt")]
internal sealed class CellVisualItem
{
    /// <summary>Initializes a new instance of the <see cref="CellVisualItem"/> class.</summary>
    /// <param name="kind">The item kind.</param>
    /// <param name="width">The item width in points.</param>
    /// <param name="height">The item height in points.</param>
    private CellVisualItem(CellItemKind kind, float width, float height)
    {
        Kind = kind;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the item kind.</summary>
    internal CellItemKind Kind { get; }

    /// <summary>Gets the item's horizontal advance in points.</summary>
    internal float Width { get; }

    /// <summary>Gets the item's vertical height in points.</summary>
    internal float Height { get; }

    /// <summary>Gets the styled segments when <see cref="Kind"/> is <see cref="CellItemKind.Text"/>.</summary>
    internal StyledSegment[] Segments { get; private init; } = [];

    /// <summary>Gets the maximum font ascent on the line when <see cref="Kind"/> is <see cref="CellItemKind.Text"/>.</summary>
    internal float Ascent { get; private init; }

    /// <summary>Gets the minimum font descent on the line when <see cref="Kind"/> is <see cref="CellItemKind.Text"/>.</summary>
    internal float Descent { get; private init; }

    /// <summary>Gets the image block when <see cref="Kind"/> is <see cref="CellItemKind.Image"/>.</summary>
    internal PdfFlowImage? Image { get; private init; }

    /// <summary>Gets the code block when <see cref="Kind"/> is <see cref="CellItemKind.Code"/>.</summary>
    internal PdfFlowCodeBlock? CodeBlock { get; private init; }

    /// <summary>Gets the resolved code font when <see cref="Kind"/> is <see cref="CellItemKind.Code"/>.</summary>
    internal PdfCanvasFont? CodeFont { get; private init; }

    /// <summary>Gets the tab-expanded code string when <see cref="Kind"/> is <see cref="CellItemKind.Code"/>.</summary>
    internal string CodeText { get; private init; } = string.Empty;

    /// <summary>Gets the code line slice when <see cref="Kind"/> is <see cref="CellItemKind.Code"/>.</summary>
    internal PdfTextLine CodeLine { get; private init; }

    /// <summary>Gets the code line pitch without box padding when <see cref="Kind"/> is <see cref="CellItemKind.Code"/>.</summary>
    internal float CodeLeading { get; private init; }

    /// <summary>Creates a text line item.</summary>
    /// <param name="segments">The styled segments on the line.</param>
    /// <param name="width">The total advance of the line.</param>
    /// <param name="height">The line pitch.</param>
    /// <param name="ascent">The maximum ascent on the line.</param>
    /// <param name="descent">The minimum descent on the line.</param>
    /// <returns>The visual item.</returns>
    internal static CellVisualItem CreateText(StyledSegment[] segments, float width, float height, float ascent, float descent) =>
        new(CellItemKind.Text, width, height) { Segments = segments, Ascent = ascent, Descent = descent };

    /// <summary>Creates an inline image item.</summary>
    /// <param name="image">The flow image.</param>
    /// <param name="width">The drawn width in points.</param>
    /// <param name="height">The drawn height in points.</param>
    /// <returns>The visual item.</returns>
    internal static CellVisualItem CreateImage(PdfFlowImage image, float width, float height) =>
        new(CellItemKind.Image, width, height) { Image = image };

    /// <summary>Creates a code block line item.</summary>
    /// <param name="block">The code block.</param>
    /// <param name="font">The resolved code font.</param>
    /// <param name="text">The tab-expanded code string.</param>
    /// <param name="line">The wrapped line range.</param>
    /// <param name="boxWidth">The outer width of the code box.</param>
    /// <param name="codeLeading">The single-line pitch of the code font.</param>
    /// <param name="height">The total vertical height of this line including any top/bottom box padding.</param>
    /// <returns>The visual item.</returns>
    internal static CellVisualItem CreateCode(
        PdfFlowCodeBlock block,
        PdfCanvasFont font,
        string text,
        PdfTextLine line,
        float boxWidth,
        float codeLeading,
        float height) =>
        new(CellItemKind.Code, boxWidth, height) { CodeBlock = block, CodeFont = font, CodeText = text, CodeLine = line, CodeLeading = codeLeading };
}

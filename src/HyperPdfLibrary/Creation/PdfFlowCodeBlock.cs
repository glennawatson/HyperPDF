// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// A preformatted code block. Line breaks and leading whitespace (including tabs expanded to <see cref="TabSpaces"/>) are
/// preserved, and the block is drawn with its own font, point size, padding, indentation, background fill and border.
/// In a tagged document the block is tagged as <c>Code</c> and its background and border are layout artifacts.
/// </summary>
/// <param name="Code">The preformatted source text.</param>
[DebuggerDisplay("PdfFlowCodeBlock: {Code}")]
public sealed record PdfFlowCodeBlock(string Code) : PdfFlowBlock
{
    /// <summary>The default code font size in points.</summary>
    private const float DefaultFontSize = 9.5F;

    /// <summary>The default line spacing multiplier.</summary>
    private const float DefaultLineSpacing = 1.35F;

    /// <summary>The default inner padding on every side in points.</summary>
    private const float DefaultPadding = 6F;

    /// <summary>The default number of spaces a tab expands to.</summary>
    private const int DefaultTabSpaces = 4;

    /// <summary>The default grey channel level of the code block background.</summary>
    private const float DefaultBackgroundShade = 0.95F;

    /// <summary>The default grey channel level of the code block border.</summary>
    private const float DefaultBorderShade = 0.8F;

    /// <summary>The default border stroke width in points.</summary>
    private const float DefaultBorderWidth = 0.5F;

    /// <summary>Gets the monospaced font, or <see langword="null"/> to use the flow's body font.</summary>
    public PdfCanvasFont? Font { get; init; }

    /// <summary>Gets the ordered fallback fonts for this code block, or <see langword="null"/> to inherit <see cref="PdfFlowOptions.FallbackFonts"/>.</summary>
    public IReadOnlyList<PdfCanvasFont>? FallbackFonts { get; init; }

    /// <summary>Gets the font size in points; the default is 9.5 pt.</summary>
    public float FontSize { get; init; } = DefaultFontSize;

    /// <summary>Gets the line pitch as a multiple of <see cref="FontSize"/>; the default is 1.35.</summary>
    public float LineSpacing { get; init; } = DefaultLineSpacing;

    /// <summary>Gets the padding inside the code block on every side, in points; the default is 6 pt.</summary>
    public float Padding { get; init; } = DefaultPadding;

    /// <summary>Gets the left indentation from the surrounding text margin or cell inset, in points; the default is zero.</summary>
    public float Indent { get; init; }

    /// <summary>Gets the number of spaces each tab character (<c>\t</c>) expands to; the default is 4.</summary>
    public int TabSpaces { get; init; } = DefaultTabSpaces;

    /// <summary>Gets the text colour; the default is black.</summary>
    public PdfRgbColor TextColor { get; init; } = PdfRgbColor.Black;

    /// <summary>Gets the background fill colour, or <see langword="null"/> for no background fill.</summary>
    public PdfRgbColor? BackgroundColor { get; init; } = new PdfRgbColor(DefaultBackgroundShade, DefaultBackgroundShade, DefaultBackgroundShade);

    /// <summary>Gets the border stroke, or <see langword="null"/> for no border.</summary>
    public PdfStroke? Border { get; init; } = new PdfStroke(DefaultBorderShade, DefaultBorderShade, DefaultBorderShade, DefaultBorderWidth);

    /// <summary>Gets how lines wider than the block's inner width are handled; the default is <see cref="PdfCodeOverflow.Wrap"/>.</summary>
    public PdfCodeOverflow Overflow { get; init; } = PdfCodeOverflow.Wrap;
}

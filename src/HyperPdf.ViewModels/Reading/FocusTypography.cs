// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Reading;

/// <summary>The sizes and spaces Focus Mode text is set in, worked out from the reader's settings. Every value is in pixels.</summary>
/// <param name="FontSize">The body text size.</param>
/// <param name="LineHeight">The body line height.</param>
/// <param name="ParagraphMargin">The space after a paragraph.</param>
/// <param name="HeadingSize">The heading text size.</param>
/// <param name="HeadingLineHeight">The heading line height.</param>
/// <param name="HeadingTopMargin">The space above a heading.</param>
/// <param name="HeadingBottomMargin">The space below a heading.</param>
/// <param name="ItemIndent">The indent of a list item.</param>
/// <param name="ItemBottomMargin">The space below a list item.</param>
/// <param name="SmallSize">The size of captions and footnotes.</param>
/// <param name="MaxWidth">The widest the text column may be.</param>
[DebuggerDisplay("FocusTypography: {FontSize}px, column {MaxWidth}px")]
public readonly record struct FocusTypography(
    double FontSize,
    double LineHeight,
    double ParagraphMargin,
    double HeadingSize,
    double HeadingLineHeight,
    double HeadingTopMargin,
    double HeadingBottomMargin,
    double ItemIndent,
    double ItemBottomMargin,
    double SmallSize,
    double MaxWidth)
{
    /// <summary>A character's average width as a share of the text size, to turn a width in characters into pixels.</summary>
    private const double CharacterWidth = 0.5;

    /// <summary>A heading's size as a multiple of the text size.</summary>
    private const double HeadingScale = 1.35;

    /// <summary>Captions and footnotes as a multiple of the text size.</summary>
    private const double SmallScale = 0.85;

    /// <summary>The list indent as a multiple of the text size.</summary>
    private const double ItemIndentScale = 1.2;

    /// <summary>Headings are spaced a little tighter than body text.</summary>
    private const double HeadingSpacingShare = 0.8;

    /// <summary>Half the paragraph space, after headings and between list items.</summary>
    private const double HalfSpace = 0.5;

    /// <summary>Works out the type for the reader's settings.</summary>
    /// <param name="fontSize">The body text size in pixels.</param>
    /// <param name="lineSpacing">The line height as a multiple of the text size.</param>
    /// <param name="paragraphSpacing">The space after a paragraph as a multiple of the text size.</param>
    /// <param name="textWidth">The column width in characters.</param>
    /// <returns>The sizes and spaces.</returns>
    public static FocusTypography From(double fontSize, double lineSpacing, double paragraphSpacing, double textWidth)
    {
        var paragraph = fontSize * paragraphSpacing;
        var heading = fontSize * HeadingScale;
        return new(
            fontSize,
            fontSize * lineSpacing,
            paragraph,
            heading,
            heading * Math.Max(1, lineSpacing * HeadingSpacingShare),
            paragraph,
            paragraph * HalfSpace,
            fontSize * ItemIndentScale,
            paragraph * HalfSpace,
            fontSize * SmallScale,
            textWidth * fontSize * CharacterWidth);
    }
}

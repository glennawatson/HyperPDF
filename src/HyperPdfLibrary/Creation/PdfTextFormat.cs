// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>How text is laid out in a box: alignment, wrapping, line spacing and line decorations.</summary>
[DebuggerDisplay("PdfTextFormat: {Alignment}, {Vertical}, wrap {Wrap}")]
public sealed record PdfTextFormat
{
    /// <summary>The default line pitch as a multiple of the font size.</summary>
    private const float DefaultLineSpacing = 1.2F;

    /// <summary>Gets a format with left alignment, top alignment, wrapping and no decorations.</summary>
    public static PdfTextFormat Default { get; } = new();

    /// <summary>Gets how lines sit between the left and right edges.</summary>
    public PdfTextAlignment Alignment { get; init; }

    /// <summary>Gets how the block of lines sits between the top and bottom edges.</summary>
    public PdfVerticalAlignment Vertical { get; init; }

    /// <summary>Gets a value indicating whether lines wrap at the box's width; when <see langword="false"/> each paragraph is one line.</summary>
    public bool Wrap { get; init; } = true;

    /// <summary>Gets the line pitch as a multiple of the font size.</summary>
    public float LineSpacing { get; init; } = DefaultLineSpacing;

    /// <summary>Gets a value indicating whether each line is underlined.</summary>
    public bool Underline { get; init; }

    /// <summary>Gets a value indicating whether a line is struck through each line.</summary>
    public bool Strikeout { get; init; }
}

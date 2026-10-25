// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>
/// How characters look. Every property is optional: an unset one is inherited from the style the text uses, and that style's
/// from the style it is based on, so a format only names what it changes.
/// </summary>
[DebuggerDisplay("PdfCharacterFormat: {Size} pt, bold {Bold}, italic {Italic}")]
public sealed record PdfCharacterFormat
{
    /// <summary>Gets the typeface.</summary>
    public PdfFontFamily? Family { get; init; }

    /// <summary>Gets the size in points.</summary>
    public float? Size { get; init; }

    /// <summary>Gets a value indicating whether the text is bold.</summary>
    public bool? Bold { get; init; }

    /// <summary>Gets a value indicating whether the text is italic.</summary>
    public bool? Italic { get; init; }

    /// <summary>Gets a value indicating whether the text is underlined.</summary>
    public bool? Underline { get; init; }

    /// <summary>Gets a value indicating whether the text is struck through.</summary>
    public bool? Strikeout { get; init; }

    /// <summary>Gets whether the text is raised, lowered or on the line.</summary>
    public PdfScript? Script { get; init; }

    /// <summary>Gets a value indicating whether lower case letters are drawn as smaller capitals.</summary>
    public bool? SmallCaps { get; init; }

    /// <summary>Gets a value indicating whether every letter is drawn as a capital.</summary>
    public bool? AllCaps { get; init; }

    /// <summary>Gets the text colour.</summary>
    public PdfCanvasColor? Color { get; init; }

    /// <summary>Gets the colour painted behind the text, like a highlighter.</summary>
    public PdfCanvasColor? Highlight { get; init; }
}

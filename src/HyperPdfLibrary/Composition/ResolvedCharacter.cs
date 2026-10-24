// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Character formatting with every property settled, after styles and direct formatting are combined.</summary>
/// <param name="Family">The typeface.</param>
/// <param name="Size">The size in points.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="Underline">Whether the text is underlined.</param>
/// <param name="Strikeout">Whether the text is struck through.</param>
/// <param name="Script">Whether the text is raised, lowered or on the line.</param>
/// <param name="SmallCaps">Whether lower case letters are drawn as smaller capitals.</param>
/// <param name="AllCaps">Whether every letter is drawn as a capital.</param>
/// <param name="Color">The text colour.</param>
/// <param name="Highlight">The colour behind the text, or <see langword="null"/> for none.</param>
[DebuggerDisplay("ResolvedCharacter: {Size} pt, bold {Bold}, italic {Italic}")]
internal sealed record ResolvedCharacter(
    PdfFontFamily? Family,
    float Size,
    bool Bold,
    bool Italic,
    bool Underline,
    bool Strikeout,
    PdfScript Script,
    bool SmallCaps,
    bool AllCaps,
    PdfCanvasColor Color,
    PdfCanvasColor? Highlight)
{
    /// <summary>The size of the text that nothing sets a size for.</summary>
    private const float FallbackSize = 11F;

    /// <summary>Gets the formatting that nothing has changed: black text on the line, with no typeface.</summary>
    internal static ResolvedCharacter Empty { get; } =
        new(null, FallbackSize, false, false, false, false, PdfScript.Normal, false, false, PdfCanvasColor.Black, null);

    /// <summary>Lays a format over this one.</summary>
    /// <param name="over">The format whose set properties win, or <see langword="null"/> for none.</param>
    /// <returns>The combined formatting.</returns>
    internal ResolvedCharacter Apply(PdfCharacterFormat? over) => over is null ? this : ApplyFont(over).ApplyLook(over);

    /// <summary>Lays the typeface, size and weight of a format over this one.</summary>
    /// <param name="over">The format.</param>
    /// <returns>The combined formatting.</returns>
    private ResolvedCharacter ApplyFont(PdfCharacterFormat over) =>
        this with { Family = over.Family ?? Family, Size = over.Size ?? Size, Bold = over.Bold ?? Bold, Italic = over.Italic ?? Italic };

    /// <summary>Lays the decorations, script, capitals and colours of a format over this one.</summary>
    /// <param name="over">The format.</param>
    /// <returns>The combined formatting.</returns>
    private ResolvedCharacter ApplyLook(PdfCharacterFormat over) => this with
    {
        Underline = over.Underline ?? Underline,
        Strikeout = over.Strikeout ?? Strikeout,
        Script = over.Script ?? Script,
        SmallCaps = over.SmallCaps ?? SmallCaps,
        AllCaps = over.AllCaps ?? AllCaps,
        Color = over.Color ?? Color,
        Highlight = over.Highlight ?? Highlight,
    };
}

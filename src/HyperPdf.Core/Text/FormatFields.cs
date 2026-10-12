// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Text;

/// <summary>The settings of a text format while a record is read, so the format is made once at the end.</summary>
internal record struct FormatFields
{
    /// <summary>Initializes a new instance of the <see cref="FormatFields"/> struct from a format.</summary>
    /// <param name="format">The format.</param>
    internal FormatFields(TextFormat format)
    {
        FontFamily = format.FontFamily;
        FontSize = format.FontSize;
        Color = format.Color;
        IsBold = format.IsBold;
        IsItalic = format.IsItalic;
        IsUnderline = format.IsUnderline;
        Alignment = format.Alignment;
        LineSpacing = format.LineSpacing;
        CharacterSpacing = format.CharacterSpacing;
        CombCells = format.CombCells;
    }

    /// <summary>Gets or sets the font family.</summary>
    internal string FontFamily { get; set; }

    /// <summary>Gets or sets the text size in points.</summary>
    internal float FontSize { get; set; }

    /// <summary>Gets or sets the colour as 0xRRGGBB.</summary>
    internal uint Color { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is bold.</summary>
    internal bool IsBold { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is italic.</summary>
    internal bool IsItalic { get; set; }

    /// <summary>Gets or sets a value indicating whether the text is underlined.</summary>
    internal bool IsUnderline { get; set; }

    /// <summary>Gets or sets the alignment.</summary>
    internal TextBoxAlignment Alignment { get; set; }

    /// <summary>Gets or sets the line spacing, as a multiple of the text size.</summary>
    internal float LineSpacing { get; set; }

    /// <summary>Gets or sets the extra space between characters, in points.</summary>
    internal float CharacterSpacing { get; set; }

    /// <summary>Gets or sets the character boxes of a comb, or 0.</summary>
    internal int CombCells { get; set; }

    /// <summary>Makes the format.</summary>
    /// <returns>The format.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly TextFormat ToFormat() => new(FontFamily, FontSize, Color)
    {
        IsBold = IsBold,
        IsItalic = IsItalic,
        IsUnderline = IsUnderline,
        Alignment = Alignment,
        LineSpacing = LineSpacing,
        CharacterSpacing = CharacterSpacing,
        CombCells = CombCells,
    };
}

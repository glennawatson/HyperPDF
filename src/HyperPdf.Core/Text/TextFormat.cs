// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;

namespace HyperPdf.Core.Text;

/// <summary>How text written on a page looks.</summary>
/// <param name="FontFamily">The font family, such as "Helvetica" or an installed family like "DejaVu Sans".</param>
/// <param name="FontSize">The font size in points.</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
[DebuggerDisplay("TextFormat: {FontFamily} {FontSize}pt")]
public sealed record TextFormat(string FontFamily, float FontSize, uint Color)
{
    /// <summary>The default line height, as a multiple of the font size.</summary>
    private const float DefaultLines = 1.25F;

    /// <summary>The smallest font size, in points.</summary>
    private const float MinSize = 4;

    /// <summary>The largest font size, in points.</summary>
    private const float MaxSize = 144;

    /// <summary>The most comb cells a text box takes.</summary>
    private const int MaxCells = 256;

    /// <summary>The default font size in points.</summary>
    private const float DefaultSize = 12;

    /// <summary>The tightest line height, as a multiple of the font size.</summary>
    private const float MinLines = 0.5F;

    /// <summary>The loosest line height, as a multiple of the font size.</summary>
    private const float MaxLines = 4F;

    /// <summary>How far character spacing may tighten, as a share of the font size.</summary>
    private const float TightestSpacing = -0.25F;

    /// <summary>How far character spacing may widen, as a multiple of the font size.</summary>
    private const float WidestSpacing = 2F;

    /// <summary>Gets the default line height, as a multiple of the font size.</summary>
    public static float DefaultLineSpacing => DefaultLines;

    /// <summary>Gets the smallest font size, in points.</summary>
    public static float MinFontSize => MinSize;

    /// <summary>Gets the largest font size, in points.</summary>
    public static float MaxFontSize => MaxSize;

    /// <summary>Gets the most comb cells a text box takes.</summary>
    public static int MaxCombCells => MaxCells;

    /// <summary>Gets the format new text starts with: Helvetica, 12 points, in ink.</summary>
    public static TextFormat Default { get; } = new(StandardFontFamilies.Sans, DefaultSize, AnnotationColors.Ink);

    /// <summary>Gets a value indicating whether the text is bold.</summary>
    public bool IsBold { get; init; }

    /// <summary>Gets a value indicating whether the text is italic.</summary>
    public bool IsItalic { get; init; }

    /// <summary>Gets a value indicating whether the text is underlined.</summary>
    public bool IsUnderline { get; init; }

    /// <summary>Gets how the lines sit across the box.</summary>
    public TextBoxAlignment Alignment { get; init; }

    /// <summary>Gets the line height as a multiple of the font size.</summary>
    public float LineSpacing { get; init; } = DefaultLines;

    /// <summary>Gets the extra space after each character, in points.</summary>
    public float CharacterSpacing { get; init; }

    /// <summary>Gets the number of evenly spaced boxes, one character each, or 0 for running text.</summary>
    public int CombCells { get; init; }

    /// <summary>Returns a copy with every value kept in its usable range.</summary>
    /// <returns>The clamped format.</returns>
    public TextFormat Clamped()
    {
        var family = string.IsNullOrWhiteSpace(FontFamily) ? StandardFontFamilies.Sans : FontFamily.Trim();
        var size = float.IsFinite(FontSize) ? Math.Clamp(FontSize, MinSize, MaxSize) : DefaultSize;
        var line = float.IsFinite(LineSpacing) && LineSpacing > 0 ? Math.Clamp(LineSpacing, MinLines, MaxLines) : DefaultLines;
        var spacing = float.IsFinite(CharacterSpacing) ? Math.Clamp(CharacterSpacing, size * TightestSpacing, size * WidestSpacing) : 0;
        var alignment = Alignment is TextBoxAlignment.Left or TextBoxAlignment.Center or TextBoxAlignment.Right ? Alignment : TextBoxAlignment.Left;
        var cells = Math.Clamp(CombCells, 0, MaxCells);
        return IsSame(family, (size, line, spacing), alignment, cells)
            ? this
            : this with { FontFamily = family, FontSize = size, LineSpacing = line, CharacterSpacing = spacing, Alignment = alignment, CombCells = cells, Color = Color & 0xFFFFFFU };
    }

    /// <summary>Gets whether two numbers are exactly the same value, as clamping a value in range returns it unchanged.</summary>
    /// <param name="left">One number.</param>
    /// <param name="right">The other.</param>
    /// <returns><see langword="true"/> when the bits match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SameBits(float left, float right) => BitConverter.SingleToInt32Bits(left) == BitConverter.SingleToInt32Bits(right);

    /// <summary>Gets whether clamped values are this format's own, so the format can be kept as it is.</summary>
    /// <param name="family">The font family.</param>
    /// <param name="sizes">The text size, line spacing and character spacing.</param>
    /// <param name="alignment">The alignment.</param>
    /// <param name="cells">The comb's character boxes.</param>
    /// <returns><see langword="true"/> when nothing changed.</returns>
    private bool IsSame(string family, (float Size, float Line, float Spacing) sizes, TextBoxAlignment alignment, int cells) =>
        ReferenceEquals(family, FontFamily)
        && SameBits(sizes.Size, FontSize)
        && SameBits(sizes.Line, LineSpacing)
        && SameBits(sizes.Spacing, CharacterSpacing)
        && alignment == Alignment
        && cells == CombCells
        && Color <= 0xFFFFFFU;
}

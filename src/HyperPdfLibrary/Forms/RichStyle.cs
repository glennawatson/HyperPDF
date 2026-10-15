// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>How a stretch of rich text looks: the CSS properties of the XHTML subset in ISO 32000-2 12.7.4.3.</summary>
/// <param name="Family">The kind of font the first family names, or <see cref="RichFamily.Inherit"/> to use the field's font.</param>
/// <param name="Size">The font size in points; 0 to use the field's size.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Italic">Whether the text is italic or oblique.</param>
/// <param name="Underline">Whether the text is underlined.</param>
/// <param name="StrikeOut">Whether the text is struck through.</param>
/// <param name="Color">The colour as 0xRRGGBB, or <see cref="NoColor"/> to use the field's colour.</param>
/// <param name="Alignment">The alignment: 0 left, 1 centred, 2 right, or <see cref="NoAlignment"/> to use the field's.</param>
/// <param name="KeepSpaces">Whether runs of spaces are kept (<c>xfa-spacerun:yes</c>) instead of collapsed into one.</param>
[DebuggerDisplay("RichStyle: {Family} {Size} bold={Bold} italic={Italic}")]
internal readonly record struct RichStyle(RichFamily Family, float Size, bool Bold, bool Italic, bool Underline, bool StrikeOut, int Color, int Alignment, bool KeepSpaces)
{
    /// <summary>The colour value that means the style sets none.</summary>
    internal const int NoColor = -1;

    /// <summary>The alignment value that means the style sets none.</summary>
    internal const int NoAlignment = -1;

    /// <summary>Gets a style that sets nothing, so a field's own font, size, colour and alignment apply.</summary>
    internal static RichStyle None => new(RichFamily.Inherit, 0, false, false, false, false, NoColor, NoAlignment, false);
}

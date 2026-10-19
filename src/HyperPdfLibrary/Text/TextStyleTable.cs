// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Text;

/// <summary>The styles of the runs of a text page, and the run each character belongs to.</summary>
/// <param name="Styles">The style of each run.</param>
/// <param name="RunOfChar">The run of each character, or -1 for a character that has none.</param>
/// <param name="Decorations">The decorations of each character, or <see langword="null"/> when the page has none.</param>
[DebuggerDisplay("TextStyleTable: {Styles.Length} runs")]
internal sealed record TextStyleTable(TextRunStyle[] Styles, int[] RunOfChar, PdfTextDecorations[]? Decorations)
{
    /// <summary>Gets the style of a character.</summary>
    /// <param name="index">The character index.</param>
    /// <returns>The style, with the character's decorations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfTextCharStyle Of(int index)
    {
        var run = RunOfChar[index];
        var style = (uint)run < (uint)Styles.Length ? Styles[run] : TextRunStyle.None;
        return new(
            style.FillColor,
            style.StrokeColor,
            style.Angle,
            style.Matrix,
            style.FontName,
            style.FontWeight,
            style.FontFlags,
            Decorations is null ? PdfTextDecorations.None : Decorations[index]);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>An RGB colour for a widget's border or background, or no colour at all.</summary>
/// <param name="Red">The red part, from 0 to 1.</param>
/// <param name="Green">The green part, from 0 to 1.</param>
/// <param name="Blue">The blue part, from 0 to 1.</param>
/// <param name="IsTransparent">Whether the widget has no colour here (<c>/MK</c> holds an empty array, ISO 32000-2 12.5.6.19).</param>
[DebuggerDisplay("PdfFieldColor: {Red} {Green} {Blue} transparent {IsTransparent}")]
public readonly record struct PdfFieldColor(float Red, float Green, float Blue, bool IsTransparent)
{
    /// <summary>Gets the colour that draws nothing.</summary>
    public static PdfFieldColor Transparent => new(0, 0, 0, true);

    /// <summary>Gets black.</summary>
    public static PdfFieldColor Black => FromRgb(0, 0, 0);

    /// <summary>Gets white.</summary>
    public static PdfFieldColor White => FromRgb(1, 1, 1);

    /// <summary>Makes a colour from its parts.</summary>
    /// <param name="red">The red part, from 0 to 1.</param>
    /// <param name="green">The green part, from 0 to 1.</param>
    /// <param name="blue">The blue part, from 0 to 1.</param>
    /// <returns>The colour.</returns>
    public static PdfFieldColor FromRgb(float red, float green, float blue) => new(red, green, blue, false);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// The soft tint drawn over fillable form fields: a filled rectangle over each visible widget that is not read-only and
/// is not a push button.
/// </summary>
/// <param name="Rgb">The colour as 0xRRGGBB.</param>
/// <param name="Alpha">The opacity, 0 (clear) to 255 (solid).</param>
[DebuggerDisplay("PdfFormHighlight: #{Rgb:X6} alpha {Alpha}")]
public readonly record struct PdfFormHighlight(uint Rgb, byte Alpha)
{
    /// <summary>The number of bits the alpha is shifted by when the tint is packed into one number.</summary>
    private const int AlphaShift = 32;

    /// <summary>Gets a tint that draws nothing.</summary>
    public static PdfFormHighlight None => default;

    /// <summary>Gets a value indicating whether the tint is drawn.</summary>
    public bool IsVisible => Alpha != 0;

    /// <summary>Unpacks a tint packed by <see cref="Pack"/>.</summary>
    /// <param name="packed">The packed tint.</param>
    /// <returns>The tint.</returns>
    internal static PdfFormHighlight Unpack(long packed) => new((uint)packed, (byte)(packed >> AlphaShift));

    /// <summary>Packs the tint into one number, so a cache can publish it with a single atomic write.</summary>
    /// <returns>The packed tint.</returns>
    internal long Pack() => ((long)Alpha << AlphaShift) | Rgb;
}

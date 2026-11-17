// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>Spells and reads the blend mode names of ISO 32000-2, Table 147.</summary>
internal static class PdfBlendModes
{
    /// <summary>Gets the name a blend mode is written as.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>The name without its slash.</returns>
    internal static string GetSpelling(PdfBlendMode mode) => mode switch
    {
        PdfBlendMode.Multiply => "Multiply",
        PdfBlendMode.Screen => "Screen",
        PdfBlendMode.Overlay => "Overlay",
        PdfBlendMode.Darken => "Darken",
        PdfBlendMode.Lighten => "Lighten",
        PdfBlendMode.ColorDodge => "ColorDodge",
        PdfBlendMode.ColorBurn => "ColorBurn",
        PdfBlendMode.HardLight => "HardLight",
        PdfBlendMode.SoftLight => "SoftLight",
        PdfBlendMode.Difference => "Difference",
        PdfBlendMode.Exclusion => "Exclusion",
        PdfBlendMode.Hue => "Hue",
        PdfBlendMode.Saturation => "Saturation",
        PdfBlendMode.Color => "Color",
        PdfBlendMode.Luminosity => "Luminosity",
        _ => "Normal",
    };
}

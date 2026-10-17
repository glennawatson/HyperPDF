// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics;

/// <summary>Recognises the standard blend modes without constructing lookup storage.</summary>
internal static class PdfBlendModeNames
{
    /// <summary>Determines whether a name is a standard blend mode other than legacy Compatible.</summary>
    /// <param name="name">The parsed name.</param>
    /// <returns>Whether ISO 32000-2 defines the blend mode.</returns>
    internal static bool IsStandard(KnownName name) =>
        IsBasicSeparable(name) || IsContrastOrDifference(name) || IsNonseparable(name);

    /// <summary>Recognises normal, product and channel comparison modes.</summary>
    /// <param name="name">The parsed name.</param>
    /// <returns>Whether the mode belongs to this separable group.</returns>
    private static bool IsBasicSeparable(KnownName name) =>
        name is KnownName.Normal or KnownName.Multiply or KnownName.Screen or KnownName.Overlay
            or KnownName.Darken or KnownName.Lighten or KnownName.ColorDodge or KnownName.ColorBurn;

    /// <summary>Recognises light, contrast and difference modes.</summary>
    /// <param name="name">The parsed name.</param>
    /// <returns>Whether the mode belongs to this separable group.</returns>
    private static bool IsContrastOrDifference(KnownName name) =>
        name is KnownName.HardLight or KnownName.SoftLight or KnownName.Difference or KnownName.Exclusion;

    /// <summary>Recognises modes that combine hue, saturation and luminosity.</summary>
    /// <param name="name">The parsed name.</param>
    /// <returns>Whether the mode is nonseparable.</returns>
    private static bool IsNonseparable(KnownName name) =>
        name is KnownName.Hue or KnownName.Saturation or KnownName.Color or KnownName.Luminosity;
}

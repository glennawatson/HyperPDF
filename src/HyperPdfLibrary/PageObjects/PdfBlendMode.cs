// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>How an object's colours mix with what is already on the page (ISO 32000-2, 11.3.5, the <c>BM</c> entry).</summary>
public enum PdfBlendMode
{
    /// <summary>The object covers what is under it.</summary>
    Normal = 0,

    /// <summary>Multiplies the colours, which only darkens.</summary>
    Multiply = 1,

    /// <summary>Multiplies the opposites of the colours, which only lightens.</summary>
    Screen = 2,

    /// <summary>Multiplies or screens, by the colour underneath.</summary>
    Overlay = 3,

    /// <summary>Keeps the darker of the two colours.</summary>
    Darken = 4,

    /// <summary>Keeps the lighter of the two colours.</summary>
    Lighten = 5,

    /// <summary>Brightens the colour underneath to match the object.</summary>
    ColorDodge = 6,

    /// <summary>Darkens the colour underneath to match the object.</summary>
    ColorBurn = 7,

    /// <summary>Multiplies or screens, by the object's colour.</summary>
    HardLight = 8,

    /// <summary>Darkens or lightens, by the object's colour, more gently than hard light.</summary>
    SoftLight = 9,

    /// <summary>The difference between the two colours.</summary>
    Difference = 10,

    /// <summary>Like difference with less contrast.</summary>
    Exclusion = 11,

    /// <summary>The object's hue with the saturation and brightness underneath.</summary>
    Hue = 12,

    /// <summary>The object's saturation with the hue and brightness underneath.</summary>
    Saturation = 13,

    /// <summary>The object's hue and saturation with the brightness underneath.</summary>
    Color = 14,

    /// <summary>The object's brightness with the hue and saturation underneath.</summary>
    Luminosity = 15,
}

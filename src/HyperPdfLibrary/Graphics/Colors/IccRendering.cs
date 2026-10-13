// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The rendering intent and black point choice an ICC conversion uses.</summary>
/// <param name="Intent">The rendering intent.</param>
/// <param name="Compensate">Whether the conversion maps the profile's black point to the output's black point.</param>
internal readonly record struct IccRendering(IccIntent Intent, bool Compensate)
{
    /// <summary>The number of renderings: four intents, each with and without compensation.</summary>
    internal const int Count = 8;

    /// <summary>
    /// Gets the rendering of the initial graphics state: relative colorimetric (ISO 32000-2 Table 51) with the
    /// compensation /Default gives.
    /// </summary>
    internal static IccRendering Default { get; } = From(IccIntent.RelativeColorimetric, BlackPointCompensationMode.Default);

    /// <summary>Gets the index of this rendering, from 0 to <see cref="Count"/> - 1.</summary>
    internal int Slot => ((int)Intent << 1) | (Compensate ? 1 : 0);

    /// <summary>
    /// Chooses the conversion for a graphics state. Absolute colorimetric never compensates (ISO 32000-2 8.6.5.9). /OFF
    /// turns compensation off. /ON and /Default turn it on: ISO 32000-2 leaves /Default to the PDF processor, and on a
    /// screen compensation keeps shadow detail and the full contrast of blacks, as desktop viewers do by default.
    /// </summary>
    /// <param name="intent">The rendering intent.</param>
    /// <param name="mode">The /UseBlackPtComp value.</param>
    /// <returns>The rendering.</returns>
    internal static IccRendering From(IccIntent intent, BlackPointCompensationMode mode) =>
        new(intent, intent != IccIntent.AbsoluteColorimetric && mode != BlackPointCompensationMode.Off);
}

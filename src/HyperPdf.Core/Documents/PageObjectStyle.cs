// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>How a page object is painted, as the properties bar shows it.</summary>
/// <param name="Fill">The fill colour as 0xAARRGGBB, with full alpha, converted from the object's own colour space.</param>
/// <param name="Stroke">The stroke colour as 0xAARRGGBB, with full alpha.</param>
/// <param name="LineWidth">The stroke width in points, or 0 for an object that is not stroked.</param>
/// <param name="FontSize">The font size of a text object in points, or 0.</param>
[DebuggerDisplay("PageObjectStyle: fill {Fill:X8}, stroke {Stroke:X8}, width {LineWidth}")]
public readonly record struct PageObjectStyle(uint Fill, uint Stroke, float LineWidth, float FontSize);

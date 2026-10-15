// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>The width and RGB colour a line or outline is stroked with.</summary>
/// <param name="Red">The red level from 0 to 1.</param>
/// <param name="Green">The green level from 0 to 1.</param>
/// <param name="Blue">The blue level from 0 to 1.</param>
/// <param name="Width">The line width in points; zero draws the thinnest line the device can.</param>
[DebuggerDisplay("PdfStroke: rgb({Red}, {Green}, {Blue}), {Width} pt")]
public readonly record struct PdfStroke(float Red, float Green, float Blue, float Width)
{
    /// <summary>Makes a black stroke.</summary>
    /// <param name="width">The line width in points.</param>
    /// <returns>The stroke.</returns>
    public static PdfStroke Black(float width) => new(0F, 0F, 0F, width);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A fill that shades from one colour to another across an object (ISO 32000-2, 8.7.4.5.3 and 8.7.4.5.4, axial and radial shadings).</summary>
/// <param name="Start">The colour at the start: the first end of a line, or the middle of a circle.</param>
/// <param name="End">The colour at the end: the second end of a line, or the edge of a circle.</param>
/// <param name="Shape">The shape the colours run along.</param>
/// <param name="AngleDegrees">For a line, the direction of the shading in degrees: 0 runs left to right and 90 runs bottom to top.</param>
[DebuggerDisplay("PdfGradient: {Shape}, {AngleDegrees} degrees")]
public sealed record PdfGradient(PdfPaint Start, PdfPaint End, PdfGradientShape Shape, float AngleDegrees)
{
    /// <summary>Makes a gradient along a line.</summary>
    /// <param name="start">The colour at the start.</param>
    /// <param name="end">The colour at the end.</param>
    /// <param name="angleDegrees">The direction in degrees.</param>
    /// <returns>The gradient.</returns>
    public static PdfGradient Linear(PdfPaint start, PdfPaint end, float angleDegrees) => new(start, end, PdfGradientShape.Linear, angleDegrees);

    /// <summary>Makes a gradient from the middle of the object out to its corners.</summary>
    /// <param name="center">The colour in the middle.</param>
    /// <param name="edge">The colour at the edge.</param>
    /// <returns>The gradient.</returns>
    public static PdfGradient Radial(PdfPaint center, PdfPaint edge) => new(center, edge, PdfGradientShape.Radial, 0F);
}

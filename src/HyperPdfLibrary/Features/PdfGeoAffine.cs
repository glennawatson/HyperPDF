// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>An affine mapping from a page point (x, y): first = A·x + B·y + C and second = D·x + E·y + F.</summary>
/// <param name="A">The x factor of the first value.</param>
/// <param name="B">The y factor of the first value.</param>
/// <param name="C">The offset of the first value.</param>
/// <param name="D">The x factor of the second value.</param>
/// <param name="E">The y factor of the second value.</param>
/// <param name="F">The offset of the second value.</param>
[DebuggerDisplay("PdfGeoAffine: [{A} {B} {C}] [{D} {E} {F}]")]
public readonly record struct PdfGeoAffine(double A, double B, double C, double D, double E, double F);

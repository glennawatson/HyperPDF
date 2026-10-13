// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A circle given by its centre and radius, in the space a shading is used in.</summary>
/// <param name="X">The centre's x.</param>
/// <param name="Y">The centre's y.</param>
/// <param name="Radius">The radius, not negative.</param>
[DebuggerDisplay("PdfCircle: ({X}, {Y}) r {Radius}")]
public readonly record struct PdfCircle(float X, float Y, float Radius);

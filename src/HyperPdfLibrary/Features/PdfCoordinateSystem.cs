// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>A geographic or projected coordinate system dictionary (ISO 32000-2 Tables 270 and 271).</summary>
/// <param name="Type">The <c>/Type</c>: GEOGCS for a geographic system or PROJCS for a projected one; null when missing.</param>
/// <param name="Name">The system's name from its well-known text, for example "World_Mercator"; null without well-known text.</param>
/// <param name="Epsg">The EPSG code (<c>/EPSG</c>), or null.</param>
/// <param name="WellKnownText">The well-known text (<c>/WKT</c>), or null.</param>
[DebuggerDisplay("PdfCoordinateSystem: {Type} {Name} EPSG {Epsg}")]
public sealed record PdfCoordinateSystem(string? Type, string? Name, int? Epsg, string? WellKnownText);

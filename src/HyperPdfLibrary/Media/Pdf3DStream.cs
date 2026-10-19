// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>A 3D stream (U3D or PRC data) with its views.</summary>
/// <param name="Subtype">The data format: U3D or PRC; null when missing.</param>
/// <param name="Data">The stream holding the 3D data.</param>
/// <param name="Views">The views.</param>
/// <param name="DefaultViewIndex">The index of the default view (<c>/DV</c>) when it is an integer, or null.</param>
/// <param name="DefaultViewSelector">The default view when <c>/DV</c> is a name or string, or null.</param>
[DebuggerDisplay("Pdf3DStream: {Subtype} {Views.Length} views")]
public sealed record Pdf3DStream(string? Subtype, PdfStream Data, Pdf3DView[] Views, int? DefaultViewIndex, string? DefaultViewSelector);

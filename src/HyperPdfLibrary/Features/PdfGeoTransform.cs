// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>The mapping from a geospatial viewport's page space to its world coordinates, fitted from its point pairs.</summary>
/// <param name="PageToWorld">Maps a page point in user space to its <c>/GPTS</c> coordinate.</param>
/// <param name="PageToUnitSquare">Maps a page point to the viewport's unit square.</param>
/// <param name="Bounds">The polygon where the mapping is valid, as unit square pairs; the whole unit square when the file gives none.</param>
[DebuggerDisplay("PdfGeoTransform: {PageToWorld}")]
public sealed record PdfGeoTransform(PdfGeoAffine PageToWorld, PdfGeoAffine PageToUnitSquare, double[] Bounds);

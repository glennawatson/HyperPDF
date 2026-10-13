// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>Geospatial data of a viewport (a <c>/GEO</c> measure dictionary, ISO 32000-2 12.10).</summary>
/// <param name="CoordinateSystemType">The coordinate system type of <c>/GCS</c>: PROJCS or GEOGCS; null when missing.</param>
/// <param name="WellKnownText">The coordinate system as well-known text (<c>/GCS /WKT</c>), or null.</param>
/// <param name="Epsg">The EPSG code (<c>/GCS /EPSG</c>), or null.</param>
/// <param name="GeoPoints">The world coordinate pairs (<c>/GPTS</c>); empty when missing or when an entry is not a number.</param>
/// <param name="LocalPoints">The matching points in the viewport's unit square (<c>/LPTS</c>); empty when missing or when an entry is not a number.</param>
/// <param name="Bounds">The area where the mapping is valid, as unit square pairs (<c>/Bounds</c>); empty when missing or when an entry is not a number.</param>
/// <param name="DisplayUnits">The preferred display units (<c>/PDU</c>): linear, area and angular.</param>
[DebuggerDisplay("PdfGeospatial: {CoordinateSystemType} EPSG {Epsg}")]
public sealed record PdfGeospatial(string? CoordinateSystemType, string? WellKnownText, int? Epsg, double[] GeoPoints, double[] LocalPoints, double[] Bounds, string[] DisplayUnits)
{
    /// <summary>Gets the name of the <c>/GCS</c> system from its well-known text, or null.</summary>
    public string? CoordinateSystemName { get; init; }

    /// <summary>Gets the system that positions should be shown in (<c>/DCS</c>), or null when the file names none.</summary>
    public PdfCoordinateSystem? DisplayCoordinateSystem { get; init; }

    /// <summary>
    /// Gets the 12 numbers of the projected coordinate system matrix (<c>/PCSM</c>); empty when missing. The reader reports
    /// it; <see cref="Document.PdfGeoReferencing"/> converts with <see cref="GeoPoints"/> and <see cref="LocalPoints"/>.
    /// </summary>
    public double[] ProjectedMatrix { get; init; } = [];
}

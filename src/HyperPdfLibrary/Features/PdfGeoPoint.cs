// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>
/// A world coordinate in the space of a geospatial measure's <c>/GPTS</c>, in the order the file writes its pairs. For a
/// geographic system (GEOGCS) <paramref name="First"/> is latitude and <paramref name="Second"/> is longitude, in degrees.
/// For a projected system (PROJCS) ISO 32000-2 Table 269 calls for eastings and northings, but map producers such as the
/// USGS write latitude and longitude of the underlying geographic system, so check the file's points before naming them.
/// </summary>
/// <param name="First">The first value of the pair.</param>
/// <param name="Second">The second value of the pair.</param>
[DebuggerDisplay("PdfGeoPoint: {First}, {Second}")]
public readonly record struct PdfGeoPoint(double First, double Second);

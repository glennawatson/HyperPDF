// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>
/// Tests that viewport measures read their ratio, number formats, point pairs and coordinate systems (ISO 32000-2 12.9
/// and 12.10), that a page point in a geospatial viewport converts to its world coordinate, and that malformed point
/// arrays are rejected without throwing.
/// </summary>
public sealed class GeospatialMeasureTests
{
    /// <summary>The environment variable naming the folder of downloaded reference PDFs.</summary>
    private const string ReferenceFolderVariable = "HYPERPDF_REFERENCE_PDF_DIR";

    /// <summary>How far a converted coordinate may be from an exact affine result.</summary>
    private const double Exact = 1e-9;

    /// <summary>
    /// How far, in degrees, a converted coordinate may be from a ground point of the USGS map's main viewport. Its four
    /// ground points are not exactly a parallelogram, so the least-squares fit misses each by up to about 0.0002 degrees.
    /// </summary>
    private const double MapFit = 5e-4;

    /// <summary>The precision of the sample distance format.</summary>
    private const int DistancePrecision = 8;

    /// <summary>The conversion factor of the sample distance format's second unit.</summary>
    private const double FeetPerMile = 5280;

    /// <summary>The sample y to x unit ratio.</summary>
    private const double YToX = 0.5;

    /// <summary>The EPSG code of WGS 84.</summary>
    private const int Wgs84 = 4326;

    /// <summary>The EPSG code of UTM zone 17 north on WGS 84.</summary>
    private const int Utm17North = 32_617;

    /// <summary>The box of the square sample viewports.</summary>
    private const string SquareBox = "[0 0 100 100]";

    /// <summary>The side of <see cref="SquareBox"/>.</summary>
    private const double SquareSide = 100;

    /// <summary>The middle of a side of <see cref="SquareBox"/>, where the triangle bounds' long edge crosses its diagonal.</summary>
    private const double SquareMiddle = 50;

    /// <summary>How far a test point sits in from a corner of <see cref="SquareBox"/>.</summary>
    private const double NearCorner = 10;

    /// <summary>The number of numbers in a projected coordinate system matrix.</summary>
    private const int MatrixNumbers = 12;

    /// <summary>The number of viewports on the USGS Lost City map.</summary>
    private const int MapViewports = 3;

    /// <summary>The index of the USGS map's quadrangle location viewport.</summary>
    private const int QuadrangleViewport = 1;

    /// <summary>The numbers in one point.</summary>
    private const int PairSize = 2;

    /// <summary>The number of dictionaries in the viewport array without geospatial measures.</summary>
    private const int TwoViewports = 2;

    /// <summary>The numbers in one row of a corner table: page x, page y, then the expected world pair.</summary>
    private const int CornerRow = 4;

    /// <summary>The position of the expected first world value in a corner table row.</summary>
    private const int FirstColumn = 2;

    /// <summary>The position of the expected second world value in a corner table row.</summary>
    private const int SecondColumn = 3;

    /// <summary>The projected system name in the sample well-known text.</summary>
    private const string UtmName = "WGS 84 / UTM zone 17N";

    /// <summary>The box of the sample geospatial viewport, as written.</summary>
    private static readonly double[] SmallBox = [0, 0, 10, 10];

    /// <summary>The corners and centre of the north-up sample viewport, with the world coordinates they convert to.</summary>
    private static readonly double[] NorthUpCorners =
    [
        100, 100, 10, 20,
        300, 100, 10, 30,
        300, 200, 15, 30,
        100, 200, 15, 20,
        200, 150, 12.5, 25,
    ];

    /// <summary>The corners of a viewport whose box is written top edge first, as USGS maps write it.</summary>
    private static readonly double[] TopDownCorners =
    [
        0, 2088, 39.125, -79,
        1728, 2088, 39.125, -78.625,
        1728, 30, 38.75, -78.625,
        0, 30, 38.75, -79,
    ];

    /// <summary>The corners of a viewport whose three point pairs are inside it, related by first = 1 + 2u + 3v and second = 4 - u + v / 2.</summary>
    private static readonly double[] InnerPointCorners =
    [
        0, 0, 1, 4,
        100, 0, 3, 3,
        100, 100, 6, 3.5,
        0, 100, 4, 4.5,
    ];

    /// <summary>The corners of the USGS Lost City map's quadrangle location viewport, with the ground points the file gives.</summary>
    private static readonly double[] QuadrangleCorners =
    [
        1114.1742, 191.51415, 40.88693, -83.70341,
        1197.69226, 191.51415, 40.88693, -76.66085,
        1197.69226, 131.57396, 36.94, -76.66085,
        1114.1742, 131.57396, 36.94, -83.70341,
    ];

    /// <summary>Rectilinear and geospatial measures keep their ratio, every number format entry, point pairs and coordinate systems.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasuresKeepFormatsPointsAndCoordinateSystems()
    {
        using var document = StructureDocuments.OpenPage(
            string.Empty,
            "/VP [<< /Type /Viewport /BBox [0 0 50 50] /Measure 4 0 R >> << /Type /Viewport /BBox [0 0 10 10] /Measure 5 0 R >>]",
            "<< /Type /Measure /Subtype /RL /R (1 in = 1 mi) /X [<< /U (mi) /C 1 >> << /U (ft) /C 5280 /F /F /D 8 /FD true /RT (.) /RD (,) /PS (~) /SS (!) /O /P >>] "
            + "/Y [<< /U (yd) /C 2 >>] /D [<< /U (mi) /C 1 /D 8 >>] /A [<< /U (sq mi) /C 1 >>] /T [<< /U (deg) /C 1 >>] /S [<< /U (%) /C 1 >>] /CYX 0.5 >>",
            "<< /Type /Measure /Subtype /GEO /GCS << /Type /PROJCS /EPSG 32617 /WKT (PROJCS[\"WGS 84 / UTM zone 17N\",GEOGCS[\"WGS 84\"]]) >> "
            + "/DCS << /Type /GEOGCS /EPSG 4326 >> /GPTS [1 2 3 4 5 6] /LPTS [0 0 1 0 0 1] /Bounds [0 0 1 0 1 1] /PDU [/KM /SQKM /DEG] "
            + "/PCSM [1 0 0 0 1 0 0 0 1 0 0 0] >>");
        var viewports = await PdfDocumentMeasure.GetViewportsAsync(document, 0, CancellationToken.None);
        var rectilinear = viewports[0].Measure!;
        var feet = rectilinear.X[^1];
        var geo = viewports[^1].Measure!.Geo!;

        await Assert.That(viewports.Length).IsEqualTo(PdfDocumentMeasure.GetViewports(document, PdfDocumentPages.GetPage(document, 0)).Length);
        await Assert.That(rectilinear.Ratio).IsEqualTo("1 in = 1 mi");
        await Assert.That(rectilinear.Geo).IsNull();
        await Assert.That(feet).IsEqualTo(new("ft", FeetPerMile, "F", DistancePrecision, true, ".", ",", "~", "!", "P"));
        await Assert.That(rectilinear.Y[0].Unit).IsEqualTo("yd");
        await Assert.That(rectilinear.Distance[0].Precision).IsEqualTo(DistancePrecision);
        await Assert.That(rectilinear.Area[0].Unit).IsEqualTo("sq mi");
        await Assert.That(rectilinear.Angle[0].Unit).IsEqualTo("deg");
        await Assert.That(rectilinear.Slope[0].Unit).IsEqualTo("%");
        await Assert.That(rectilinear.YToXRatio).IsEqualTo(YToX);
        await Assert.That(geo.CoordinateSystemType).IsEqualTo("PROJCS");
        await Assert.That(geo.Epsg).IsEqualTo(Utm17North);
        await Assert.That(geo.CoordinateSystemName).IsEqualTo(UtmName);
        await Assert.That(geo.DisplayCoordinateSystem).IsEqualTo(new("GEOGCS", null, Wgs84, null));
        await Assert.That(geo.DisplayUnits).IsEquivalentTo(["KM", "SQKM", "DEG"]);
        await Assert.That(geo.GeoPoints.Length).IsEqualTo(geo.LocalPoints.Length);
        await Assert.That(geo.Bounds.Length).IsEqualTo(geo.GeoPoints.Length);
        await Assert.That(geo.ProjectedMatrix.Length).IsEqualTo(MatrixNumbers);
        await Assert.That(viewports[^1].BoundingBox).IsEquivalentTo(SmallBox);
    }

    /// <summary>Each corner of a north-up viewport, and its centre, converts to the expected world coordinate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NorthUpViewportCornersConvert()
    {
        var transform = Transform("[100 100 300 200]", "/GPTS [10 20 10 30 15 30 15 20] /LPTS [0 0 1 0 1 1 0 1]");

        await AssertCornersAsync(transform, NorthUpCorners, Exact);
    }

    /// <summary>A box written top edge first maps the unit square from its first corner, so the ground points land on the right corners.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TopDownViewportCornersConvert()
    {
        var transform = Transform("[0 2088 1728 30]", "/GPTS [38.75 -79 39.125 -79 39.125 -78.625 38.75 -78.625] /LPTS [0 1 0 0 1 0 1 1]");

        await AssertCornersAsync(transform, TopDownCorners, Exact);
    }

    /// <summary>Three point pairs inside the viewport fix the mapping, and its corners convert exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InnerPointPairsConvertTheCorners()
    {
        var transform = Transform(SquareBox, "/GPTS [2.25 3.875 3.25 3.375 4.25 3.875] /LPTS [0.25 0.25 0.75 0.25 0.5 0.75]");

        await AssertCornersAsync(transform, InnerPointCorners, Exact);
    }

    /// <summary>The bounds polygon (a map's neatline) decides where the mapping is valid; its edge counts as inside.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoundsLimitWhereTheMappingIsValid()
    {
        var whole = Transform(SquareBox, "/GPTS [0 0 0 1 1 1] /LPTS [0 0 1 0 1 1]")!;
        var triangle = Transform(SquareBox, "/GPTS [0 0 0 1 1 1] /LPTS [0 0 1 0 1 1] /Bounds [0 0 1 0 0 1]")!;

        await Assert.That(whole.Bounds).IsEquivalentTo([0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 0.0]);
        await Assert.That(PdfGeoReferencing.IsInBounds(whole, SquareSide, SquareSide)).IsTrue();
        await Assert.That(PdfGeoReferencing.IsInBounds(whole, 0, 0)).IsTrue();
        await Assert.That(PdfGeoReferencing.IsInBounds(whole, -1, 0)).IsFalse();
        await Assert.That(PdfGeoReferencing.IsInBounds(triangle, NearCorner, NearCorner)).IsTrue();
        await Assert.That(PdfGeoReferencing.IsInBounds(triangle, SquareMiddle, SquareMiddle)).IsTrue();
        await Assert.That(PdfGeoReferencing.IsInBounds(triangle, SquareSide - NearCorner, SquareSide - NearCorner)).IsFalse();
    }

    /// <summary>Missing, odd, mismatched, too short, collinear or non-numeric point arrays, and a missing or flat box, are rejected without throwing.</summary>
    /// <param name="box">The viewport's box, or an empty string for none.</param>
    /// <param name="entries">The measure's entries after <c>/Subtype /GEO</c>.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[0 0 1 1]", "/LPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 0 1 1 5] /LPTS [0 0 1 0 1 1 5]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 0 1 1 2 2] /LPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 0] /LPTS [0 0 1 0]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 1 2 2] /LPTS [0 0 0.5 0.5 1 1]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 (x) 1 1] /LPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 1 1]", "/GPTS 5 /LPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 0 1 1] /LPTS [0 0 1 0 1 1] /Bounds [0 0 1 0 1]")]
    [Arguments("[0 0 1 1]", "/GPTS [0 0 1 0 1 1] /LPTS [0 0 1 0 1 1] /Bounds [0 0 1 1]")]
    [Arguments("", "/GPTS [0 0 1 0 1 1] /LPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 1]", "/GPTS [0 0 1 0 1 1] /LPTS [0 0 1 0 1 1]")]
    [Arguments("[0 0 0 1]", "/GPTS [0 0 1 0 1 1] /LPTS [0 0 1 0 1 1]")]
    public async Task MalformedGeospatialDataIsRejected(string box, string entries) => await Assert.That(Transform(box, entries)).IsNull();

    /// <summary>A rectilinear measure and a viewport without a measure have no world mapping.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewportsWithoutGeospatialMeasuresHaveNoMapping()
    {
        using var document = StructureDocuments.OpenPage(
            string.Empty,
            "/VP [<< /BBox [0 0 1 1] /Measure << /Subtype /RL /R (1:1) >> >> << /BBox [0 0 1 1] >> 7 (text)]");
        var viewports = PdfDocumentMeasure.GetViewports(document, PdfDocumentPages.GetPage(document, 0));

        await Assert.That(viewports.Length).IsEqualTo(TwoViewports);
        await Assert.That(Array.TrueForAll(viewports, static viewport => PdfGeoReferencing.CreateTransform(viewport) is null)).IsTrue();
    }

    /// <summary>The coordinate system name comes from the first quoted name in its well-known text.</summary>
    /// <param name="text">The well-known text.</param>
    /// <param name="expected">The name, or null when there is none.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("PROJCS[\"World_Mercator\",GEOGCS[\"GCS_WGS_1984\"]]", "World_Mercator")]
    [Arguments("GEOGCRS ( \"WGS 84\" , DATUM[\"x\"])", "WGS 84")]
    [Arguments("PROJCS[\"Say \"\"hi\"\"\",UNIT[\"m\",1]]", "Say \"hi\"")]
    [Arguments("PROJCS[World]", null)]
    [Arguments("PROJCS[\"Unclosed", null)]
    [Arguments("no brackets", null)]
    public async Task WellKnownTextNamesAreRead(string text, string? expected) => await Assert.That(WellKnownTextNames.Read(text)).IsEqualTo(expected);

    /// <summary>A cancelled token stops the async viewport read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledViewportReadThrows()
    {
        using var document = StructureDocuments.OpenPage(string.Empty, "/VP [<< /BBox [0 0 1 1] >>]");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => _ = await PdfDocumentMeasure.GetViewportsAsync(document, 0, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>
    /// The USGS Lost City map's three viewports read their coordinate systems, and every ground point converts back from
    /// its page position: exactly for the two north-up insets, and within <see cref="MapFit"/> for the main map. Skips when
    /// <c>HYPERPDF_REFERENCE_PDF_DIR</c> does not hold <c>usgs-lost-city-2023.pdf</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsgsMapGroundPointsConvert(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(RequireReferencePdf("usgs-lost-city-2023"), null, cancellationToken);
        var viewports = await PdfDocumentMeasure.GetViewportsAsync(document, 0, cancellationToken);

        await Assert.That(viewports.Select(static viewport => viewport.Name ?? string.Empty)).IsEquivalentTo(["Map Layers", "Quadrangle Location", "Adjoining Sheet Diagram"]);
        await Assert.That(viewports.Select(static viewport => viewport.Measure!.Geo!.CoordinateSystemName ?? string.Empty))
            .IsEquivalentTo(["GCS North American 1983 UTM Zone 17N (Calculated)", "World_Mercator", "World_Mercator"]);
        await Assert.That(viewports.Length).IsEqualTo(MapViewports);
        await AssertCornersAsync(PdfGeoReferencing.CreateTransform(viewports[QuadrangleViewport]), QuadrangleCorners, Exact);
        await AssertGroundPointsAsync(viewports[0], MapFit);
        await AssertGroundPointsAsync(viewports[QuadrangleViewport], Exact);
        await AssertGroundPointsAsync(viewports[^1], Exact);
    }

    /// <summary>The USGS Morrison map of 1957 uses the older <c>/LGIDict</c> form, which ISO 32000-2 does not define, so it has no viewports.</summary>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsgsLgiMapHasNoViewports(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(RequireReferencePdf("usgs-morrison-1957"), null, cancellationToken);

        await Assert.That((await PdfDocumentMeasure.GetViewportsAsync(document, 0, cancellationToken)).Length).IsEqualTo(0);
    }

    /// <summary>Opens a one-page document with one geospatial viewport and fits its mapping.</summary>
    /// <param name="box">The viewport's <c>/BBox</c> value, or an empty string for none.</param>
    /// <param name="entries">The measure's entries after <c>/Subtype /GEO</c>.</param>
    /// <returns>The mapping, or <see langword="null"/> when it is rejected.</returns>
    private static PdfGeoTransform? Transform(string box, string entries)
    {
        var boxEntry = box.Length == 0 ? string.Empty : $"/BBox {box} ";
        using var document = StructureDocuments.OpenPage(
            string.Empty,
            $"/VP [<< /Type /Viewport {boxEntry}/Measure << /Type /Measure /Subtype /GEO /GCS << /Type /GEOGCS /EPSG 4326 >> {entries} >> >>]");
        return PdfGeoReferencing.CreateTransform(PdfDocumentMeasure.GetViewports(document, PdfDocumentPages.GetPage(document, 0))[0]);
    }

    /// <summary>Checks that each page point in a corner table converts to its expected world coordinate.</summary>
    /// <param name="transform">The mapping.</param>
    /// <param name="corners">The table: page x, page y, expected first and second world values per row.</param>
    /// <param name="tolerance">How far each value may be from the expected one.</param>
    /// <returns>A task.</returns>
    private static async Task AssertCornersAsync(PdfGeoTransform? transform, double[] corners, double tolerance)
    {
        await Assert.That(transform).IsNotNull();
        for (var row = 0; row < corners.Length; row += CornerRow)
        {
            var world = PdfGeoReferencing.ToWorld(transform!, corners[row], corners[row + 1]);
            await Assert.That(world.First).IsEqualTo(corners[row + FirstColumn]).Within(tolerance);
            await Assert.That(world.Second).IsEqualTo(corners[row + SecondColumn]).Within(tolerance);
            await Assert.That(PdfGeoReferencing.IsInBounds(transform!, corners[row], corners[row + 1])).IsTrue();
        }
    }

    /// <summary>Checks that each of a viewport's ground points converts back from the page position of its local point.</summary>
    /// <param name="viewport">The viewport.</param>
    /// <param name="tolerance">How far each value may be from the ground point.</param>
    /// <returns>A task.</returns>
    private static async Task AssertGroundPointsAsync(PdfViewport viewport, double tolerance)
    {
        var geo = viewport.Measure!.Geo!;
        var box = viewport.BoundingBox;
        var transform = PdfGeoReferencing.CreateTransform(viewport);
        await Assert.That(transform).IsNotNull();
        for (var i = 0; i < geo.LocalPoints.Length; i += PairSize)
        {
            var x = box[0] + (geo.LocalPoints[i] * (box[PairSize] - box[0]));
            var y = box[1] + (geo.LocalPoints[i + 1] * (box[PairSize + 1] - box[1]));
            var world = PdfGeoReferencing.ToWorld(transform!, x, y);
            await Assert.That(world.First).IsEqualTo(geo.GeoPoints[i]).Within(tolerance);
            await Assert.That(world.Second).IsEqualTo(geo.GeoPoints[i + 1]).Within(tolerance);
        }
    }

    /// <summary>Gets a downloaded reference PDF, or skips the test when it is missing.</summary>
    /// <param name="id">The manifest id.</param>
    /// <returns>The file path.</returns>
    private static string RequireReferencePdf(string id)
    {
        var folder = Environment.GetEnvironmentVariable(ReferenceFolderVariable);
        var path = folder is { Length: > 0 } ? Path.Combine(folder, $"{id}.pdf") : string.Empty;
        if (!File.Exists(path))
        {
            Skip.Test($"No {id}.pdf. Fetch it from src/tests/reference-pdfs/manifest.json with src/tools/FetchReferencePdfs.cs and set {ReferenceFolderVariable}.");
        }

        return path;
    }
}

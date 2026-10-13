// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures geospatial viewports from a warm document laid out like a USGS topographic map: reading a page's viewports
/// and measures, fitting a viewport's world mapping, and converting a page point and checking it against the neatline,
/// as happens on every pointer move. Allocations come from the EventPipe trace.
/// </summary>
public class HyperPdfGeospatialBenchmarks
{
    /// <summary>The x of the converted page point.</summary>
    private const double PointX = 864;

    /// <summary>The y of the converted page point.</summary>
    private const double PointY = 1059;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The document's page.</summary>
    private PdfPage _page = null!;

    /// <summary>The main map viewport.</summary>
    private PdfViewport _viewport = null!;

    /// <summary>The main map viewport's mapping.</summary>
    private PdfGeoTransform _transform = null!;

    /// <summary>Opens the document and reads the viewports once so their objects are parsed.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1728 2160] /VP [<< /Type /Viewport /BBox [0 2088 1727.96 30.59] /Name (Map Layers) /Measure 4 0 R >> "
            + "<< /Type /Viewport /BBox [1114.17 191.51 1197.69 131.57] /Name (Quadrangle Location) /Measure 5 0 R >>] >>",
            "<< /Type /Measure /Subtype /GEO /Bounds [-0.02854 0.99939 0.00063 -0.02012 1.02854 0.00061 0.99938 1.02012] "
            + "/GCS << /Type /PROJCS /WKT (PROJCS[\"GCS North American 1983 UTM Zone 17N\",GEOGCS[\"GCS_North_American_1983\"]]) >> "
            + "/GPTS [38.85631 -78.90111 39.01631 -78.89638 39.01305 -78.72273 38.85307 -78.72784] "
            + "/LPTS [-0.02854 0.99939 0.00063 -0.02012 1.02854 0.00061 0.99938 1.02012] /PDU [/MI /SQMI /DEG] >>",
            "<< /Type /Measure /Subtype /GEO /GCS << /Type /PROJCS /WKT (PROJCS[\"World_Mercator\",GEOGCS[\"GCS_WGS_1984\"]]) >> "
            + "/GPTS [36.94 -83.70341 40.88693 -83.70341 40.88693 -76.66085 36.94 -76.66085] /LPTS [0 1 0 0 1 0 1 1] >>"),
            null);
        _page = PdfDocumentPages.GetPage(_document, 0);
        _viewport = PdfDocumentMeasure.GetViewports(_document, _page)[0];
        _transform = PdfGeoReferencing.CreateTransform(_viewport)!;
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the page's viewports with their geospatial measures and coordinate systems.</summary>
    /// <returns>The viewport count.</returns>
    [Benchmark]
    public int ReadViewports() => PdfDocumentMeasure.GetViewports(_document, _page).Length;

    /// <summary>Fits the main map viewport's mapping from its four point pairs.</summary>
    /// <returns>The mapping's bounds length.</returns>
    [Benchmark]
    public int CreateTransform() => PdfGeoReferencing.CreateTransform(_viewport)!.Bounds.Length;

    /// <summary>Converts a page point to its world coordinate and checks it against the neatline.</summary>
    /// <returns>The latitude, or not-a-number outside the neatline.</returns>
    [Benchmark]
    public double ConvertPoint() =>
        PdfGeoReferencing.IsInBounds(_transform, PointX, PointY) ? PdfGeoReferencing.ToWorld(_transform, PointX, PointY).First : double.NaN;
}

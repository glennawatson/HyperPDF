// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures recording a page that fills with one shading pattern at more distinct matrices than the shader cache holds, each used several times in a row.</summary>
public class SkiaPatternShaderBenchmarks
{
    /// <summary>The distinct matrices, more than the cache keeps.</summary>
    private const int DistinctMatrices = 150;

    /// <summary>The fills made in a row at each matrix.</summary>
    private const int Uses = 3;

    /// <summary>The width of each filled strip in points.</summary>
    private const int StripWidth = 3;

    /// <summary>The height of each filled strip in points.</summary>
    private const int StripHeight = 500;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>Builds the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder("/Pattern cs /P1 scn ");
        for (var strip = 0; strip < DistinctMatrices; strip++)
        {
            for (var use = 0; use < Uses; use++)
            {
                _ = content.Append(CultureInfo.InvariantCulture, $"q 1 0 0 1 {strip * StripWidth} {SkiaRecordingPage.PageHeight - StripHeight} cm 0 0 {StripWidth} {StripHeight} re f Q ");
            }
        }

        const string Pattern = "/Pattern << /P1 << /PatternType 2 /Shading << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 500 0] "
            + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >> >> >>";
        _pdf = SkiaRecordingPage.Create(content.ToString(), Pattern, false);
    }

    /// <summary>Opens the document and records and draws the page.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RecordPageOfPatternFillsAtManyMatrices()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }
}

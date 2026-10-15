// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures function sampling, gradient stop conversion and owned shader creation.</summary>
public class HyperPdfShadingShaderBenchmarks
{
    /// <summary>The document that owns the parsed functions and shading dictionaries.</summary>
    private PdfDocument _document = null!;

    /// <summary>The axial shading, parsed before measurement.</summary>
    private PdfShading _axial = null!;

    /// <summary>The radial shading, parsed before measurement.</summary>
    private PdfShading _radial = null!;

    /// <summary>Gets or sets whether both endpoint colours extend beyond the shading.</summary>
    [Params(false, true)]
    public bool Extended { get; set; }

    /// <summary>Parses the shading dictionaries and functions, leaving shader creation in the measured methods.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var common = $"/ColorSpace /DeviceRGB /Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> /Extend [{(Extended ? "true true" : "false false")}]";
        _document = BenchmarkDrawing.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /Shading << /S1 << /ShadingType 2 /Coords [0 0 100 0] {common} >> "
                + $"/S2 << /ShadingType 3 /Coords [50 50 0 50 50 50] {common} >> >> >> >>"),
            null);
        var shadings = PdfDocumentPages.GetPage(_document, 0).Resources!.GetDictionary(KnownName.Shading)!;
        _axial = PdfShading.Parse(shadings.Get(_document.Objects.Names.Intern("S1")), null)!;
        _radial = PdfShading.Parse(shadings.Get(_document.Objects.Names.Intern("S2")), null)!;
    }

    /// <summary>Releases the document and its parsed resources.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Builds and disposes a new axial shader, including all stop sampling and conversion.</summary>
    [Benchmark]
    public void Axial()
    {
        using var shader = ShadingShaders.Create(_axial);
    }

    /// <summary>Builds and disposes a new radial shader, including all stop sampling and conversion.</summary>
    [Benchmark]
    public void Radial()
    {
        using var shader = ShadingShaders.Create(_radial);
    }
}

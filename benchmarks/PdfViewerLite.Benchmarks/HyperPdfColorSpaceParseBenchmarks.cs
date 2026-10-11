// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures the lookup of a resource default colour space, including its recursion guard.</summary>
public class HyperPdfColorSpaceParseBenchmarks
{
    /// <summary>The source document that owns the resource names.</summary>
    private PdfDocument _document = null!;

    /// <summary>The resource dictionary with a recursive /DefaultRGB.</summary>
    private PdfDictionary _resources = null!;

    /// <summary>Creates a document and a colour resource with a recursive default.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.CreateTagged(), null);
        _resources = new(_document.Objects);
        _resources.Add(_document.Objects.Names.Intern("DefaultRGB"), PdfValue.FromName(KnownName.DeviceRGB));
    }

    /// <summary>Closes the fixture document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Parses a device space through its recursive default.</summary>
    /// <returns>The space's component count.</returns>
    [Benchmark]
    public int ParseDefaultRgb() => PdfColorSpace.Parse(PdfValue.FromName(KnownName.DeviceRGB), _resources).Components;
}

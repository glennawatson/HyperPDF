// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Layers;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures HyperPDF's layer handling: reading the layers and showing or hiding one. PDFium has no layer API, so
/// <see cref="LayerBenchmarks"/> measures its rewrite-and-reopen path for comparison.
/// </summary>
public class HyperPdfLayerBenchmarks
{
    /// <summary>The layered document's bytes.</summary>
    private byte[] _file = [];

    /// <summary>The open document's layers.</summary>
    private PdfOptionalContent _content = null!;

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The hidden layer's id.</summary>
    private int _notes;

    /// <summary>The visibility the toggle benchmark sets next.</summary>
    private bool _show;

    /// <summary>Opens the layered document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _file = TestPdf.CreateWithLayers();
        _document = PdfDocumentReader.Open(_file, null);
        _content = PdfDocumentLayers.GetOptionalContent(_document);
        _notes = _content.Layers[1].Id;
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Opens the bytes and reads the layers.</summary>
    /// <returns>The layer count.</returns>
    [Benchmark]
    public int ReadLayers()
    {
        using var document = PdfDocumentReader.Open(_file, null);
        return PdfDocumentLayers.GetOptionalContent(document).Layers.Count;
    }

    /// <summary>Shows or hides a layer.</summary>
    /// <returns>Whether it applied.</returns>
    [Benchmark]
    public bool ToggleLayer()
    {
        _show = !_show;
        return _content.SetVisible(_notes, _show);
    }
}

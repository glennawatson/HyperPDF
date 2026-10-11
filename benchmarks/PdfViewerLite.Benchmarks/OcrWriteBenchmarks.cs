// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Ocr;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures the steps that change a scanned page, so each iteration starts from a freshly opened scan: writing the
/// recognised words as an invisible text layer, and the whole page from render to text layer. Allocations are checked
/// from the EventPipe trace.
/// </summary>
[InvocationCount(1, 1)]
public class OcrWriteBenchmarks
{
    /// <summary>The scan resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The reused word list.</summary>
    private readonly List<OcrWord> _words = [];

    /// <summary>The recogniser.</summary>
    private TesseractEngine _engine = null!;

    /// <summary>The scanned page as a PDF.</summary>
    private byte[] _scan = [];

    /// <summary>The words recognised on the scan.</summary>
    private OcrWord[] _recognized = [];

    /// <summary>The scan opened for this iteration.</summary>
    private BenchmarkPdfDocument? _document;

    /// <summary>Makes the scan and recognises it once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _engine = new("eng");
        using var source = new BenchmarkPdfDocument(TestPdf.Create(1));
        var size = source.Document.GetPageSizes()[0];
        var width = (int)MathF.Ceiling(size.Width * ScanScale);
        var height = (int)MathF.Ceiling(size.Height * ScanScale);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = source.Document.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
        var grey = new byte[width * height];
        OcrRunner.ToGrey(pixels, grey);
        _scan = TestPdf.CreateScan(grey, width, height);
        _engine.Recognize(grey, width, height, ScanScale, _words);
        _recognized = [.. _words];
    }

    /// <summary>Opens a fresh copy of the scan.</summary>
    [IterationSetup]
    public void OpenScan() => _document = new(_scan);

    /// <summary>Closes the scan.</summary>
    [IterationCleanup]
    public void CloseScan() => _document?.Dispose();

    /// <summary>Closes Tesseract.</summary>
    [GlobalCleanup]
    public void Cleanup() => _engine.Dispose();

    /// <summary>Writes the recognised words onto the scanned page.</summary>
    /// <returns>The number written.</returns>
    [Benchmark]
    public int WriteTextLayer() => DocumentFeature<ITextLayerWriter>.Cast(_document!.Document)!.AddTextLayer(0, _recognized);

    /// <summary>Renders, recognises and writes the text layer of the scanned page.</summary>
    /// <returns>The number of words written.</returns>
    [Benchmark]
    public int RecognizePage() => OcrRunner.RecognizePage(_document!.Document, DocumentFeature<ITextLayerWriter>.Cast(_document.Document)!, _engine, 0, _words).Words;
}

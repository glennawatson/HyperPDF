// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.Ocr;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the parts of text recognition that can run repeatedly on one document: converting a rendered strip to
/// greyscale, recognising a scanned page with the managed recogniser, and skipping a page that already has text. Allocations are
/// checked from the EventPipe trace.
/// </summary>
public class OcrBenchmarks
{
    /// <summary>The scan resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The rows in a rendered strip.</summary>
    private const int StripRows = 256;

    /// <summary>The reused word list.</summary>
    private readonly List<OcrWord> _words = [];

    /// <summary>The recogniser.</summary>
    private ManagedOcrEngine _engine = null!;

    /// <summary>A document with text.</summary>
    private BenchmarkPdfDocument _text = null!;

    /// <summary>The scanned page as greyscale pixels.</summary>
    private byte[] _grey = [];

    /// <summary>A rendered BGRA strip.</summary>
    private byte[] _strip = [];

    /// <summary>The greyscale strip.</summary>
    private byte[] _greyStrip = [];

    /// <summary>The scan width in pixels.</summary>
    private int _width;

    /// <summary>The scan height in pixels.</summary>
    private int _height;

    /// <summary>Renders the test page at scanning resolution and starts the recogniser.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _engine = new("eng");
        _text = new(TestPdf.Create(1));
        var size = _text.Document.GetPageSizes()[0];
        _width = (int)MathF.Ceiling(size.Width * ScanScale);
        _height = (int)MathF.Ceiling(size.Height * ScanScale);
        var pixels = new byte[_width * _height * BytesPerPixel];
        _ = _text.Document.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, _width, _height, _width * BytesPerPixel));
        _grey = new byte[_width * _height];
        OcrRunner.ToGrey(pixels, _grey);
        _strip = pixels[..(_width * StripRows * BytesPerPixel)];
        _greyStrip = new byte[_width * StripRows];
        _engine.Recognize(_grey, _width, _height, ScanScale, _words);
    }

    /// <summary>Closes the document and the recogniser.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _text.Dispose();
        _engine.Dispose();
    }

    /// <summary>Converts one rendered 300 DPI strip to greyscale.</summary>
    /// <returns>A converted pixel.</returns>
    [Benchmark]
    public byte ConvertStripToGrey()
    {
        OcrRunner.ToGrey(_strip, _greyStrip);
        return _greyStrip[0];
    }

    /// <summary>Recognises the words on a scanned letter-size page.</summary>
    /// <returns>The number of words.</returns>
    [Benchmark]
    public int RecognizeScannedPage()
    {
        _words.Clear();
        _engine.Recognize(_grey, _width, _height, ScanScale, _words);
        return _words.Count;
    }

    /// <summary>Asks to recognise a page that already has text, which is skipped.</summary>
    /// <returns>The status.</returns>
    [Benchmark]
    public OcrPageStatus SkipPageWithText() =>
        OcrRunner.RecognizePage(_text.Document, DocumentFeature<ITextLayerWriter>.Cast(_text.Document)!, _engine, 0, _words).Status;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.Ocr;
using HyperPdf.Ocr.Layout;
using HyperPdf.Ocr.Lstm;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the stages of the managed text recogniser on a page of text at scanning resolution: separating ink from paper,
/// finding the blobs and lines, reading one line with the network, and a whole page. Allocations are checked from the
/// EventPipe trace.
/// </summary>
public class ManagedOcrBenchmarks
{
    /// <summary>The scan resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The lines of text on the test page.</summary>
    private const int LinesPerPage = 30;

    /// <summary>The share of a letter's height a line window is at least as tall as.</summary>
    private const double MinimumWindowHeights = 1.5;

    /// <summary>The reused list of blobs.</summary>
    private readonly List<ComponentBox> _blobs = [];

    /// <summary>The reused list of words.</summary>
    private readonly List<OcrWord> _words = [];

    /// <summary>The reused list of characters.</summary>
    private readonly List<RecognizedCharacter> _characters = [];

    /// <summary>The recogniser.</summary>
    private ManagedOcrEngine _engine = null!;

    /// <summary>The working memory of the stages.</summary>
    private RecognitionScratch _scratch = null!;

    /// <summary>The model.</summary>
    private LstmModel _model = null!;

    /// <summary>The page as greyscale pixels.</summary>
    private byte[] _grey = [];

    /// <summary>The pixels of the first line.</summary>
    private byte[] _line = [];

    /// <summary>Where the first line was cut from the page.</summary>
    private LineWindow _window;

    /// <summary>The page width in pixels.</summary>
    private int _width;

    /// <summary>The page height in pixels.</summary>
    private int _height;

    /// <summary>Renders the page and loads the English model.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _engine = new("eng");
        _scratch = new();
        using var document = new BenchmarkPdfDocument(TestPdf.CreateTextFilled(1, LinesPerPage));
        var size = document.Document.GetPageSizes()[0];
        _width = (int)MathF.Ceiling(size.Width * ScanScale);
        _height = (int)MathF.Ceiling(size.Height * ScanScale);
        var pixels = new byte[_width * _height * BytesPerPixel];
        _ = document.Document.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, _width, _height, _width * BytesPerPixel));
        _grey = new byte[_width * _height];
        OcrRunner.ToGrey(pixels, _grey);
        var mask = InkMask.Build(_grey, _width, _height, _scratch);
        RunLabeller.Label(mask, _blobs, _scratch);
        var lines = new List<TextLine>();
        TextLineFinder.Find(_blobs, lines, _scratch);
        _window = LineWindow.For(lines[0], (int)(MinimumWindowHeights * TextLineFinder.TypicalHeight(_blobs, _scratch)), _width, _height);
        _line = new byte[_window.Pixels];
        _window.CopyFrom(_grey, _width, _line);
        var path = Path.Combine(AppContext.BaseDirectory, "tessdata", "eng.traineddata");
        _model = LstmModel.Load(TrainedData.Open(File.ReadAllBytes(path)));
        _engine.Recognize(_grey, _width, _height, ScanScale, _words);
        _ = ReadLine();
    }

    /// <summary>Closes the recogniser.</summary>
    [GlobalCleanup]
    public void Cleanup() => _engine.Dispose();

    /// <summary>Separates ink from paper on the page.</summary>
    /// <returns>The mask.</returns>
    [Benchmark]
    public InkMask BuildInkMask() => InkMask.Build(_grey, _width, _height, _scratch);

    /// <summary>Finds the blobs of ink on the page.</summary>
    /// <returns>The number of blobs.</returns>
    [Benchmark]
    public int LabelBlobs()
    {
        _blobs.Clear();
        RunLabeller.Label(_scratch.Ink, _blobs, _scratch);
        return _blobs.Count;
    }

    /// <summary>Reads one line of the page with the network.</summary>
    /// <returns>The number of characters read.</returns>
    [Benchmark]
    public int ReadLine() => LineReader.Read(_model, _line, _window, _characters, _scratch).Characters.Count;

    /// <summary>Recognises a whole page of thirty lines.</summary>
    /// <returns>The number of words.</returns>
    [Benchmark]
    public int RecognizePage()
    {
        _words.Clear();
        _engine.Recognize(_grey, _width, _height, ScanScale, _words);
        return _words.Count;
    }
}

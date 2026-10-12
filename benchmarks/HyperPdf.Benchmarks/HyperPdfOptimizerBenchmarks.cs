// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures <see cref="PdfOptimizer"/> writing to <see cref="Stream.Null"/>: a cached scanned corpus file
/// (loc-brown-v-board, JBIG2 pages), a cached tagged text file (verapdf-ua-notes), and a generated page with a 300 ppi
/// photograph that is downsampled and saved as JPEG. Generated stand-ins replace the corpus files when they are not
/// cached. Allocations come from the EventPipe trace.
/// </summary>
public class HyperPdfOptimizerBenchmarks
{
    /// <summary>The scanned corpus file.</summary>
    private const string ScanFile = "loc-brown-v-board.pdf";

    /// <summary>The tagged text corpus file.</summary>
    private const string TextFile = "verapdf-ua-notes.pdf";

    /// <summary>The pages of the generated text stand-in.</summary>
    private const int StandInPages = 20;

    /// <summary>The photo's width and height in pixels.</summary>
    private const int PhotoPixels = 1200;

    /// <summary>The photo's drawn size: four inches, so it is drawn at 300 ppi.</summary>
    private const int PhotoPoints = 288;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The highest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The period of the photo's waves.</summary>
    private const double Wave = 29.0;

    /// <summary>The strength of the photo's waves.</summary>
    private const double WaveStrength = 50.0;

    /// <summary>The scanned document.</summary>
    private PdfDocument _scan = null!;

    /// <summary>The text document.</summary>
    private PdfDocument _text = null!;

    /// <summary>The photo document.</summary>
    private PdfDocument _photo = null!;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "hyperpdf", "reference-pdfs");
        var scan = Path.Combine(folder, ScanFile);
        var text = Path.Combine(folder, TextFile);
        _scan = File.Exists(scan) ? PdfDocumentReader.Open(scan, null) : PdfDocumentReader.Open(TestPdf.CreateScan(new byte[PhotoPixels * PhotoPixels], PhotoPixels, PhotoPixels), null);
        _text = File.Exists(text) ? PdfDocumentReader.Open(text, null) : PdfDocumentReader.Open(TestPdf.CreateArticle(StandInPages), null);
        _photo = PdfDocumentReader.Open(PhotoPage(), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _scan.Dispose();
        _text.Dispose();
        _photo.Dispose();
    }

    /// <summary>Optimises the scanned file with the balanced preset.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long ScanBalanced() => PdfOptimizer.Optimize(_scan, Stream.Null, PdfOptimizeOptions.Balanced).BytesAfter;

    /// <summary>Optimises the tagged text file with the balanced preset.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long TextBalanced() => PdfOptimizer.Optimize(_text, Stream.Null, PdfOptimizeOptions.Balanced).BytesAfter;

    /// <summary>Optimises the tagged text file losslessly.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long TextKeepQuality() => PdfOptimizer.Optimize(_text, Stream.Null, PdfOptimizeOptions.KeepQuality).BytesAfter;

    /// <summary>Optimises the photo page with the smaller preset: downsampled to 150 ppi and saved as JPEG.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long PhotoSmaller() => PdfOptimizer.Optimize(_photo, Stream.Null, PdfOptimizeOptions.Smaller).BytesAfter;

    /// <summary>Builds a page with a 1200 pixel photograph drawn four inches wide.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] PhotoPage()
    {
        var samples = new byte[PhotoPixels * PhotoPixels * RgbBytes];
        for (var y = 0; y < PhotoPixels; y++)
        {
            for (var x = 0; x < PhotoPixels; x++)
            {
                var wave = Math.Sin(x / Wave) * Math.Cos(y / Wave) * WaveStrength;
                var offset = ((y * PhotoPixels) + x) * RgbBytes;
                samples[offset] = (byte)Math.Clamp((int)(((double)x * MaxSample / PhotoPixels) + wave), 0, MaxSample);
                samples[offset + 1] = (byte)Math.Clamp((int)(((double)y * MaxSample / PhotoPixels) - wave), 0, MaxSample);
                samples[offset + RgbBytes - 1] = (byte)Math.Clamp((int)((double)(x + y) * MaxSample / (PhotoPixels + PhotoPixels)), 0, MaxSample);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            zlib.Write(samples);
        }

        var image = MiniPdf.Stream(
            $"/Type /XObject /Subtype /Image /Width {PhotoPixels} /Height {PhotoPixels} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode",
            Encoding.Latin1.GetString(compressed.GetBuffer(), 0, (int)compressed.Length));
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, $"q {PhotoPoints} 0 0 {PhotoPoints} 72 72 cm /Im1 Do Q\n"),
            image);
    }
}

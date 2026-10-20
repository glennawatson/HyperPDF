// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Writing;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures cutting an embedded OpenType font down to the glyphs used, rewriting a high quality JPEG, and trimming the font
/// a redaction leaves behind. Allocations come from the EventPipe trace.
/// </summary>
public class FontAndJpegOptimizeBenchmarks
{
    /// <summary>The side of the JPEG in pixels.</summary>
    private const int JpegSide = 800;

    /// <summary>The JPEG quality, high enough that the balanced preset writes it again.</summary>
    private const int JpegQuality = 95;

    /// <summary>The text shown in the fonts.</summary>
    private const string FontText = "Hello World";

    /// <summary>The offset of the first letter in the page.</summary>
    private const int Margin = 72;

    /// <summary>The catalog of a one-page file.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The page tree of a one-page file.</summary>
    private const string OnePage = "<< /Type /Pages /Kids [3 0 R] /Count 1 >>";

    /// <summary>The number of colour channels whose sum the blue channel follows.</summary>
    private const int BlueDivisor = 2;

    /// <summary>The area over the second line of the TrueType page.</summary>
    private static readonly PdfRectangle SecondLine = new(20, 110, 330, 140);

    /// <summary>The CID-keyed OpenType font page.</summary>
    private PdfDocument _openType = null!;

    /// <summary>The JPEG page.</summary>
    private PdfDocument _jpeg = null!;

    /// <summary>The TrueType page the redaction runs on.</summary>
    private byte[] _trueType = [];

    /// <summary>Makes the pages.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _openType = PdfDocumentReader.Open(OpenTypePage(), null);
        _jpeg = PdfDocumentReader.Open(JpegPage(), null);
        _trueType = TrueTypePage();
    }

    /// <summary>Closes the pages.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _openType.Dispose();
        _jpeg.Dispose();
    }

    /// <summary>Optimises the font page losslessly with font subsetting on.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long OpenTypeSubset() => PdfOptimizer.Optimize(_openType, Stream.Null, PdfOptimizeOptions.KeepQuality).BytesAfter;

    /// <summary>Optimises the font page losslessly with font subsetting off, to show what the subsetting adds.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark(Baseline = true)]
    public long OpenTypeNoSubset() => PdfOptimizer.Optimize(_openType, Stream.Null, PdfOptimizeOptions.KeepQuality with { SubsetFonts = false }).BytesAfter;

    /// <summary>Optimises the page with a high quality JPEG, which the balanced preset estimates and writes again.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long JpegBalanced() => PdfOptimizer.Optimize(_jpeg, Stream.Null, PdfOptimizeOptions.Balanced).BytesAfter;

    /// <summary>Redacts a line of text and trims the outlines of the letters only that line used.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public long RedactAndTrimFont()
    {
        using var document = PdfDocumentReader.Open(_trueType, null);
        _ = PdfRedactions.Add(document, 0, [SecondLine], PdfRedactionAppearance.Black);
        using var output = new MemoryStream();
        _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
        return output.Length;
    }

    /// <summary>Opens the TrueType page and saves it compactly with nothing redacted, to show what the redaction adds.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int OpenAndSaveTrueType()
    {
        using var document = PdfDocumentReader.Open(_trueType, null);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default).Length;
    }

    /// <summary>Builds a page that shows text in the CID-keyed OpenType test font.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] OpenTypePage()
    {
        var file = TestCffFont.Create(new() { CidKeyed = true });
        var shown = string.Concat(FontText.Select(static letter => TestFont.GlyphOf(letter).ToString("X4", CultureInfo.InvariantCulture)));
        return MiniPdf.Build(
            Catalog,
            OnePage,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, string.Create(CultureInfo.InvariantCulture, $"BT /F1 24 Tf {Margin} 700 Td <{shown}> Tj ET\n")),
            "<< /Type /Font /Subtype /Type0 /BaseFont /PVLTestCidCff /Encoding /Identity-H /DescendantFonts [6 0 R] >>",
            "<< /Type /Font /Subtype /CIDFontType0 /BaseFont /PVLTestCidCff /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /DW 600 /FontDescriptor 7 0 R >>",
            "<< /Type /FontDescriptor /FontName /PVLTestCidCff /Flags 4 /FontBBox [0 -200 1000 800] /ItalicAngle 0 "
            + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile3 8 0 R >>",
            MiniPdf.Stream("/Subtype /OpenType", Encoding.Latin1.GetString(file)));
    }

    /// <summary>Builds a page with two lines in the embedded TrueType test font; the second line has letters the first does not.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] TrueTypePage()
    {
        var program = TestFont.Create();
        var widths = string.Join(' ', Enumerable.Repeat("600", '~' - ' ' + 1));
        return MiniPdf.Build(
            Catalog,
            OnePage,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, "BT /F1 20 Tf 20 150 Td (Public text) Tj 0 -30 Td (SECRET-ALPHA) Tj ET"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Font /Subtype /TrueType /BaseFont /PVLTestSans /FirstChar 32 /LastChar 126 /Widths [{widths}] /Encoding /WinAnsiEncoding /FontDescriptor 6 0 R >>"),
            "<< /Type /FontDescriptor /FontName /PVLTestSans /Flags 32 /FontBBox [0 -200 1000 800] /ItalicAngle 0 "
            + "/Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 7 0 R >>",
            MiniPdf.Stream(string.Create(CultureInfo.InvariantCulture, $"/Length1 {program.Length}"), Encoding.Latin1.GetString(program)));
    }

    /// <summary>Builds a page that draws a high quality JPEG.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] JpegPage()
    {
        using var bitmap = new SKBitmap(JpegSide, JpegSide);
        for (var y = 0; y < JpegSide; y++)
        {
            for (var x = 0; x < JpegSide; x++)
            {
                var blue = (x + y) * byte.MaxValue / (JpegSide * BlueDivisor);
                bitmap.SetPixel(x, y, new((byte)(x * byte.MaxValue / JpegSide), (byte)(y * byte.MaxValue / JpegSide), (byte)blue));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        var image3 = MiniPdf.Stream(
            string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width {JpegSide} /Height {JpegSide} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode"),
            Encoding.Latin1.GetString(data.ToArray()));
        return MiniPdf.Build(
            Catalog,
            OnePage,
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, "q 288 0 0 288 72 72 cm /Im1 Do Q\n"),
            image3);
    }
}

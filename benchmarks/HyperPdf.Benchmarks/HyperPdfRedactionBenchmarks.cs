// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures applying a redaction to a page of body text and to a page with a large picture, against opening the same
/// page and saving it compactly with nothing removed.
/// </summary>
public class HyperPdfRedactionBenchmarks
{
    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 48;

    /// <summary>The line spacing.</summary>
    private const int Leading = 14;

    /// <summary>The left margin and the baseline of the first line.</summary>
    private const int Margin = 72;

    /// <summary>The page height.</summary>
    private const int PageHeight = 792;

    /// <summary>The picture's width and height in pixels.</summary>
    private const int PictureSide = 1000;

    /// <summary>The words each line is made from.</summary>
    private const string LineText = "The quick brown fox jumps over the lazy dog while reading a long accessible document";

    /// <summary>The area over three lines of the text page.</summary>
    private static readonly PdfRectangle TextArea = new(Margin, 560, 400, 600);

    /// <summary>The area over part of the picture.</summary>
    private static readonly PdfRectangle PictureArea = new(150, 250, 300, 400);

    /// <summary>The text page.</summary>
    private byte[] _text = [];

    /// <summary>The picture page.</summary>
    private byte[] _picture = [];

    /// <summary>Builds the pages.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _text = CreateTextPage();
        _picture = CreatePicturePage();
    }

    /// <summary>Opens the text page and saves it compactly.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark(Baseline = true)]
    public int OpenAndSaveCompact()
    {
        using var document = PdfDocumentReader.Open(_text, null);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default).Length;
    }

    /// <summary>Opens the text page, marks three lines and applies the redaction.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public long RedactText() => Redact(_text, TextArea);

    /// <summary>Opens the picture page, marks part of the picture and applies the redaction, blanking the covered pixels.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public long RedactPicture() => Redact(_picture, PictureArea);

    /// <summary>Marks an area of a page and applies the redaction.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="area">The area.</param>
    /// <returns>The saved length.</returns>
    private static long Redact(byte[] pdf, PdfRectangle area)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        _ = PdfRedactions.Add(document, 0, [area], PdfRedactionAppearance.Black);
        using var output = new MemoryStream();
        _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
        return output.Length;
    }

    /// <summary>Builds a page of body text.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateTextPage()
    {
        var content = new StringBuilder("BT /F1 10 Tf\n");
        for (var line = 0; line < Lines; line++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"1 0 0 1 {Margin} {PageHeight - Margin - (line * Leading)} Tm ({LineText}) Tj\n");
        }

        _ = content.Append("ET");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>");
    }

    /// <summary>Builds a page with a grey picture.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePicturePage()
    {
        var samples = new byte[PictureSide * PictureSide];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(i % byte.MaxValue);
        }

        return TestPdf.CreateScan(samples, PictureSide, PictureSide);
    }
}

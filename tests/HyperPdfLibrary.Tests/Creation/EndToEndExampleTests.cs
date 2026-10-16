// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Examples;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Smoke tests for the end-to-end creation and SkiaSharp rendering example in <c>src/examples</c>.</summary>
public sealed class EndToEndExampleTests
{
    /// <summary>The PNG magic header byte 0.</summary>
    private const byte PngMagic0 = 0x89;

    /// <summary>The PNG magic header byte 1 ('P').</summary>
    private const byte PngMagic1 = 0x50;

    /// <summary>The PNG magic header byte 2 ('N').</summary>
    private const byte PngMagic2 = 0x4E;

    /// <summary>The PNG magic header byte 3 ('G').</summary>
    private const byte PngMagic3 = 0x47;

    /// <summary>The minimum expected PNG byte length.</summary>
    private const int MinPngBytes = 64;

    /// <summary>The index of the third byte in the PNG header.</summary>
    private const int PngMagicIndex2 = 2;

    /// <summary>The index of the fourth byte in the PNG header.</summary>
    private const int PngMagicIndex3 = 3;

    /// <summary>The expected number of H1 headings in the example document.</summary>
    private const int ExpectedHeadings = 2;

    /// <summary>The end-to-end example creates a valid tagged PDF, extracts its text, and renders its first page to PNG.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EndToEndExampleCreatesReadsAndRendersPdf(CancellationToken cancellationToken)
    {
        var report = await EndToEndExample.RunAsync(cancellationToken);

        await Assert.That(report.PageCount).IsEqualTo(1);
        await Assert.That(report.ExtractedText).Contains("HyperPDF End-to-End Report");
        await Assert.That(report.ExtractedText).Contains("Component Status");
        await Assert.That(report.ExtractedText).Contains("PdfDocumentCreator");
        await Assert.That(report.ExtractedText).Contains("PdfTextFlow");
        await Assert.That(report.ExtractedText).Contains("PdfPageRenderer");

        await Assert.That(report.RenderedWidth).IsGreaterThan(0);
        await Assert.That(report.RenderedHeight).IsGreaterThan(0);
        await Assert.That(report.FirstPagePngBytes.Length).IsGreaterThan(MinPngBytes);
        await Assert.That(report.FirstPagePngBytes[0]).IsEqualTo(PngMagic0);
        await Assert.That(report.FirstPagePngBytes[1]).IsEqualTo(PngMagic1);
        await Assert.That(report.FirstPagePngBytes[PngMagicIndex2]).IsEqualTo(PngMagic2);
        await Assert.That(report.FirstPagePngBytes[PngMagicIndex3]).IsEqualTo(PngMagic3);

        using var document = PdfDocumentReader.Open(report.PdfBytes, null);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(CountType(root, "H1")).IsEqualTo(ExpectedHeadings);
        await Assert.That(CountType(root, "Figure")).IsEqualTo(1);
        await Assert.That(CountType(root, "Table")).IsEqualTo(1);
        await Assert.That(accessibility.Findings.Select(static f => f.Code).ToArray()).IsEmpty();
        await CreationTestKit.CheckWithQpdfAsync("end-to-end-example", report.PdfBytes, cancellationToken);
    }

    /// <summary>The example CLI entry point writes <c>example.pdf</c> and <c>page-1.png</c> when given an output folder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgramMainWritesOutputFilesWhenDirectoryIsProvided()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"hyperpdf-example-test-{Guid.NewGuid():N}");
        try
        {
            await Program.Main([tempDir]);
            var pdfPath = Path.Combine(tempDir, "example.pdf");
            var pngPath = Path.Combine(tempDir, "page-1.png");

            await Assert.That(File.Exists(pdfPath)).IsTrue();
            await Assert.That(File.Exists(pngPath)).IsTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    /// <summary>Counts the elements of a type under an element, including itself.</summary>
    /// <param name="element">The element.</param>
    /// <param name="type">The raw structure type.</param>
    /// <returns>The count.</returns>
    private static int CountType(PdfStructureElement element, string type)
    {
        var count = element.RawType == type ? 1 : 0;
        foreach (var kid in element.Kids)
        {
            count += kid.Element is { } child ? CountType(child, type) : 0;
        }

        return count;
    }
}

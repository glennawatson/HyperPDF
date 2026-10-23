// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the adapter's colour separation source and the separation warnings of page edits.</summary>
public sealed class HyperPdfSeparationTests
{
    /// <summary>The black separation of printed page 1.</summary>
    private const int BlackPage = 3;

    /// <summary>The separations of printed page 1 left after one is lost.</summary>
    private const int ThreeKept = 3;

    /// <summary>The selection that deletes the black separation of printed page 1.</summary>
    private static readonly int[] BlackOnly = [BlackPage];

    /// <summary>The selection that extracts the first three separations of printed page 1.</summary>
    private static readonly int[] FirstThree = [0, 1, 2];

    /// <summary>The source reports both printed pages with one-based numbers and their colourants.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SourceReportsPrintedPages()
    {
        using var document = Open(SeparationPdf.CreatePreseparated());
        var source = DocumentFeature<ISeparationSource>.Get(document)!;
        var separations = await source.GetSeparationsAsync(CancellationToken.None);

        await Assert.That(separations.ProblemCount).IsEqualTo(0);
        await Assert.That(separations.Groups.Length).IsEqualTo(SeparationPdf.PrintedPages);
        await Assert.That(separations.Groups[0].PrintedPage).IsEqualTo(1);
        await Assert.That(separations.Groups[1].PrintedPage).IsEqualTo(SeparationPdf.PrintedPages);
        await Assert.That(separations.Groups[1].Pages.Select(static page => page.Colorant)).IsEquivalentTo(SeparationPdf.SecondColorants);
        await Assert.That(source.GetSeparations().Groups[0].Pages.Select(static page => page.PageIndex)).IsEquivalentTo(SeparationPdf.FirstGroup);
    }

    /// <summary>An ordinary document reports no printed pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryDocumentHasNone()
    {
        using var document = Open(TestPdf.Create(1));
        await Assert.That(DocumentFeature<ISeparationSource>.Get(document)!.GetSeparations()).IsSameReferenceAs(DocumentSeparations.Empty);
    }

    /// <summary>Deleting a separation reports the printed page that lost it, and undo restores the group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletionReportsTheLostSeparation()
    {
        using var document = Open(SeparationPdf.CreatePreseparated());
        var manager = HyperPdfDocumentPageManagement.GetPageManager(document);
        var report = new PageEditReport();
        await manager.ApplyAsync(new(PageEditKind.Delete, BlackOnly, 0), report, CancellationToken.None);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new SeparationChange(1, SeparationPdf.ColorantsPerPage, ThreeKept, 0)]);
        await Assert.That(DocumentFeature<ISeparationSource>.Get(document)!.GetSeparations().Groups[0].Pages.Length).IsEqualTo(ThreeKept);
        await Assert.That(await manager.UndoAsync(CancellationToken.None)).IsTrue();
        await Assert.That(DocumentFeature<ISeparationSource>.Get(document)!.GetSeparations().Groups[0].Pages.Length).IsEqualTo(SeparationPdf.ColorantsPerPage);
    }

    /// <summary>Extracting part of a printed page reports it and writes a file whose separations read cleanly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractionReportsAPartialPrintedPage()
    {
        using var document = Open(SeparationPdf.CreatePreseparated());
        var report = new PageEditReport();
        await using var output = new MemoryStream();
        await HyperPdfDocumentPageManagement.GetPageManager(document).ExtractAsync(FirstThree, output, report, CancellationToken.None);
        using var extracted = PdfDocumentReader.Open(output.ToArray(), null);
        var info = PdfDocumentSeparations.GetSeparations(extracted);

        await Assert.That(report.SeparationChanges).IsEquivalentTo([new SeparationChange(1, SeparationPdf.ColorantsPerPage, ThreeKept, 0)]);
        await Assert.That(info.Findings).IsEmpty();
        await Assert.That(info.Groups[0].Separations.Length).IsEqualTo(ThreeKept);
    }

    /// <summary>Opens a document through the adapter.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns>The document.</returns>
    private static HyperPdfDocument Open(byte[] bytes) => new(SkiaTestDocuments.Open(bytes), "memory.pdf");
}

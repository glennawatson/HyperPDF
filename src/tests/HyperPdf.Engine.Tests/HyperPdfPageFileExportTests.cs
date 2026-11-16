// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Exporting;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that pages are written to files by the page export signal for every kind of file.</summary>
public sealed class HyperPdfPageFileExportTests
{
    /// <summary>The resolution used for the pictures.</summary>
    private const int Dpi = 36;

    /// <summary>The pages in the small document.</summary>
    private const int SmallPages = 2;

    /// <summary>The pages in the document the several page file is written from.</summary>
    private const int SeveralPages = 3;

    /// <summary>Every kind of file holds the page the export was asked for.</summary>
    /// <param name="format">The kind of file.</param>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(PageFileFormat.Png)]
    [Arguments(PageFileFormat.Jpeg)]
    [Arguments(PageFileFormat.Svg)]
    [Arguments(PageFileFormat.PostScript)]
    [Arguments(PageFileFormat.EncapsulatedPostScript)]
    public async Task EveryFormatIsWritten(PageFileFormat format, CancellationToken cancellationToken)
    {
        using var document = new HyperPdfDocument(SkiaTestDocuments.Open(TestPdf.Create(SmallPages)), "export.pdf");
        var exporter = (IPageFileExporter)document.GetFeature(typeof(IPageFileExporter))!;
        await using var destination = new MemoryStream();

        var result = await exporter.Export([0], format, new(Dpi, true), destination).ToTask(cancellationToken);

        await Assert.That(result.Written).IsTrue();
        await Assert.That(destination.Length).IsGreaterThan(0);
    }

    /// <summary>A file that holds several pages is written once every page has been prepared.</summary>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PostScriptHoldsSeveralPages(CancellationToken cancellationToken)
    {
        using var document = new HyperPdfDocument(SkiaTestDocuments.Open(TestPdf.Create(SeveralPages)), "export-pages.pdf");
        var exporter = (IPageFileExporter)document.GetFeature(typeof(IPageFileExporter))!;
        await using var one = new MemoryStream();
        await using var several = new MemoryStream();

        _ = await exporter.Export([0], PageFileFormat.PostScript, new(Dpi, true), one).ToTask(cancellationToken);
        _ = await exporter.Export([.. Enumerable.Range(0, SeveralPages)], PageFileFormat.PostScript, new(Dpi, true), several).ToTask(cancellationToken);

        await Assert.That(several.Length).IsGreaterThan(one.Length);
    }

    /// <summary>A request that cannot be met fails the signal when it is subscribed to, and writes nothing.</summary>
    /// <param name="cancellationToken">Cancels the export.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BadRequestFailsTheSignal(CancellationToken cancellationToken)
    {
        using var document = new HyperPdfDocument(SkiaTestDocuments.Open(TestPdf.Create(1)), "export-bad.pdf");
        var exporter = (IPageFileExporter)document.GetFeature(typeof(IPageFileExporter))!;
        await using var destination = new MemoryStream();

        var pending = exporter.Export([], PageFileFormat.Png, new(Dpi, true), destination);

        await Assert.That(async () => await pending.ToTask(cancellationToken)).Throws<ArgumentException>();
        await Assert.That(destination.Length).IsEqualTo(0);
    }
}

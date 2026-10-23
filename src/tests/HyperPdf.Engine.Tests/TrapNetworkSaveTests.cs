// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that saving through the app's editor tells the user when a trap network was removed.</summary>
public sealed class TrapNetworkSaveTests
{
    /// <summary>The note's position across the page.</summary>
    private const float NoteX = 100;

    /// <summary>The note's position down the page.</summary>
    private const float NoteY = 100;

    /// <summary>The note's colour as 0xRRGGBB.</summary>
    private const uint NoteColor = 0xFFD966;

    /// <summary>The index of the page without a trap network.</summary>
    private const int SecondPage = 1;

    /// <summary>The page list naming only the first page.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>Adding a note to the trapped page removes its out of date network and the save report says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoteOnTrappedPageReportsRemovedNetwork()
    {
        using var source = new HyperPdfTestDocument(TrapNetworkPdf.Build(TrapNetworkPdf.Versioned));
        var editor = DocumentFeature<IAnnotationEditor>.Cast(source.Document)!;
        _ = editor.AddNote(0, new(NoteX, NoteY), "Check the red box", NoteColor);
        var report = new DocumentSaveReport();
        await using var output = new MemoryStream();

        var saved = await editor.SaveAsync(output, report, CancellationToken.None);

        await Assert.That(saved).IsTrue();
        await Assert.That(report.TrapNetworkWarnings).Count().IsEqualTo(1);
        await Assert.That(report.TrapNetworkWarnings[0]).Contains("page 1");
        using var copy = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfTrapNetworkPages.Find(copy.Objects, CancellationToken.None)).IsNull();
    }

    /// <summary>Extracting a trapped page whose network is out of date reports the warning through the page manager.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractReportsLeftOutNetwork()
    {
        using var source = new HyperPdfTestDocument(TrapNetworkPdf.Build(TrapNetworkPdf.Dated("D:20210101000000Z")));
        var manager = HyperPdf.Engine.HyperPdfDocumentPageManagement.GetPageManager(source.Document);
        var report = new PageEditReport();
        await using var output = new MemoryStream();

        await manager.ExtractAsync(FirstPage, output, report, CancellationToken.None);

        await Assert.That(report.TrapNetworkWarnings).Count().IsEqualTo(1);
        await Assert.That(report.TrapNetworkWarnings[0]).Contains("did not copy the trap network of page 1");
    }

    /// <summary>A note on another page keeps the network, and the report has no warning.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoteOnOtherPageKeepsNetwork()
    {
        using var source = new HyperPdfTestDocument(TrapNetworkPdf.Build(TrapNetworkPdf.Versioned));
        var editor = DocumentFeature<IAnnotationEditor>.Cast(source.Document)!;
        _ = editor.AddNote(SecondPage, new(NoteX, NoteY), "A note elsewhere", NoteColor);
        var report = new DocumentSaveReport();
        await using var output = new MemoryStream();

        var saved = editor.Save(output, report);

        await Assert.That(saved).IsTrue();
        await Assert.That(report.TrapNetworkWarnings).IsEmpty();
        using var copy = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfTrapNetworkPages.Find(copy.Objects, CancellationToken.None)).IsNotNull();
    }
}

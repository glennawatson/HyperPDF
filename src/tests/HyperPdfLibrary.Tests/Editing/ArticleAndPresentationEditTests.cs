// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Portfolio;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for article threads, page transitions, measurement viewports and portfolios.</summary>
public sealed class ArticleAndPresentationEditTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The beads in the sample thread.</summary>
    private const int BeadCount = 3;

    /// <summary>The left edge of a bead.</summary>
    private const float BeadLeft = 72;

    /// <summary>The bottom edge of a bead.</summary>
    private const float BeadBottom = 100;

    /// <summary>The right edge of a bead.</summary>
    private const float BeadRight = 300;

    /// <summary>The top edge of a bead.</summary>
    private const float BeadTop = 400;

    /// <summary>The seconds before the sample page advances.</summary>
    private const double Advance = 5;

    /// <summary>The seconds the sample transition takes.</summary>
    private const double Duration = 1.5;

    /// <summary>How far a written conversion factor may differ from the value given, which is written with ten digits.</summary>
    private const double ConversionTolerance = 1e-9;

    /// <summary>The name of the attached file the portfolio shows first.</summary>
    private const string SecondFile = "b.txt";

    /// <summary>The decimal places of the sample number format.</summary>
    private const int Precision = 2;

    /// <summary>The beads of the sample thread that sit on the first page.</summary>
    private const int BeadsOnFirstPage = 2;

    /// <summary>A direction the standard does not allow.</summary>
    private const int OddDirection = 45;

    /// <summary>The wipe direction in degrees.</summary>
    private const int WipeDirection = 90;

    /// <summary>The third part of ISO 19005.</summary>
    private const int PdfAPart3 = 3;

    /// <summary>The conversion of one point to inches.</summary>
    private const double PointsToInches = 1.0 / 72.0;

    /// <summary>A thread reopens with its title and beads in order, every bead is on its page's list, and removal cleans up.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreadReopensAndIsRemoved(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var box = new PdfRectangle(BeadLeft, BeadBottom, BeadRight, BeadTop);

        _ = await PdfDocumentThreadEditing.AddThreadAsync(document, "Story", [new(0, box), new(1, box), new(0, box)], cancellationToken);

        var file = EditChecks.SaveCompact(document);
        using (var reopened = PdfDocumentReader.Open(file, null))
        {
            var thread = PdfDocumentCatalog.GetThreads(reopened).Single();
            await Assert.That(thread.Title).IsEqualTo("Story");
            await Assert.That(thread.Beads.Select(static bead => bead.PageIndex)).IsEquivalentTo([0, 1, 0]);
            await Assert.That(PdfDocumentPages.GetPage(reopened, 0).Dictionary.GetArray(KnownName.B)!.Count).IsEqualTo(BeadsOnFirstPage);
        }

        await EditChecks.AssertQpdfCleanAsync("thread", file, cancellationToken);
        await Assert.That(await PdfDocumentThreadEditing.RemoveThreadAsync(document, 0, cancellationToken)).IsTrue();
        await Assert.That(await PdfDocumentThreadEditing.RemoveThreadAsync(document, 0, cancellationToken)).IsFalse();
        await Assert.That(PdfDocumentCatalog.GetThreads(document)).IsEmpty();
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.B)).IsNull();
        await EditChecks.AssertQpdfCleanAsync("thread-removed", EditChecks.SaveCompact(document), cancellationToken);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentCatalog.GetThreads(document).Single().Beads.Length).IsEqualTo(BeadCount);
        await Assert.That(() => PdfDocumentThreadEditing.AddThread(document, null, [])).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentThreadEditing.AddThread(document, null, [new(0, null)])).Throws<ArgumentException>();
    }

    /// <summary>A transition and its advance time reopen, a wrong direction is refused, and removal clears both.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransitionReopensAndIsRemoved(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        await PdfDocumentTransitionEditing.SetTransitionAsync(document, 1, new("Wipe", Duration, "H", "I", WipeDirection, 1, false, Advance), cancellationToken);

        var file = EditChecks.SaveIncremental(document);
        using (var reopened = PdfDocumentReader.Open(file, null))
        {
            var transition = PdfDocumentViewerPreferences.GetTransition(reopened, PdfDocumentPages.GetPage(reopened, 1));
            await Assert.That(transition).IsNotNull();
            await Assert.That(transition!.Style).IsEqualTo("Wipe");
            await Assert.That(transition.Duration).IsEqualTo(Duration);
            await Assert.That(transition.Direction).IsEqualTo(WipeDirection);
            await Assert.That(transition.AdvanceAfter).IsEqualTo(Advance);
        }

        await EditChecks.AssertQpdfCleanAsync("transition", file, cancellationToken);
        await Assert.That(() => PdfDocumentTransitionEditing.SetTransition(document, 1, new("Wipe", 1, "H", "I", OddDirection, 1, false, null))).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentTransitionEditing.SetTransition(document, 1, new("Spin", 1, "H", "I", 0, 1, false, null))).Throws<ArgumentException>();
        PdfDocumentTransitionEditing.SetTransition(document, 1, null);
        await Assert.That(PdfDocumentViewerPreferences.GetTransition(document, PdfDocumentPages.GetPage(document, 1))).IsNull();
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Dictionary.ContainsKey(document.Objects.Names.Intern("Dur"u8))).IsFalse();
    }

    /// <summary>A measure viewport reopens with its scale and units; geospatial measures are refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasureViewportReopens(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var format = new PdfNumberFormat("in", PointsToInches, "D", Precision, false, ",", ".", string.Empty, " in", "S");
        var measure = new PdfMeasure("RL", "1 in = 1 in", [format], [], [format], [format], [], [], [], 1, null);

        await PdfDocumentTransitionEditing.SetViewportsAsync(
            document,
            0,
            [new(new PdfRectangle(BeadLeft, BeadBottom, BeadRight, BeadTop), "Plan", measure) { BoundingBox = [BeadLeft, BeadBottom, BeadRight, BeadTop] }],
            cancellationToken);

        var file = EditChecks.SaveCompact(document);
        using (var reopened = PdfDocumentReader.Open(file, null))
        {
            var viewport = PdfDocumentMeasure.GetViewports(reopened, PdfDocumentPages.GetPage(reopened, 0)).Single();
            await Assert.That(viewport.Name).IsEqualTo("Plan");
            await Assert.That(viewport.Measure!.Ratio).IsEqualTo("1 in = 1 in");
            await Assert.That(viewport.Measure.Distance.Single().Unit).IsEqualTo("in");
            await Assert.That(viewport.Measure.Distance.Single().Conversion).IsEqualTo(PointsToInches).Within(ConversionTolerance);
        }

        await EditChecks.AssertQpdfCleanAsync("viewport-scale", file, cancellationToken);
        var geo = measure with { Subtype = "GEO" };
        await Assert.That(() => PdfDocumentTransitionEditing.SetViewports(document, 0, [new(new PdfRectangle(0, 0, 1, 1), null, geo)])).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentTransitionEditing.SetViewports(document, 0, [new(null, null, null)])).Throws<ArgumentException>();
        PdfDocumentTransitionEditing.SetViewports(document, 0, []);
        await Assert.That(PdfDocumentMeasure.GetViewports(document, PdfDocumentPages.GetPage(document, 0))).IsEmpty();
    }

    /// <summary>A portfolio reopens with its schema, sort, view and first file, and can be turned back into a document.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PortfolioReopensAndIsRemoved(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = PdfDocumentAttachmentEditing.AddAttachment(document, new("a.txt", "a"u8.ToArray()));
        _ = PdfDocumentAttachmentEditing.AddAttachment(document, new(SecondFile, "b"u8.ToArray()));
        var spec = new PdfPortfolioSpec([new("name", "F", "File name", 0, true, false), new("size", "Size", "Bytes", 1, true, false)])
        {
            Sort = [new("size", false)],
            InitialDocument = SecondFile,
            View = "T",
        };

        await PdfDocumentPortfolioEditing.SetPortfolioAsync(document, spec, cancellationToken);

        var file = EditChecks.SaveCompact(document);
        using (var reopened = PdfDocumentReader.Open(file, null))
        {
            await Assert.That(PdfDocumentPortfolio.IsPortfolio(reopened)).IsTrue();
            var portfolio = PdfDocumentPortfolio.GetPortfolio(reopened)!;
            await Assert.That(portfolio.Schema.Select(static field => field.Key)).IsEquivalentTo(["name", "size"]);
            await Assert.That(portfolio.Sort.Single().Key).IsEqualTo("size");
            await Assert.That(portfolio.Sort.Single().Ascending).IsFalse();
            await Assert.That(portfolio.InitialDocument).IsEqualTo(SecondFile);
            await Assert.That(portfolio.View).IsEqualTo("T");
        }

        await EditChecks.AssertQpdfCleanAsync("portfolio", file, cancellationToken);
        await Assert.That(() => PdfDocumentPortfolioEditing.SetPortfolio(document, spec with { InitialDocument = "none.txt" })).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentPortfolioEditing.SetPortfolio(document, spec with { Sort = [new("missing", true)] })).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentPortfolioEditing.SetPortfolio(document, spec with { View = "Z" })).Throws<ArgumentException>();
        await Assert.That(await PdfDocumentPortfolioEditing.RemovePortfolioAsync(document, cancellationToken)).IsTrue();
        await Assert.That(PdfDocumentPortfolio.IsPortfolio(document)).IsFalse();
        await Assert.That(PdfDocumentAttachments.GetAttachments(document).Count).IsEqualTo(Pages);
    }

    /// <summary>A PDF/A-1 claim refuses a portfolio and a certification refuses every one of these edits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimsLimitTheEdits()
    {
        using var a1 = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        using var a3 = PdfDocumentReader.Open(PdfAFixtures.Create(new(PdfAPart3, "B")), null);
        using var certified = PdfDocumentReader.Open(EditGuardSamples.Certified(1), null);

        await Assert.That(() => PdfDocumentPortfolioEditing.SetPortfolio(a1, new([]))).Throws<InvalidOperationException>();
        PdfDocumentPortfolioEditing.SetPortfolio(a3, new([]));
        await Assert.That(() => PdfDocumentTransitionEditing.SetTransition(certified, 0, null)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentThreadEditing.RemoveThread(certified, 0)).Throws<InvalidOperationException>();
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentCatalogEditing"/>.</summary>
public sealed class CatalogEditTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The page the opening action goes to.</summary>
    private const int TargetPage = 2;

    /// <summary>The number of copies the sample asks for.</summary>
    private const int Copies = 3;

    /// <summary>The third part of ISO 19005.</summary>
    private const int PdfAPart3 = 3;

    /// <summary>The edits the first test makes and then undoes.</summary>
    private const int EditsMade = 4;

    /// <summary>A page layout number the standard does not define.</summary>
    private const int UnknownLayout = 99;

    /// <summary>The script the sample runs.</summary>
    private const string Script = "app.alert('saved');";

    /// <summary>Preferences, layout, mode and the opening action reopen with the same values, undo puts each back, and the files pass qpdf.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpeningSettingsReopenAndUndo(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        await PdfDocumentCatalogEditing.SetViewerPreferencesAsync(
            document,
            new()
            {
                HideToolbar = true,
                FitWindow = true,
                DisplayDocTitle = true,
                Direction = "R2L",
                PrintScaling = "None",
                Duplex = "DuplexFlipLongEdge",
                NumCopies = Copies,
                PrintPageRange = [1, TargetPage],
            },
            cancellationToken);
        await PdfDocumentCatalogEditing.SetPageLayoutAsync(document, PdfPageLayout.TwoColumnRight, cancellationToken);
        await PdfDocumentCatalogEditing.SetPageModeAsync(document, PdfPageMode.UseAttachments, cancellationToken);
        await PdfDocumentCatalogEditing.SetOpenActionAsync(document, PdfBookmarkTarget.ToPage(PdfExplicitDestination.FitPage(TargetPage)), cancellationToken);

        var incremental = EditChecks.SaveIncremental(document);
        var compact = EditChecks.SaveCompact(document);
        foreach (var file in new[] { incremental, compact })
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            var preferences = PdfDocumentViewerPreferences.GetViewerPreferences(reopened);
            await Assert.That(preferences.HideToolbar).IsTrue();
            await Assert.That(preferences.FitWindow).IsTrue();
            await Assert.That(preferences.HideMenubar).IsFalse();
            await Assert.That(preferences.DisplayDocTitle).IsTrue();
            await Assert.That(preferences.Direction).IsEqualTo("R2L");
            await Assert.That(preferences.PrintScaling).IsEqualTo("None");
            await Assert.That(preferences.Duplex).IsEqualTo("DuplexFlipLongEdge");
            await Assert.That(preferences.NumCopies).IsEqualTo(Copies);
            await Assert.That(preferences.PrintPageRange).IsEquivalentTo([1, TargetPage]);
            await Assert.That(preferences.PageLayout).IsEqualTo("TwoColumnRight");
            await Assert.That(preferences.PageMode).IsEqualTo("UseAttachments");
            var open = PdfDocumentActions.GetOpenAction(reopened);
            await Assert.That(open).IsNotNull();
            await Assert.That(open!.Subtype).IsEqualTo("GoTo");
        }

        await EditChecks.AssertQpdfCleanAsync("catalog-incremental", incremental, cancellationToken);
        await EditChecks.AssertQpdfCleanAsync("catalog-compact", compact, cancellationToken);

        for (var i = 0; i < EditsMade; i++)
        {
            await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        }

        var undone = PdfDocumentViewerPreferences.GetViewerPreferences(document);
        await Assert.That(undone.HideToolbar).IsFalse();
        await Assert.That(undone.PageLayout).IsEqualTo("SinglePage");
        await Assert.That(undone.PageMode).IsEqualTo("UseNone");
        await Assert.That(PdfDocumentActions.GetOpenAction(document)).IsNull();
    }

    /// <summary>Defaults remove their entries, a name opens as a GoTo action, and a removed action is gone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultsRemoveEntriesAndNamesBecomeActions()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfDocumentCatalogEditing.SetPageLayout(document, PdfPageLayout.OneColumn);
        PdfDocumentCatalogEditing.SetPageLayout(document, PdfPageLayout.SinglePage);
        PdfDocumentCatalogEditing.SetOpenAction(document, PdfBookmarkTarget.ToNamedDestination("chapter"));

        await Assert.That(document.Catalog.ContainsKey(document.Objects.Names.Intern("PageLayout"u8))).IsFalse();
        await Assert.That(document.Catalog.GetDictionary(HyperPdfLibrary.Objects.KnownName.OpenAction)).IsNotNull();

        PdfDocumentCatalogEditing.SetOpenAction(document, null);
        await Assert.That(PdfDocumentActions.GetOpenAction(document)).IsNull();
        PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { HideMenubar = true });
        PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { Duplex = "Simplex" });
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(document).HideMenubar).IsTrue();
        PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { Duplex = string.Empty });
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(document).Duplex).IsNull();
    }

    /// <summary>Document scripts are written to the catalog's additional actions and removed again.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentScriptsAreWrittenAndRemoved(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        await PdfDocumentCatalogEditing.SetDocumentActionAsync(document, PdfDocumentActionEvent.WillSave, Script, cancellationToken);
        await PdfDocumentCatalogEditing.SetDocumentActionAsync(document, PdfDocumentActionEvent.DidPrint, Script, cancellationToken);

        var file = EditChecks.SaveCompact(document);
        using (var reopened = PdfDocumentReader.Open(file, null))
        {
            var triggers = PdfDocumentActions.GetTriggers(reopened);
            await Assert.That(triggers.Select(static trigger => trigger.Event)).IsEquivalentTo(["WS", "DP"]);
        }

        await EditChecks.AssertQpdfCleanAsync("catalog-scripts", file, cancellationToken);
        PdfDocumentCatalogEditing.SetDocumentAction(document, PdfDocumentActionEvent.WillSave, null);
        PdfDocumentCatalogEditing.SetDocumentAction(document, PdfDocumentActionEvent.DidPrint, null);
        await Assert.That(PdfDocumentActions.GetTriggers(document)).IsEmpty();
        await Assert.That(document.Catalog.ContainsKey(document.Objects.Names.Intern("AA"u8))).IsFalse();
    }

    /// <summary>Wrong values are refused with the allowed ones named, and nothing is changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrongValuesAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        await Assert.That(() => PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { Direction = "up" })).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { NumCopies = Copies * Copies })).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { PrintPageRange = [TargetPage, 1] })).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentCatalogEditing.SetPageLayout(document, (PdfPageLayout)UnknownLayout)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentCatalogEditing.SetOpenAction(document, PdfBookmarkTarget.ToNamedAction("Quit"))).Throws<ArgumentException>();
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(0);
    }

    /// <summary>A PDF/UA claim keeps the window showing the document title.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfUaClaimKeepsTheTitleShown()
    {
        var packet = Accessibility.AccessibilityPdfs.Packet(1, true);
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Metadata 4 0 R /ViewerPreferences << /DisplayDocTitle true >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Metadata /Subtype /XML /Length {packet.Length} >>\nstream\n{packet}\nendstream"));
        using var document = PdfDocumentReader.Open(bytes, null);

        var thrown = await Assert.That(() => PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { DisplayDocTitle = false })).Throws<InvalidOperationException>();

        await Assert.That(thrown!.Message).Contains("PDF/UA");
        PdfDocumentCatalogEditing.SetViewerPreferences(document, new() { HideToolbar = true });
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(document).DisplayDocTitle).IsTrue();
    }

    /// <summary>A certification refuses every opening edit, and a PDF/A claim refuses scripts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClaimsLimitTheEdits()
    {
        using var certified = PdfDocumentReader.Open(EditGuardSamples.Certified(1), null);
        using var a3 = PdfDocumentReader.Open(PdfAFixtures.Create(new(PdfAPart3, "B")), null);

        await Assert.That(() => PdfDocumentCatalogEditing.SetPageMode(certified, PdfPageMode.UseOutlines)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentCatalogEditing.SetDocumentAction(a3, PdfDocumentActionEvent.WillClose, Script)).Throws<InvalidOperationException>();
        PdfDocumentCatalogEditing.SetPageMode(a3, PdfPageMode.UseOutlines);
        PdfDocumentCatalogEditing.SetViewerPreferences(a3, new() { DisplayDocTitle = true });
        await Assert.That(PdfDocumentViewerPreferences.GetViewerPreferences(a3).PageMode).IsEqualTo("UseOutlines");
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Checks accessibility snapshot reuse, invalidation and cancellation.</summary>
public sealed class PdfDocumentAccessibilityTests
{
    /// <summary>The language edit's history label.</summary>
    private const string LanguageEditLabel = "Set document language";

    /// <summary>Repeated synchronous and asynchronous reads share the current snapshot.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GetAccessibilityReport_UnchangedDocument_ReusesSnapshot(CancellationToken cancellationToken)
    {
        using var document = await Open(cancellationToken);
        var first = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var second = PdfAccessibilityReader.Read(document, cancellationToken);
        var asynchronous = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(asynchronous).IsSameReferenceAs(first);
    }

    /// <summary>Edits, undo and redo invalidate snapshots while preserving older results.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GetAccessibilityReport_CatalogLanguageEdited_ReadsNewSnapshot(CancellationToken cancellationToken)
    {
        using var document = await Open(cancellationToken);
        var first = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        PdfDocumentEditing.RunObjectEdit(
            document,
            LanguageEditLabel,
            PdfChangeKinds.Metadata,
            "set the document language",
            PdfValue.FromString("en"u8.ToArray()),
            static (edits, language) => edits.Catalog().Set(KnownName.Lang, language));
        var changed = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(changed).IsNotSameReferenceAs(first);
        await Assert.That(changed.Language).IsEqualTo("en");
        await Assert.That(changed.GetCount(PdfAccessibilityCode.NoLanguage)).IsEqualTo(0);
        await Assert.That(first.Language).IsNull();
        await Assert.That(first.GetCount(PdfAccessibilityCode.NoLanguage)).IsEqualTo(1);

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        var undone = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(undone).IsNotSameReferenceAs(changed);
        await Assert.That(undone.GetCount(PdfAccessibilityCode.NoLanguage)).IsEqualTo(1);
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        var redone = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(redone).IsNotSameReferenceAs(undone);
        await Assert.That(redone.Language).IsEqualTo("en");
    }

    /// <summary>Reports read during an open edit reflect its current catalog and are not published as stable snapshots.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GetAccessibilityReport_OpenTransaction_DoesNotReuseSnapshot(CancellationToken cancellationToken)
    {
        using var document = await Open(cancellationToken);
        var first = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        using (var transaction = PdfDocumentEditing.BeginEdit(document, LanguageEditLabel))
        {
            PdfDocumentEditing.RunObjectEdit(
                document,
                LanguageEditLabel,
                PdfChangeKinds.Metadata,
                "set the document language",
                PdfValue.FromString("en"u8.ToArray()),
                static (edits, language) => edits.Catalog().Set(KnownName.Lang, language));
            var changed = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
            var repeated = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
            await Assert.That(changed).IsNotSameReferenceAs(first);
            await Assert.That(changed.Language).IsEqualTo("en");
            await Assert.That(repeated).IsNotSameReferenceAs(changed);
        }

        var restored = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(restored.Language).IsNull();
        await Assert.That(first.Language).IsNull();
    }

    /// <summary>A cached snapshot cannot bypass cancellation.</summary>
    /// <param name="cancellationToken">Cancels the initial read.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GetAccessibilityReport_CancelledWarmRead_Throws(CancellationToken cancellationToken)
    {
        using var document = await Open(cancellationToken);
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(() => PdfDocumentAccessibility.GetAccessibilityReport(document, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>A cached snapshot cannot bypass document disposal.</summary>
    /// <param name="cancellationToken">Cancels the initial read.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task GetAccessibilityReport_DisposedWarmRead_Throws(CancellationToken cancellationToken)
    {
        var document = await Open(cancellationToken);
        using (document)
        {
            _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        }

        await Assert.That(() => PdfDocumentAccessibility.GetAccessibilityReport(document, cancellationToken)).Throws<ObjectDisposedException>();
        await Assert.That(async () => await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken)).Throws<ObjectDisposedException>();
    }

    /// <summary>Opens a blank page without a language declaration.</summary>
    /// <param name="cancellationToken">Cancels opening the document.</param>
    /// <returns>The test document.</returns>
    private static ValueTask<PdfDocument> Open(CancellationToken cancellationToken) => PdfDocumentReader.OpenAsync(
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>"),
        null,
        cancellationToken);
}

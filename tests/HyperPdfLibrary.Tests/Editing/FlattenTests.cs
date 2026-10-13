// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Tests.Signatures;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for drawing annotations and form fields into page content and removing them.</summary>
public sealed class FlattenTests
{
    /// <summary>The widgets in the field kinds page: a text field, a check box, two radio buttons, a combo box, a list box and a push button.</summary>
    private const int FieldKindWidgets = 7;

    /// <summary>The widgets flattened from one page of the two page form, and the widgets of the unsigned form.</summary>
    private const int TwoWidgets = 2;

    /// <summary>The fields left in the two page form after its first page is flattened.</summary>
    private const int TwoFields = 2;

    /// <summary>The pages in the two page form.</summary>
    private const int TwoPages = 2;

    /// <summary>A flattened annotation is drawn into the content, leaves the page, and the page looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationIsDrawnIntoTheContentAndLeavesThePage()
    {
        var file = FlattenTestDocuments.WithAnnotation("/F 4");
        var before = FlattenTestDocuments.Render(file, PdfRenderFlags.None);
        using var document = PdfDocumentReader.Open(file, null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var after = FlattenTestDocuments.Render(FlattenTestDocuments.SaveCompact(document), PdfRenderFlags.None);

        await Assert.That(report.AnnotationsDrawn).IsEqualTo(1);
        await Assert.That(report.PagesChanged).IsEqualTo(1);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(FlattenTestDocuments.Content(document, 0)).Contains("HpFlat0 Do");
        await Assert.That(FlattenTestDocuments.CountColour(before, FlattenTestDocuments.Blue)).IsEqualTo(FlattenTestDocuments.BluePixels);
        await Assert.That(FlattenTestDocuments.CountColour(after, FlattenTestDocuments.Blue)).IsEqualTo(FlattenTestDocuments.BluePixels);
        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));
    }

    /// <summary>Each field kind is drawn from its appearance, the widgets and the form go, and the page looks the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryFieldKindIsFlattenedAndTheFormIsRemoved()
    {
        var file = FlattenTestDocuments.WithFieldKinds(FlattenTestDocuments.Print);
        var before = FlattenTestDocuments.Render(file, PdfRenderFlags.None);
        using var document = PdfDocumentReader.Open(file, null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        using var saved = PdfDocumentReader.Open(FlattenTestDocuments.SaveCompact(document), null);
        var after = FlattenTestDocuments.Render(FlattenTestDocuments.SaveCompact(document), PdfRenderFlags.None);

        await Assert.That(report.WidgetsDrawn).IsEqualTo(FieldKindWidgets);
        await Assert.That(report.FormRemoved).IsTrue();
        await Assert.That(report.FieldsRemoved).IsGreaterThanOrEqualTo(FieldKindWidgets - 1);
        await Assert.That(FlattenTestDocuments.AnnotationCount(saved, 0)).IsEqualTo(0);
        await Assert.That(saved.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
        await Assert.That(FlattenTestDocuments.CountReachable(saved, static d => d.IsName(KnownName.Subtype, KnownName.Widget) || d.ContainsKey(KnownName.FT))).IsEqualTo(0);
        await Assert.That(FlattenTestDocuments.CountDifferences(before, after)).IsLessThanOrEqualTo(FlattenTestDocuments.AllowedDifferences(before));
    }

    /// <summary>The flatten is one edit: undo brings back the annotation and the form, and redo flattens again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlattenCanBeUndone()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithFieldKinds(FlattenTestDocuments.Print), null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);

        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(FieldKindWidgets);
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsTrue();
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
    }

    /// <summary>Flattening one page keeps the form with the fields that still have widgets, and flattening the rest removes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialFlattenKeepsTheRemainingFields()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.TwoPageForm(), null);
        var first = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var fields = document.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!;

        await Assert.That(first.FormRemoved).IsFalse();
        await Assert.That(first.WidgetsDrawn).IsEqualTo(TwoWidgets);
        await Assert.That(first.FieldsRemoved).IsEqualTo(1);
        await Assert.That(fields.Count).IsEqualTo(TwoFields);
        await Assert.That(fields.GetDictionary(1)!.GetArray(KnownName.Kids)!.Count).IsEqualTo(1);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0)).IsEqualTo(0);
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 1)).IsEqualTo(TwoWidgets);

        var second = PdfDocumentFlattening.Flatten(document, [1], PdfFlattenMode.Display);

        await Assert.That(second.FormRemoved).IsTrue();
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
        await Assert.That(FlattenTestDocuments.CountReachable(document, static d => d.ContainsKey(KnownName.FT) || d.IsName(KnownName.Subtype, KnownName.Widget))).IsEqualTo(0);
    }

    /// <summary>XFA data goes when any field is flattened, as it would no longer match the fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfaGoesWithTheFlattenedFields()
    {
        var pdf = new RenderTestPdf(FlattenTestDocuments.Edge, FlattenTestDocuments.Edge);
        var xfa = pdf.AddStream(string.Empty, "<xdp:xdp xmlns:xdp=\"http://ns.adobe.com/xfa/data\"/>");
        var field = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (a) /V (b) /Rect [10 10 50 30] /F {FlattenTestDocuments.Print} >>");
        var other = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (c) /Rect [60 10 90 30] /F {FlattenTestDocuments.Print} >>");
        pdf.CatalogEntries = $"/AcroForm << /Fields [{field} 0 R {other} 0 R] /XFA [(template) {xfa} 0 R] >>";
        pdf.PageEntries = $"/Annots [{field} 0 R]";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var form = document.Catalog.GetDictionary(KnownName.AcroForm)!;

        await Assert.That(report.XfaRemoved).IsTrue();
        await Assert.That(report.FormRemoved).IsFalse();
        await Assert.That(form.ContainsKey(KnownName.XFA)).IsFalse();
        await Assert.That(form.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
    }

    /// <summary>A signed document is refused and not changed; an unsigned signature field is flattened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedDocumentIsRefused()
    {
        using var signed = PdfDocumentReader.Open(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty), null);
        using var unsigned = PdfDocumentReader.Open(SignatureSamples.Unsigned(), null);
        var report = PdfDocumentFlattening.Flatten(unsigned, [0], PdfFlattenMode.Display);

        await Assert.That(PdfDocumentFlattening.GetRefusal(signed)).IsEqualTo(PdfFlattenRefusal.Signed);
        await Assert.That(() => PdfDocumentFlattening.Flatten(signed, [0], PdfFlattenMode.Display)).Throws<InvalidOperationException>();
        await Assert.That(StoreEditing.HasEdits(signed.Objects)).IsFalse();
        await Assert.That(PdfDocumentFlattening.GetRefusal(unsigned)).IsEqualTo(PdfFlattenRefusal.None);
        await Assert.That(report.FormRemoved).IsTrue();
        await Assert.That(report.WidgetsDrawn + report.WidgetsRemoved).IsEqualTo(TwoWidgets);
    }

    /// <summary>A cancelled flatten changes nothing; a bad page or option is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledOrInvalidFlattenChangesNothing()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithFieldKinds(FlattenTestDocuments.Print), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(() => PdfDocumentFlattening.Flatten(document, [0], PdfFlattenOptions.Default, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(() => PdfDocumentFlattening.Flatten(document, [1], PdfFlattenMode.Display)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentFlattening.Flatten(document, [0], new PdfFlattenOptions { Mode = (PdfFlattenMode)(-1) })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
    }

    /// <summary>The asynchronous form flattens every page and reports like the synchronous one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsynchronousFlattenFlattensEveryPage()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.TwoPageForm(), null);
        var report = await PdfDocumentFlattening.FlattenAllAsync(document, PdfFlattenOptions.Default, CancellationToken.None);

        await Assert.That(report.PagesChanged).IsEqualTo(TwoPages);
        await Assert.That(report.FormRemoved).IsTrue();
        await Assert.That(FlattenTestDocuments.AnnotationCount(document, 0) + FlattenTestDocuments.AnnotationCount(document, 1)).IsEqualTo(0);
    }

    /// <summary>The flattened file passes qpdf's check from both writers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlattenedFileIsCleanForQpdf()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithFieldKinds(FlattenTestDocuments.Print), null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var compact = await QpdfCheck.RunAsync("flatten-compact", FlattenTestDocuments.SaveCompact(document), CancellationToken.None);
        var incremental = await QpdfCheck.RunAsync("flatten-incremental", PdfIncrementalWriter.Save(document.Objects), CancellationToken.None);

        foreach (var outcome in (QpdfOutcome?[])[compact, incremental])
        {
            if (outcome is { } result)
            {
                await Assert.That(result.IsClean).IsTrue().Because(result.Output);
            }
        }
    }
}

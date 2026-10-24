// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that inserting and extracting pages keep forms, outlines, destinations, labels, layers and embedded files.</summary>
public sealed class CarryStructuresTests
{
    /// <summary>The first page's index.</summary>
    private const int FirstPage = 0;

    /// <summary>The second page's index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The fourth page's index.</summary>
    private const int FourthPage = 3;

    /// <summary>The pages in a merge of two books.</summary>
    private const int MergedPages = 4;

    /// <summary>The text typed into a copied field.</summary>
    private const string Typed = "Typed";

    /// <summary>The layers in a book that holds its own and a copied layer.</summary>
    private const int TwoLayers = 2;

    /// <summary>The embedded files in a book that holds its own and a copied file.</summary>
    private const int TwoFiles = 2;

    /// <summary>The full name of the field on the book's second page.</summary>
    private const string CityName = "Address.City";

    /// <summary>Gets the names of the book's fields on page 1.</summary>
    private static string[] FirstPageFields => ["Name", "Address.Street"];

    /// <summary>Gets the names of the book's fields on page 1, after a copy renamed them.</summary>
    private static string[] RenamedFirstPageFields => ["Name_1", "Address_1.Street"];

    /// <summary>Merging two books gives every field a unique name, and every copied field fills and saves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MergedFormsHaveUniqueFieldsThatFillAndSave()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        PdfDocumentPageOperations.InsertPages(target, target.PageCount, source, [FirstPage, SecondPage]);

        await Assert.That(target.PageCount).IsEqualTo(MergedPages);
        await Assert.That(CarryTestDocuments.WidgetNames(target, FirstPage)).IsEquivalentTo(FirstPageFields);
        await Assert.That(CarryTestDocuments.WidgetNames(target, ThirdPage)).IsEquivalentTo(RenamedFirstPageFields);
        await Assert.That(CarryTestDocuments.WidgetNames(target, FourthPage)).IsEquivalentTo(["Address_1.City"]);
        await Assert.That(target.Catalog.GetDictionary(KnownName.AcroForm)!.GetArray(KnownName.Fields)!.Count).IsEqualTo(MergedPages);

        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(target), ThirdPage, 0, Typed)).IsTrue();
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(target), FourthPage, 0, Typed)).IsTrue();

        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(target.Objects), null);
        var widgets = new List<HyperPdfLibrary.Forms.PdfFormWidget>();
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(saved), ThirdPage, widgets);
        await Assert.That(widgets[0].Value).IsEqualTo(Typed);
        await Assert.That(WritingTestDocuments.CountMissing(saved.Objects)).IsEqualTo(0);
    }

    /// <summary>A field that spans pages keeps only the widgets on copied pages, and a form with no fields in the target gets an AcroForm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialCopyKeepsOnlyCopiedWidgets()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentPageOperations.InsertPages(target, 1, source, [SecondPage]);

        await Assert.That(CarryTestDocuments.WidgetNames(target, 1)).IsEquivalentTo([CityName]);
        var form = target.Catalog.GetDictionary(KnownName.AcroForm)!;
        await Assert.That(form.GetArray(KnownName.Fields)!.Count).IsEqualTo(1);
        await Assert.That(form.GetArray(KnownName.Fields)!.GetDictionary(0)!.GetArray(KnownName.Kids)!.Count).IsEqualTo(1);
        await Assert.That(form.GetStringBytes(KnownName.DA).Length).IsGreaterThan(0);
        await Assert.That(form.GetDictionary(KnownName.DR)!.GetDictionary(KnownName.Font)!.Count).IsEqualTo(1);
    }

    /// <summary>Outlines and named destinations lead to the copies, named destinations that clash are renamed, and links to pages left out are removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlinesAndDestinationsFollowTheCopies()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        PdfDocumentPageOperations.InsertPages(target, target.PageCount, source, [FirstPage, SecondPage]);

        await Assert.That(CarryTestDocuments.OutlinePages(target)).IsEquivalentTo([FirstPage, SecondPage, ThirdPage, FourthPage]);
        await Assert.That(CarryTestDocuments.LinkPages(target, FirstPage)).IsEquivalentTo([SecondPage, SecondPage]);
        await Assert.That(CarryTestDocuments.LinkPages(target, ThirdPage)).IsEquivalentTo([FourthPage, FourthPage]);
        await Assert.That(CarryTestDocuments.LinkPages(target, FourthPage)).IsEquivalentTo([ThirdPage]);

        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(target.Objects), null);
        await Assert.That(CarryTestDocuments.OutlinePages(saved)).IsEquivalentTo([FirstPage, SecondPage, ThirdPage, FourthPage]);
        await Assert.That(CarryTestDocuments.LinkPages(saved, ThirdPage)).IsEquivalentTo([FourthPage, FourthPage]);
    }

    /// <summary>Copying one page keeps the outline entries and links that lead to it and drops the rest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyingOnePageDropsLinksToOtherPages()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentPageOperations.InsertPages(target, 1, source, [SecondPage]);

        await Assert.That(CarryTestDocuments.OutlinePages(target)).IsEquivalentTo([1]);
        await Assert.That(CarryTestDocuments.LinkPages(target, 1)).IsEmpty();
        var links = PdfDocumentPages.GetPage(target, 1).Dictionary.GetArray(KnownName.Annots)!;
        await Assert.That(links.GetDictionary(1)!.ContainsKey(KnownName.Dest)).IsFalse();
    }

    /// <summary>Copied pages keep their layer, with the state the source gave it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayersKeepTheirState()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        await Assert.That(PdfDocumentLayers.GetOptionalContent(target).Layers.Count).IsEqualTo(1);
        PdfDocumentPageOperations.InsertPages(target, target.PageCount, source, [FirstPage]);

        var layers = PdfDocumentLayers.GetOptionalContent(target).Layers;
        await Assert.That(layers.Count).IsEqualTo(TwoLayers);
        await Assert.That(layers[1].Name).IsEqualTo("Notes");
        await Assert.That(layers[1].IsVisible).IsFalse();

        using var flat = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentPageOperations.InsertPages(flat, 0, source, [FirstPage]);
        await Assert.That(PdfDocumentLayers.GetOptionalContent(flat).Layers.Count).IsEqualTo(1);
        await Assert.That(PdfDocumentLayers.GetOptionalContent(flat).Layers[0].IsVisible).IsFalse();
    }

    /// <summary>Copied pages bring their page labels and the names of their embedded files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabelsAndEmbeddedFilesComeAlong()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(MergedPages - 1), null);
        PdfDocumentPageOperations.InsertPages(target, 0, source, [FirstPage, SecondPage]);

        await Assert.That(PdfDocumentLabels.GetPageLabel(target, FirstPage)).IsEqualTo("i");
        await Assert.That(PdfDocumentLabels.GetPageLabel(target, SecondPage)).IsEqualTo("ii");
        await Assert.That(PdfDocumentLabels.GetPageLabel(target, ThirdPage)).IsEqualTo("1");
        await Assert.That(PdfDocumentAttachments.GetAttachments(target).Count).IsEqualTo(1);
        await Assert.That(PdfDocumentAttachments.GetAttachments(target)[0].Name).IsEqualTo(CarryTestDocuments.AttachmentName);

        using var both = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        PdfDocumentPageOperations.InsertPages(both, both.PageCount, source, [FirstPage]);
        await Assert.That(PdfDocumentAttachments.GetAttachments(both).Count).IsEqualTo(TwoFiles);
    }

    /// <summary>Pages inserted from the document itself get their own fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertingFromTheSameDocumentRenamesFields()
    {
        using var document = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        PdfDocumentPageOperations.InsertPages(document, document.PageCount, document, [FirstPage, SecondPage]);

        await Assert.That(CarryTestDocuments.WidgetNames(document, FirstPage)).IsEquivalentTo(FirstPageFields);
        await Assert.That(CarryTestDocuments.WidgetNames(document, ThirdPage)).IsEquivalentTo(RenamedFirstPageFields);
        await Assert.That(CarryTestDocuments.LinkPages(document, ThirdPage)).IsEquivalentTo([FourthPage, FourthPage]);
    }

    /// <summary>Extracting pages writes a document that keeps the same structures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractedPagesKeepTheStructures()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var extracted = PdfDocumentReader.Open(PdfDocumentPageOperations.ExtractPages(source, [FirstPage, SecondPage]), null);

        await Assert.That(CarryTestDocuments.WidgetNames(extracted, FirstPage)).IsEquivalentTo(FirstPageFields);
        await Assert.That(CarryTestDocuments.WidgetNames(extracted, SecondPage)).IsEquivalentTo([CityName]);
        await Assert.That(CarryTestDocuments.OutlinePages(extracted)).IsEquivalentTo([FirstPage, SecondPage]);
        await Assert.That(CarryTestDocuments.LinkPages(extracted, FirstPage)).IsEquivalentTo([SecondPage, SecondPage]);
        await Assert.That(CarryTestDocuments.LinkPages(extracted, SecondPage)).IsEquivalentTo([FirstPage]);
        await Assert.That(PdfDocumentLabels.GetPageLabel(extracted, SecondPage)).IsEqualTo("ii");
        await Assert.That(PdfDocumentLayers.GetOptionalContent(extracted).Layers.Count).IsEqualTo(1);
        await Assert.That(PdfDocumentLayers.GetOptionalContent(extracted).Layers[0].IsVisible).IsFalse();
        await Assert.That(PdfDocumentAttachments.GetAttachments(extracted).Count).IsEqualTo(1);
        await Assert.That(WritingTestDocuments.CountMissing(extracted.Objects)).IsEqualTo(0);
        await Assert.That(HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(extracted), FirstPage, 0, Typed)).IsTrue();
    }

    /// <summary>Extracting one page leaves out the outline entries and links to the others.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractingOnePageLeavesOutOtherLinks()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var extracted = PdfDocumentReader.Open(PdfDocumentPageOperations.ExtractPages(source, [SecondPage]), null);

        await Assert.That(CarryTestDocuments.OutlinePages(extracted)).IsEquivalentTo([FirstPage]);
        await Assert.That(CarryTestDocuments.LinkPages(extracted, FirstPage)).IsEmpty();
        await Assert.That(CarryTestDocuments.WidgetNames(extracted, FirstPage)).IsEquivalentTo([CityName]);
    }
}

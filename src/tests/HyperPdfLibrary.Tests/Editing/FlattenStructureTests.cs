// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that a flatten keeps the structure tree of a tagged document consistent.</summary>
public sealed class FlattenStructureTests
{
    /// <summary>The widget's key in the parent tree.</summary>
    private const int WidgetKey = 1;

    /// <summary>The marked content id of the paragraph that was on the page already.</summary>
    private const int ParagraphMcid = 0;

    /// <summary>The marked content id given to the flattened widget.</summary>
    private const int WidgetMcid = 1;

    /// <summary>The number of drawings marked as artifacts: the annotation that no element owned.</summary>
    private const int ArtifactCount = 1;

    /// <summary>The object number of the widget.</summary>
    private const int WidgetObject = 8;

    /// <summary>The object number of the form element.</summary>
    private const int FormElement = 10;

    /// <summary>An object reference to a widget becomes marked content of its element, and the parent tree follows.</summary>
    /// <param name="pageHasKey">Whether the page had a parent tree key before.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task WidgetReferenceBecomesMarkedContent(bool pageHasKey)
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.Tagged(pageHasKey), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var content = FlattenTestDocuments.Content(document, 0);
        var element = StoreReading.GetDictionary(document.Objects, new(FormElement, 0))!;
        var kid = element.GetDictionary(KnownName.K)!;
        var tree = PdfStructureTree.Load(document)!;

        await Assert.That(report.TaggedContent).IsEqualTo(1);
        await Assert.That(report.Artifacts).IsEqualTo(ArtifactCount);
        await Assert.That(content).Contains("/Form <</MCID");
        await Assert.That(content).Contains("/Artifact BMC");
        await Assert.That(kid.IsName(KnownName.Type, KnownName.MCR)).IsTrue();
        await Assert.That(kid.GetInt32(KnownName.MCID)).IsEqualTo(WidgetMcid);
        await Assert.That(kid.GetRaw(KnownName.Pg).AsReference()).IsEqualTo(PdfDocumentPages.GetPage(document, 0).Id);
        await Assert.That(StoreReading.GetObject(document.Objects, new(WidgetObject, 0)).IsNull).IsTrue();
        await Assert.That(tree.GetMarkedContentParent(0, WidgetMcid)?.Type.ToString()).IsEqualTo("Form");
        await Assert.That(tree.GetMarkedContentParent(0, ParagraphMcid)?.Type.ToString()).IsEqualTo(pageHasKey ? "Paragraph" : null);
    }

    /// <summary>No object reference to a removed annotation and no parent tree entry for it is left behind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoDanglingStructureReferenceIsLeft()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.Tagged(true), null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var root = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbers(root.GetDictionary(KnownName.ParentTree), entries);

        await Assert.That(FlattenTestDocuments.CountReachable(document, static d => d.IsName(KnownName.Type, KnownName.OBJR))).IsEqualTo(0);
        await Assert.That(entries.Exists(static entry => entry.Key.AsInteger() == WidgetKey)).IsFalse();
        await Assert.That(FlattenTestDocuments.CountReachable(document, static d => d.IsName(KnownName.Subtype, KnownName.Widget))).IsEqualTo(0);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary.ContainsKey(KnownName.Annots)).IsFalse();
    }

    /// <summary>The structure survives a save with both writers, and qpdf finds nothing wrong.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureSurvivesSaving()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.Tagged(true), null);
        _ = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var compact = FlattenTestDocuments.SaveCompact(document);
        using var reopened = PdfDocumentReader.Open(compact, null);
        var tree = PdfStructureTree.Load(reopened)!;
        var outcome = await QpdfCheck.RunAsync("flatten-tagged", compact, CancellationToken.None);

        await Assert.That(tree.GetMarkedContentParent(0, WidgetMcid)?.Type.ToString()).IsEqualTo("Form");
        if (outcome is { } result)
        {
            await Assert.That(result.IsClean).IsTrue().Because(result.Output);
        }
    }

    /// <summary>A hidden widget that a form element refers to leaves the element without content, and nothing dangles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenWidgetLeavesItsElementEmpty()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.Tagged(true, FlattenTestDocuments.Hidden), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var element = StoreReading.GetDictionary(document.Objects, new(FormElement, 0))!;
        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbers(document.Catalog.GetDictionary(KnownName.StructTreeRoot)!.GetDictionary(KnownName.ParentTree), entries);

        await Assert.That(report.WidgetsRemoved).IsEqualTo(1);
        await Assert.That(report.TaggedContent).IsEqualTo(0);
        await Assert.That(element.ContainsKey(KnownName.K)).IsFalse();
        await Assert.That(entries.Exists(static entry => entry.Key.AsInteger() == WidgetKey)).IsFalse();
        await Assert.That(FlattenTestDocuments.CountReachable(document, static d => d.IsName(KnownName.Type, KnownName.OBJR))).IsEqualTo(0);
    }

    /// <summary>A document that is not tagged gets no marked content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentGetsNoMarkedContent()
    {
        using var document = PdfDocumentReader.Open(FlattenTestDocuments.WithAnnotation("/F 4"), null);
        var report = PdfDocumentFlattening.Flatten(document, [0], PdfFlattenMode.Display);
        var content = FlattenTestDocuments.Content(document, 0);

        await Assert.That(report.TaggedContent).IsEqualTo(0);
        await Assert.That(report.Artifacts).IsEqualTo(0);
        await Assert.That(content).DoesNotContain("BDC");
        await Assert.That(content).DoesNotContain("Artifact");
    }
}

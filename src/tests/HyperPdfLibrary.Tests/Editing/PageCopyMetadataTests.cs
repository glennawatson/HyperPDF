// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>
/// Tests that a page copy carries the metadata around the copied pages: the form's signature flags, calculation order and
/// XFA data, the layers' radio-button groups and automatic states, the actions chained after a copied action, and the
/// structure elements that outline entries name.
/// </summary>
public sealed class PageCopyMetadataTests
{
    /// <summary>The first page's index.</summary>
    private const int FirstPage = 0;

    /// <summary>The signature flags of a source that has signatures and is append only.</summary>
    private const int SourceSignatureFlags = 3;

    /// <summary>The signature flags of a target that has signatures.</summary>
    private const int TargetSignatureFlags = 1;

    /// <summary>The fields in the form of a target that had one field and received three.</summary>
    private const int FieldsAfterMerge = 4;

    /// <summary>The second page's index in the source.</summary>
    private const int SecondPage = 1;

    /// <summary>The place of the third link among the annotations of the copied page.</summary>
    private const int ThirdAnnotation = 2;

    /// <summary>The index of the copied page in the target.</summary>
    private const int CopiedPageIndex = 1;

    /// <summary>Copying signature fields keeps the signature flags, and the calculation order lists the carried fields in the source's order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureFlagsAndCalculationOrderFollowTheCarriedFields()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateCalculatingForm(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage]);

        var form = target.Catalog.GetDictionary(KnownName.AcroForm)!;
        await Assert.That(form.GetInt32(KnownName.SigFlags)).IsEqualTo(SourceSignatureFlags);
        await Assert.That(PageCopyMetadataDocuments.ReadCalculationOrder(target)).IsEquivalentTo([PageCopyMetadataDocuments.SecondField, PageCopyMetadataDocuments.FirstField]);
        await Assert.That(form.ContainsKey(KnownName.XFA)).IsFalse();

        using var saved = PdfDocumentReader.Open(EditChecks.SaveCompact(target), null);
        await Assert.That(PageCopyMetadataDocuments.ReadCalculationOrder(saved)).IsEquivalentTo([PageCopyMetadataDocuments.SecondField, PageCopyMetadataDocuments.FirstField]);
        await Assert.That(saved.Catalog.GetDictionary(KnownName.AcroForm)!.GetInt32(KnownName.SigFlags)).IsEqualTo(SourceSignatureFlags);
    }

    /// <summary>A page without signature fields adds no signature flags, and a page 2 field joins the order only when its page is copied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopyingOtherPagesKeepsOnlyTheirOrder()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateCalculatingForm(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        PdfDocumentPageOperations.InsertPages(target, 1, source, [SecondPage]);

        var form = target.Catalog.GetDictionary(KnownName.AcroForm)!;
        await Assert.That(form.ContainsKey(KnownName.SigFlags)).IsFalse();
        await Assert.That(PageCopyMetadataDocuments.ReadCalculationOrder(target)).IsEquivalentTo(["Third"]);
    }

    /// <summary>The flags and the order of the target are combined with the carried ones, and its stale XFA data is removed and reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TargetFormIsMergedAndStaleXfaIsRemoved()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateCalculatingForm(), null);
        using var target = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateXfaTarget(), null);
        var report = new PdfPageEditReport();

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage], report, CancellationToken.None);

        var form = target.Catalog.GetDictionary(KnownName.AcroForm)!;
        await Assert.That(form.GetInt32(KnownName.SigFlags)).IsEqualTo(SourceSignatureFlags | TargetSignatureFlags);
        await Assert.That(PageCopyMetadataDocuments.ReadCalculationOrder(target)).IsEquivalentTo(["Own", PageCopyMetadataDocuments.SecondField, PageCopyMetadataDocuments.FirstField]);
        await Assert.That(form.ContainsKey(KnownName.XFA)).IsFalse();
        await Assert.That(report.FormChanges.Count).IsEqualTo(1);
        await Assert.That(form.GetArray(KnownName.Fields)!.Count).IsEqualTo(FieldsAfterMerge);
        await EditChecks.AssertQpdfCleanAsync("page-copy-merged-form", EditChecks.SaveCompact(target), CancellationToken.None);
    }

    /// <summary>A form that is only XFA data keeps it, because there are no standard fields to fall back on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task XfaOnlyFormKeepsItsData()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateCalculatingForm(), null);
        using var target = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreatePureXfaTarget(), null);
        var report = new PdfPageEditReport();

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage], report, CancellationToken.None);

        await Assert.That(target.Catalog.GetDictionary(KnownName.AcroForm)!.ContainsKey(KnownName.XFA)).IsTrue();
        await Assert.That(report.FormChanges).IsEmpty();
    }

    /// <summary>Radio-button groups and automatic states are cut to the copied layers; a group left with one layer and a state with none are dropped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerRadioGroupsAndAutomaticStatesAreCarried()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateRadioLayers(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage]);

        string[] expected = [PageCopyMetadataDocuments.FirstLayer, PageCopyMetadataDocuments.SecondLayer];
        var configuration = target.Catalog.GetDictionary(KnownName.OCProperties)!.GetDictionary(KnownName.D)!;
        var groups = configuration.GetArray(KnownName.RBGroups)!;
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(PageCopyMetadataDocuments.ReadLayerNames(groups.GetArray(0)!)).IsEquivalentTo(expected);

        var states = configuration.GetArray(KnownName.AS)!;
        await Assert.That(states.Count).IsEqualTo(1);
        await Assert.That(states.GetDictionary(0)!.IsName(KnownName.Event, KnownName.View)).IsTrue();
        await Assert.That(PageCopyMetadataDocuments.ReadLayerNames(states.GetDictionary(0)!.GetArray(KnownName.OCGs)!)).IsEquivalentTo(expected);

        using var saved = PdfDocumentReader.Open(EditChecks.SaveIncremental(target), null);
        var savedGroups = saved.Catalog.GetDictionary(KnownName.OCProperties)!.GetDictionary(KnownName.D)!.GetArray(KnownName.RBGroups)!;
        await Assert.That(PageCopyMetadataDocuments.ReadLayerNames(savedGroups.GetArray(0)!)).IsEquivalentTo(expected);
    }

    /// <summary>A chained go-to action that leads to a page that is not copied is removed from the chain, and the rest of the chain stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DanglingGoToIsRemovedFromActionChains()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateChainedActions(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage]);

        var annotations = PdfDocumentPages.GetPage(target, 1).Dictionary.GetArray(KnownName.Annots)!;
        var single = annotations.GetDictionary(0)!.GetDictionary(KnownName.A)!;
        await Assert.That(single.IsName(KnownName.S, KnownName.URI)).IsTrue();
        await Assert.That(single.ContainsKey(KnownName.Next)).IsFalse();

        var list = annotations.GetDictionary(1)!.GetDictionary(KnownName.A)!;
        await Assert.That(list.IsName(KnownName.S, KnownName.GoTo)).IsTrue();
        var rest = list.GetArray(KnownName.Next)!;
        await Assert.That(rest.Count).IsEqualTo(1);
        await Assert.That(rest.GetDictionary(0)!.IsName(KnownName.S, KnownName.URI)).IsTrue();

        var live = annotations.GetDictionary(ThirdAnnotation)!.GetDictionary(KnownName.A)!.GetDictionary(KnownName.Next)!;
        await Assert.That(live.IsName(KnownName.S, KnownName.GoTo)).IsTrue();
        await Assert.That(live.GetDictionary(KnownName.Next)!.IsName(KnownName.S, KnownName.URI)).IsTrue();

        await EditChecks.AssertQpdfCleanAsync("page-copy-action-chain", EditChecks.SaveCompact(target), CancellationToken.None);
    }

    /// <summary>Outline entries keep their link to the copy of their structure element, and an entry that only names a copied element stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineEntriesKeepTheirStructureElements()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateTaggedOutline(), null);
        using var target = PdfDocumentReader.Open(TaggedSamples.Basic(), null);

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage]);

        var tree = PdfStructureTree.Load(target)!;
        var heading = tree.GetMarkedContentParent(CopiedPageIndex, 0)!;
        var figure = tree.GetMarkedContentParent(CopiedPageIndex, 1)!;
        var title = target.Catalog.GetDictionary(KnownName.Outlines)!.GetDictionary(KnownName.First)!;
        var second = title.GetDictionary(KnownName.Next)!;
        await Assert.That(title.GetRaw(KnownName.SE).AsReference()).IsEqualTo(heading.Id);
        await Assert.That(second.GetRaw(KnownName.SE).AsReference()).IsEqualTo(figure.Id);
        await Assert.That(second.ContainsKey(KnownName.Dest)).IsFalse();
        await EditChecks.AssertQpdfCleanAsync("page-copy-outline-elements", EditChecks.SaveCompact(target), CancellationToken.None);
    }

    /// <summary>Without a tagged target the structure elements are not copied, so the entries lose the link and the entry with no other target goes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedTargetDropsTheStructureElements()
    {
        using var source = PdfDocumentReader.Open(PageCopyMetadataDocuments.CreateTaggedOutline(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        PdfDocumentPageOperations.InsertPages(target, 1, source, [FirstPage]);

        var title = target.Catalog.GetDictionary(KnownName.Outlines)!.GetDictionary(KnownName.First)!;
        await Assert.That(title.ContainsKey(KnownName.SE)).IsFalse();
        await Assert.That(title.ContainsKey(KnownName.Dest)).IsTrue();
        await Assert.That(title.ContainsKey(KnownName.Next)).IsFalse();
    }
}

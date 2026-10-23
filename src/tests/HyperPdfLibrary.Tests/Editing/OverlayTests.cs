// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for drawing pages of one document on pages of another (overlay and underlay).</summary>
public sealed class OverlayTests
{
    /// <summary>The pages in the target document.</summary>
    private const int TargetPages = 3;

    /// <summary>The streams in an overlaid page: the opening q, the page's own content and the drawing.</summary>
    private const int OverlaidStreams = 3;

    /// <summary>The streams in an underlaid page: the drawing and the page's own content.</summary>
    private const int UnderlaidStreams = 2;

    /// <summary>The index of the last target page.</summary>
    private const int LastTargetPage = 2;

    /// <summary>The resource name of the first form drawn on a page.</summary>
    private const string FormName = "HpForm0";

    /// <summary>The group name used by the layer tests.</summary>
    private const string Layer = "Letterhead";

    /// <summary>The first part of ISO 19005.</summary>
    private const int PdfA1 = 1;

    /// <summary>An overlay in front of the content draws after the page's own content and keeps it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlayDrawsAfterTheOriginalContent()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TargetPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, PdfDocumentOverlays.MapEveryPage(TargetPages, 0), PdfOverlayOptions.Default);
        var streams = Streams(target, 0);

        await Assert.That(streams.Length).IsEqualTo(OverlaidStreams);
        await Assert.That(streams[0]).IsEqualTo("q\n");
        await Assert.That(streams[1]).Contains("(Page 1)");
        await Assert.That(streams[^1]).StartsWith("Q\n");
        await Assert.That(streams[^1]).Contains($"/{FormName} Do");
        await Assert.That(FormOf(target, 0).Dictionary.TryGetRectangle(KnownName.BBox, out var box)).IsTrue();
        await Assert.That(box).IsEqualTo(PdfDocumentPages.GetPage(source, 0).CropBox);
    }

    /// <summary>An underlay draws before the page's own content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnderlayDrawsBeforeTheOriginalContent()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TargetPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], new() { Placement = PdfOverlayPlacement.Underlay });
        var streams = Streams(target, 0);

        await Assert.That(streams.Length).IsEqualTo(UnderlaidStreams);
        await Assert.That(streams[0]).Contains($"/{FormName} Do");
        await Assert.That(streams[1]).Contains("(Page 1)");
    }

    /// <summary>A source page drawn on several pages is copied once, and pages that are not mapped are left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SourcePageIsCopiedOnceAndOtherPagesAreUntouched()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TargetPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0), new(LastTargetPage, 0)], PdfOverlayOptions.Default);

        await Assert.That(FormId(target, 0)).IsEqualTo(FormId(target, LastTargetPage));
        await Assert.That(PdfDocumentPages.GetPage(target, 1).Dictionary.Get(KnownName.Contents).AsStream()).IsNotNull();
    }

    /// <summary>Two drawings on one page stack, and the second does not lose the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedMappingsStackOnOnePage()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0), new(0, 0)], PdfOverlayOptions.Default);
        var forms = PdfDocumentPages.GetPage(target, 0).Resources!.GetDictionary(KnownName.XObject)!;

        await Assert.That(forms.Count).IsEqualTo(UnderlaidStreams);
    }

    /// <summary>Resources that pages share are not changed: the stamped page gets its own copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharedResourcesAreCopiedNotChanged()
    {
        using var target = PdfDocumentReader.Open(SharedResources(), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], PdfOverlayOptions.Default);

        await Assert.That(PdfDocumentPages.GetPage(target, 0).Resources!.GetDictionary(KnownName.XObject)!.ContainsKey(target.Objects.Names.Intern(FormName))).IsTrue();
        await Assert.That(PdfDocumentPages.GetPage(target, 1).Resources!.GetDictionary(KnownName.XObject)!.Count).IsEqualTo(0);
    }

    /// <summary>The overlay survives an incremental save and a compact save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlaySurvivesBothWriters()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TargetPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, PdfDocumentOverlays.MapEveryPage(TargetPages, 0), PdfOverlayOptions.Default);
        using var incremental = PdfDocumentReader.Open(PdfIncrementalWriter.Save(target.Objects), null);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(target.Objects, PdfCompactOptions.Default), null);
        foreach (var saved in (PdfDocument[])[incremental, compact])
        {
            await Assert.That(Streams(saved, TargetPages - 1).Length).IsEqualTo(OverlaidStreams);
            await Assert.That(FormOf(saved, 1).Dictionary.IsName(KnownName.Subtype, KnownName.Form)).IsTrue();
        }
    }

    /// <summary>A named layer is one optional content group shared by every overlay, and the form is controlled by it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerNameCreatesOneSharedGroup()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TargetPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentOverlays.OverlayPages(target, source, PdfDocumentOverlays.MapEveryPage(TargetPages, 0), new() { LayerName = Layer });
        PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], new() { LayerName = Layer });
        var properties = target.Objects.Catalog.GetDictionary(KnownName.OCProperties)!;
        var groups = properties.GetArray(KnownName.OCGs)!;
        var order = properties.GetDictionary(KnownName.D)!.GetArray(KnownName.Order)!;

        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(order.Count).IsEqualTo(1);
        await Assert.That(groups.GetDictionary(0)!.GetText(KnownName.Name)).IsEqualTo(Layer);
        await Assert.That(FormOf(target, 1).Dictionary.GetRaw(KnownName.OC).AsReference()).IsEqualTo(groups.GetRaw(0).AsReference());
        await Assert.That(PdfDocumentLayers.GetOptionalContent(target).Layers.Count).IsEqualTo(1);
    }

    /// <summary>A tagged document marks the drawing as an artifact, and an untagged one does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentMarksTheDrawingAsAnArtifact()
    {
        var pdf = ConformancePdf.Page();
        pdf.CatalogEntries = "/MarkInfo << /Marked true >> /StructTreeRoot << /Type /StructTreeRoot >>";
        using var tagged = PdfDocumentReader.Open(pdf.ToBytes(), null);
        using var plain = PdfDocumentReader.Open(ConformancePdf.Page().ToBytes(), null);
        using var source = PdfDocumentReader.Open(ConformancePdf.Page().ToBytes(), null);
        PdfDocumentOverlays.OverlayPages(tagged, source, [new(0, 0)], PdfOverlayOptions.Default);
        PdfDocumentOverlays.OverlayPages(plain, source, [new(0, 0)], PdfOverlayOptions.Default);

        await Assert.That(Streams(tagged, 0)[^1]).Contains("/Artifact BMC");
        await Assert.That(Streams(tagged, 0)[^1]).Contains("EMC");
        await Assert.That(Streams(plain, 0)[^1]).DoesNotContain("Artifact");
    }

    /// <summary>A document that claims PDF/A-1 refuses a layer, and is not changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA1ClaimRefusesALayer()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PdfA1, "B", null);
        using var target = PdfDocumentReader.Open(pdf.ToBytes(), null);
        using var source = PdfDocumentReader.Open(ConformancePdf.Page().ToBytes(), null);

        await Assert.That(() => PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], new() { LayerName = Layer })).Throws<InvalidOperationException>();
        await Assert.That(StoreEditing.HasEdits(target.Objects)).IsFalse();
    }

    /// <summary>A page that does not exist is refused and nothing changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownPagesAreRefused()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        await Assert.That(() => PdfDocumentOverlays.OverlayPages(target, source, [new(0, 1)], PdfOverlayOptions.Default)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentOverlays.OverlayPages(target, source, [new(1, 0)], PdfOverlayOptions.Default)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(StoreEditing.HasEdits(target.Objects)).IsFalse();
    }

    /// <summary>The asynchronous form edits like the synchronous one, and a cancelled call changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormEditsAndCancels()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        PdfOverlayMapping[] mappings = [new(0, 0)];

        await Assert.That(async () => await PdfDocumentOverlays.OverlayPagesAsync(target, source, mappings, PdfOverlayOptions.Default, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(StoreEditing.HasEdits(target.Objects)).IsFalse();
        await PdfDocumentOverlays.OverlayPagesAsync(target, source, mappings, PdfOverlayOptions.Default, CancellationToken.None);

        await Assert.That(Streams(target, 0).Length).IsEqualTo(OverlaidStreams);
    }

    /// <summary>Cycling maps repeat the source pages in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclingMappingsRepeatTheSourcePages()
    {
        var mappings = PdfDocumentOverlays.MapCycling(TargetPages, UnderlaidStreams);

        await Assert.That(mappings.Select(static mapping => mapping.SourcePage).ToArray()).IsEquivalentTo([0, 1, 0]);
    }

    /// <summary>Gets the decoded content streams of a page, in order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The stream texts.</returns>
    internal static string[] Streams(PdfDocument document, int index)
    {
        var contents = PdfDocumentPages.GetPage(document, index).Dictionary.Get(KnownName.Contents).AsArray()!;
        var texts = new string[contents.Count];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = Encoding.Latin1.GetString(contents.Get(i).AsStream()!.DecodeToArray());
        }

        return texts;
    }

    /// <summary>Gets the first form XObject of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The form.</returns>
    internal static PdfStream FormOf(PdfDocument document, int index) =>
        PdfDocumentPages.GetPage(document, index).Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern(FormName))!;

    /// <summary>Gets the object number of the first form XObject of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The object number.</returns>
    private static int FormId(PdfDocument document, int index) =>
        PdfDocumentPages.GetPage(document, index).Resources!.GetDictionary(KnownName.XObject)!.GetRaw(document.Objects.Names.Intern(FormName)).AsReference().Number;

    /// <summary>Builds two pages that share one indirect resources dictionary.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] SharedResources() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 7 0 R /Contents 5 0 R >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 7 0 R /Contents 6 0 R >>",
        MiniPdf.Stream(string.Empty, "0 0 1 rg 0 0 10 10 re f"),
        MiniPdf.Stream(string.Empty, "1 0 0 rg 0 0 10 10 re f"),
        "<< /XObject << >> >>");
}

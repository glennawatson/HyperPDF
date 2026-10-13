// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Conformance;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for text and image watermarks.</summary>
public sealed class WatermarkTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The first part of ISO 19005.</summary>
    private const int PdfA1 = 1;

    /// <summary>The second part of ISO 19005.</summary>
    private const int PdfA2 = 2;

    /// <summary>The edge of the test image in pixels.</summary>
    private const int ImageEdge = 2;

    /// <summary>The width the test image is drawn at, in points.</summary>
    private const float ImageWidth = 100;

    /// <summary>The opacity used by the transparency tests.</summary>
    private const float HalfOpacity = 0.5F;

    /// <summary>The turn used by the placement tests.</summary>
    private const float Turn = 45;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The text of the watermarks.</summary>
    private const string Text = "DRAFT";

    /// <summary>A text watermark goes only on the chosen pages, after their content, with the text in its form.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWatermarkIsDrawnOnTheChosenPages()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfDocumentWatermarks.AddText(document, [0, Pages - 1], Text, new(), PdfWatermarkOptions.Default);
        var streams = OverlayTests.Streams(document, 0);
        var form = OverlayTests.FormOf(document, 0);

        await Assert.That(streams.Length).IsEqualTo(Pages);
        await Assert.That(streams[^1]).Contains("HpForm0 Do");
        await Assert.That(Encoding.Latin1.GetString(form.DecodeToArray())).Contains($"({Text}) Tj");
        await Assert.That(form.Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!.Count).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Dictionary.Get(KnownName.Contents).AsStream()).IsNotNull();
        await Assert.That(OverlayTests.Streams(document, Pages - 1).Length).IsEqualTo(Pages);
    }

    /// <summary>A watermark behind the page is the first stream, and opacity adds a graphics state.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BehindWatermarkWithOpacityUsesAGraphicsState()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        PdfDocumentWatermarks.AddText(document, [0], Text, new(), new() { Behind = true, Opacity = HalfOpacity, RotationDegrees = Turn });
        var streams = OverlayTests.Streams(document, 0);
        var states = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.ExtGState)!;

        await Assert.That(streams[0]).Contains("HpState0 gs");
        await Assert.That(streams[0]).Contains("HpForm0 Do");
        await Assert.That(states.Count).IsEqualTo(1);
        await Assert.That(states.GetDictionary(states.GetKeyAt(0))!.GetNumber(document.Objects.Names.Intern("ca"u8))).IsEqualTo((double)HalfOpacity);
    }

    /// <summary>An image watermark draws the image through a form, with a soft mask for pixels that are not opaque.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageWatermarkDrawsThroughAForm()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var pixels = new byte[ImageEdge * ImageEdge * BytesPerPixel];
        pixels.AsSpan().Fill(byte.MaxValue);
        pixels[BytesPerPixel - 1] = 0;
        PdfDocumentWatermarks.AddImage(document, [0], new(pixels, ImageEdge, ImageEdge, ImageWidth), PdfWatermarkOptions.Default);
        var form = OverlayTests.FormOf(document, 0);
        var image = form.Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("WmImage"u8))!;

        await Assert.That(image.Dictionary.IsName(KnownName.Subtype, KnownName.Image)).IsTrue();
        await Assert.That(image.Dictionary.Get(KnownName.SMask).AsStream()).IsNotNull();
    }

    /// <summary>A watermark in a layer is controlled by an optional content group named after the layer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayerWatermarkIsControlledByAGroup()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfDocumentWatermarks.AddText(document, [0, 1, Pages - 1], Text, new(), new() { LayerName = Text });
        using var saved = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);

        await Assert.That(PdfDocumentLayers.GetOptionalContent(saved).Layers.Count).IsEqualTo(1);
        await Assert.That(OverlayTests.FormOf(saved, 1).Dictionary.ContainsKey(KnownName.OC)).IsTrue();
    }

    /// <summary>A tagged document marks the watermark as a pagination artifact of subtype Watermark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentMarksAWatermarkArtifact()
    {
        var pdf = ConformancePdf.Page();
        pdf.CatalogEntries = "/MarkInfo << /Marked true >> /StructTreeRoot << /Type /StructTreeRoot >>";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        PdfDocumentWatermarks.AddText(document, [0], Text, new(), PdfWatermarkOptions.Default);

        await Assert.That(OverlayTests.Streams(document, 0)[^1]).Contains("/Subtype /Watermark");
    }

    /// <summary>A document that claims PDF/A refuses a built-in font, and PDF/A-1 also refuses opacity; nothing changes.</summary>
    /// <param name="part">The claimed part.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfA1)]
    [Arguments(PdfA2)]
    public async Task PdfAClaimRefusesWhatBreaksIt(int part)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, part, "B", null);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);

        await Assert.That(() => PdfDocumentWatermarks.AddText(document, [0], Text, new(), PdfWatermarkOptions.Default)).Throws<InvalidOperationException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
    }

    /// <summary>A document that claims PDF/A-1 refuses a watermark with opacity below one, which needs transparency.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA1ClaimRefusesOpacity()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PdfA1, "B", null);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var opaque = new byte[BytesPerPixel];
        opaque.AsSpan().Fill(byte.MaxValue);

        await Assert.That(() => PdfDocumentWatermarks.AddImage(document, [0], new(opaque, 1, 1, ImageWidth), new() { Opacity = HalfOpacity })).Throws<InvalidOperationException>();
        PdfDocumentWatermarks.AddImage(document, [0], new(opaque, 1, 1, ImageWidth), PdfWatermarkOptions.Default);

        await Assert.That(OverlayTests.Streams(document, 0).Length).IsEqualTo(Pages);
    }

    /// <summary>Text the font cannot show, empty text and bad numbers are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadArgumentsAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        await Assert.That(() => PdfDocumentWatermarks.AddText(document, [0], string.Empty, new(), PdfWatermarkOptions.Default)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentWatermarks.AddText(document, [0], "中", new(), PdfWatermarkOptions.Default)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentWatermarks.AddText(document, [0], Text, new() { FontSize = 0 }, PdfWatermarkOptions.Default)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentWatermarks.AddText(document, [0], Text, new(), new() { Opacity = 0 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentWatermarks.AddText(document, [1], Text, new(), PdfWatermarkOptions.Default)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
    }

    /// <summary>The asynchronous forms edit and stop when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormsEditAndCancel()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        int[] pages = [0];

        await Assert.That(async () => await PdfDocumentWatermarks.AddTextAsync(document, pages, Text, new(), PdfWatermarkOptions.Default, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
        await PdfDocumentWatermarks.AddTextAsync(document, pages, Text, new(), PdfWatermarkOptions.Default, CancellationToken.None);

        await Assert.That(OverlayTests.Streams(document, 0).Length).IsEqualTo(Pages);
    }
}

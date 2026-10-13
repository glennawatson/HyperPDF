// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for reading and setting the five page boundaries of ISO 32000-2 14.11.2.</summary>
public sealed class PageBoxTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The index of the page the tests edit.</summary>
    private const int SecondPage = 1;

    /// <summary>The index of the right edge in a box array.</summary>
    private const int RightEdge = 2;

    /// <summary>The media box of the test pages.</summary>
    private static readonly PdfRectangle Letter = new(0, 0, 612, 792);

    /// <summary>A crop box inside the media box.</summary>
    private static readonly PdfRectangle Crop = new(20, 30, 500, 700);

    /// <summary>A bleed box inside the crop box.</summary>
    private static readonly PdfRectangle Bleed = new(30, 40, 490, 690);

    /// <summary>A trim box inside the bleed box.</summary>
    private static readonly PdfRectangle Trim = new(40, 50, 480, 680);

    /// <summary>An art box inside the trim box.</summary>
    private static readonly PdfRectangle Art = new(60, 70, 460, 660);

    /// <summary>The trim box with its corners given upper right first.</summary>
    private static readonly PdfRectangle SwappedTrim = new(480, 680, 40, 50);

    /// <summary>A box that extends past the media box on every side.</summary>
    private static readonly PdfRectangle Oversized = new(-10, -10, 700, 800);

    /// <summary>A box with no width.</summary>
    private static readonly PdfRectangle Empty = new(10, 10, 10, 50);

    /// <summary>A box wholly outside the media box.</summary>
    private static readonly PdfRectangle Outside = new(700, 800, 900, 1000);

    /// <summary>A box with a number that is not finite.</summary>
    private static readonly PdfRectangle NotFinite = new(0, 0, float.NaN, 50);

    /// <summary>A page without extra boxes reports the crop box for the bleed, trim and art boxes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingBoxesDefaultToTheCropBox()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Crop, Crop);
        var page = PdfDocumentPages.GetPage(document, 0);

        await Assert.That(page.GetBox(PdfPageBox.Bleed)).IsEqualTo(Crop);
        await Assert.That(page.TrimBox).IsEqualTo(Crop);
        await Assert.That(page.ArtBox).IsEqualTo(Crop);
    }

    /// <summary>All five boxes read back after the edit, an incremental save and a compact save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoxesRoundTripThroughBothWriters()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        SetAll(document, SecondPage);
        using var incremental = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        foreach (var saved in (PdfDocument[])[document, incremental, compact])
        {
            var page = PdfDocumentPages.GetPage(saved, SecondPage);

            await Assert.That(page.GetBox(PdfPageBox.Media)).IsEqualTo(Letter);
            await Assert.That(page.GetBox(PdfPageBox.Crop)).IsEqualTo(Crop);
            await Assert.That(page.GetBox(PdfPageBox.Bleed)).IsEqualTo(Bleed);
            await Assert.That(page.GetBox(PdfPageBox.Trim)).IsEqualTo(Trim);
            await Assert.That(page.GetBox(PdfPageBox.Art)).IsEqualTo(Art);
            await Assert.That(PdfDocumentPages.GetPage(saved, 0).TrimBox).IsEqualTo(Letter);
        }
    }

    /// <summary>Corners given in the wrong order are written lower left first, and a box past the media box is clipped to it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoxesAreNormalisedAndClippedToTheMediaBox()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var swapped = PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Trim, SwappedTrim);
        var clipped = PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Bleed, Oversized);
        var array = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.TrimBox)!;

        await Assert.That(swapped).IsEqualTo(Trim);
        await Assert.That(clipped).IsEqualTo(Letter);
        await Assert.That(array.GetNumber(0)).IsEqualTo((double)Trim.Left);
        await Assert.That(array.GetNumber(RightEdge)).IsEqualTo((double)Trim.Right);
    }

    /// <summary>A box that is empty, outside the media box or not finite is refused, and the page keeps its boxes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableBoxesAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        await Assert.That(() => PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Art, Empty)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Art, Outside)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Art, NotFinite)).Throws<ArgumentException>();
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary.ContainsKey(KnownName.ArtBox)).IsFalse();
    }

    /// <summary>Clearing a box restores its default, and the media box cannot be cleared.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearedBoxesTakeTheirDefault()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = PdfDocumentPageBoxes.SetBox(document, 0, PdfPageBox.Trim, Trim);
        PdfDocumentPageBoxes.ClearBox(document, 0, PdfPageBox.Trim);

        await Assert.That(PdfDocumentPages.GetPage(document, 0).TrimBox).IsEqualTo(Letter);
        await Assert.That(() => PdfDocumentPageBoxes.ClearBox(document, 0, PdfPageBox.Media)).Throws<ArgumentException>();
    }

    /// <summary>The asynchronous forms edit the same way and stop when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormsEditAndCancel()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = await PdfDocumentPageBoxes.SetBoxAsync(document, 0, PdfPageBox.Art, Art, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(PdfDocumentPages.GetPage(document, 0).ArtBox).IsEqualTo(Art);
        await Assert.That(async () => await PdfDocumentPageBoxes.ClearBoxAsync(document, 0, PdfPageBox.Art, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(PdfDocumentPages.GetPage(document, 0).ArtBox).IsEqualTo(Art);
    }

    /// <summary>Sets all five boxes of one page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page index.</param>
    private static void SetAll(PdfDocument document, int page)
    {
        _ = PdfDocumentPageBoxes.SetBox(document, page, PdfPageBox.Media, Letter);
        _ = PdfDocumentPageBoxes.SetBox(document, page, PdfPageBox.Crop, Crop);
        _ = PdfDocumentPageBoxes.SetBox(document, page, PdfPageBox.Bleed, Bleed);
        _ = PdfDocumentPageBoxes.SetBox(document, page, PdfPageBox.Trim, Trim);
        _ = PdfDocumentPageBoxes.SetBox(document, page, PdfPageBox.Art, Art);
    }
}

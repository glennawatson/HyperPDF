// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;
using HyperPdfLibrary.Document;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the watermarks, headers, footers, page and Bates numbers, backgrounds and crops that are added to pages.</summary>
public sealed class PageDecoratorTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 3;

    /// <summary>The colour of the stamped text: opaque black.</summary>
    private const uint Black = 0xFF000000;

    /// <summary>The size of the stamped text in points.</summary>
    private const float TextSize = 12;

    /// <summary>The margin of the stamped text in points.</summary>
    private const float Margin = 24;

    /// <summary>The first Bates number.</summary>
    private const int FirstBates = 100;

    /// <summary>The digits of a Bates number.</summary>
    private const int BatesDigits = 6;

    /// <summary>What is taken off each side when a page is cropped, in points.</summary>
    private const float CropAmount = 36;

    /// <summary>What a crop takes off the width or the height, which is the margin on both sides.</summary>
    private const float BothSides = CropAmount + CropAmount;

    /// <summary>A pale yellow background.</summary>
    private const uint Yellow = 0xFFFFF4B0;

    /// <summary>How close two lengths in points are for them to be equal.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The words of a draft watermark.</summary>
    private const string Draft = "DRAFT";

    /// <summary>The last page of the test document.</summary>
    private const int LastPage = 2;

    /// <summary>Every page of the test document.</summary>
    private static readonly int[] EveryPage = [0, 1, LastPage];

    /// <summary>Only the first page.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>Only the second page.</summary>
    private static readonly int[] SecondPage = [1];

    /// <summary>The page number and the page count are written in the words of each page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageNumbersAreWrittenOnEachPage()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;

        await decorator.StampText(EveryPage, Stamp("Page {page} of {pages}", PageStampAnchor.BottomCenter)).ToTask();

        await Assert.That(TextOf(test, 1)).Contains("Page 2 of 3");
        await Assert.That(TextOf(test, 0)).Contains("Page 1 of 3");
    }

    /// <summary>A Bates number counts the stamped pages from the number asked for, padded with zeros.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BatesNumbersCountUpFromTheFirst()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;
        var stamp = Stamp("ACME-{number}", PageStampAnchor.BottomRight) with { FirstNumber = FirstBates, Digits = BatesDigits };

        await decorator.StampText(EveryPage, stamp).ToTask();

        await Assert.That(TextOf(test, 0)).Contains("ACME-000100");
        await Assert.That(TextOf(test, LastPage)).Contains("ACME-000102");
    }

    /// <summary>Only the pages chosen get the text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyTheChosenPagesAreStamped()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;

        await decorator.StampText(SecondPage, Stamp(Draft, PageStampAnchor.Center)).ToTask();

        await Assert.That(TextOf(test, 1)).Contains(Draft);
        await Assert.That(TextOf(test, 0)).DoesNotContain(Draft);
    }

    /// <summary>A crop takes the margins off the page as it is shown, and undoing it gives the page back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CropTakesMarginsOffAndCanBeUndone()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;
        var before = PdfDocumentPages.GetPage(test.Document.Document, 0).CropBox;

        await decorator.Crop(FirstPage, new(CropAmount, CropAmount, CropAmount, CropAmount)).ToTask();
        var cropped = PdfDocumentPages.GetPage(test.Document.Document, 0).CropBox;
        _ = HyperPdfEditTransactions.Step(test.Document.Document, true);
        var restored = PdfDocumentPages.GetPage(test.Document.Document, 0).CropBox;

        var narrower = (before.Right - before.Left) - BothSides;
        var shorter = (before.Top - before.Bottom) - BothSides;
        await Assert.That(cropped.Right - cropped.Left).IsBetween(narrower - Tolerance, narrower + Tolerance);
        await Assert.That(cropped.Top - cropped.Bottom).IsBetween(shorter - Tolerance, shorter + Tolerance);
        await Assert.That(restored).IsEqualTo(before);
    }

    /// <summary>Taking nothing off any side shows the whole page again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CropWithNoMarginsShowsTheWholePage()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;
        var media = PdfDocumentPages.GetPage(test.Document.Document, 0).MediaBox;
        await decorator.Crop(FirstPage, new(CropAmount, CropAmount, CropAmount, CropAmount)).ToTask();

        await decorator.Crop(FirstPage, default).ToTask();

        await Assert.That(PdfDocumentPages.GetPage(test.Document.Document, 0).CropBox).IsEqualTo(media);
    }

    /// <summary>A background colour is drawn first, as a filled shape in that colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackgroundIsTheFirstObjectOfThePage()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;
        var editor = DocumentFeature<IPageContentEditor>.Get(test.Document)!;

        await decorator.FillBackground(FirstPage, Yellow).ToTask();
        var style = await editor.ReadStyle(0, 0).ToTask();

        await Assert.That(style.Fill).IsEqualTo(Yellow);
    }

    /// <summary>Choosing no page is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoPagesIsRefused()
    {
        using var test = new HyperPdfTestDocument(Pages);
        var decorator = DocumentFeature<IPageDecorator>.Get(test.Document)!;

        await Assert.That(async () => await decorator.Crop(ReadOnlyMemory<int>.Empty, default).ToTask()).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Makes a stamp of black text.</summary>
    /// <param name="text">The words.</param>
    /// <param name="anchor">Where it sits.</param>
    /// <returns>The stamp.</returns>
    private static PageTextStamp Stamp(string text, PageStampAnchor anchor) =>
        new(text, PageTextFamily.Sans, false, false, TextSize, Black, 1, 0, anchor, Margin, false);

    /// <summary>Reads the words of a page.</summary>
    /// <param name="test">The document.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns>The page's characters as text.</returns>
    private static string TextOf(HyperPdfTestDocument test, int page)
    {
        var characters = new List<PageCharacter>();
        DocumentFeature<ITextLayoutSource>.Get(test.Document)!.GetCharacters(page, characters);
        var text = new StringBuilder(characters.Count);
        foreach (var character in characters)
        {
            _ = text.Append(character.Value);
        }

        return text.ToString();
    }
}

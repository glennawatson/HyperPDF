// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Ocr;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that recognised words are written to the page they were read from, even if pages move while they are recognised.</summary>
public sealed class TextLayerPageTokenTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 4;

    /// <summary>Where the last page is after the first is deleted.</summary>
    private const int ShiftedPage = Pages - 2;

    /// <summary>The recogniser's confidence in the test word.</summary>
    private const float Confidence = 90;

    /// <summary>The word written.</summary>
    private const string Marker = "Zebra";

    /// <summary>The characters of page text read back.</summary>
    private const int TextLength = 400;

    /// <summary>The first page, as a selection.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>The word, placed near the top left of the page.</summary>
    private static readonly OcrWord[] Words = [new(Marker, new(40, 30, 80, 20), Confidence)];

    /// <summary>Words go onto the page the token named after an earlier page is deleted, not onto the page now at that index.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task WordsFollowThePageWhenAnEarlierPageIsDeleted(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var writer = DocumentFeature<ITextLayerWriter>.Cast(test.Document)!;
        var token = writer.GetPageToken(Pages - 1);

        await HyperPdfDocumentPageManagement.GetPageManager(test.Document).ApplyAsync(new(PageEditKind.Delete, FirstPage, 0), cancellationToken);
        var written = writer.AddTextLayerToPage(token, Words);

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(test.Document.GetText(ShiftedPage, 0, TextLength)).Contains(Marker);
        await Assert.That(test.Document.GetText(0, 0, TextLength)).DoesNotContain(Marker);
    }

    /// <summary>Words are not written when the page the token named is deleted.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task WordsAreNotWrittenWhenThePageIsDeleted(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var writer = DocumentFeature<ITextLayerWriter>.Cast(test.Document)!;
        var token = writer.GetPageToken(0);

        await HyperPdfDocumentPageManagement.GetPageManager(test.Document).ApplyAsync(new(PageEditKind.Delete, FirstPage, 0), cancellationToken);

        await Assert.That(writer.AddTextLayerToPage(token, Words)).IsEqualTo(0);
        await Assert.That(writer.GetPageToken(Pages)).IsEqualTo(0);
        await Assert.That(test.Document.GetText(0, 0, TextLength)).DoesNotContain(Marker);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks reading the text inside a rectangle, and reading a page in the order of its article threads.</summary>
public sealed class TextRegionTests
{
    /// <summary>Two columns of two lines each: the left column at x 10, the right column at x 150, with baselines at y 100 and y 80.</summary>
    private const string Columns = "BT /F1 10 Tf 10 100 Td (L1) Tj 0 -20 Td (L2) Tj ET BT /F1 10 Tf 150 100 Td (R1) Tj 0 -20 Td (R2) Tj ET";

    /// <summary>The page the beads are on.</summary>
    private const int FirstPage = 0;

    /// <summary>The right edge of the left column's rectangle.</summary>
    private const float LeftColumnRight = 100;

    /// <summary>The left edge of the right column's rectangle.</summary>
    private const float RightColumnLeft = 140;

    /// <summary>The right edge of the page's text.</summary>
    private const float PageRight = 250;

    /// <summary>The bottom of the rectangle that covers both lines.</summary>
    private const float BothLinesBottom = 70;

    /// <summary>The bottom of the rectangle that covers only the first line.</summary>
    private const float FirstLineBottom = 90;

    /// <summary>The top of a rectangle that lies below both lines.</summary>
    private const float BelowTheText = 50;

    /// <summary>The top of every rectangle, above the first line.</summary>
    private const float Top = 115;

    /// <summary>Verifies that a rectangle around one column reads only that column, both lines of it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rectangle_AroundAColumn_ReadsThatColumn()
    {
        var page = TextTestDocument.Extract(Columns);

        await Assert.That(page.GetTextInRectangle(new(0, BothLinesBottom, LeftColumnRight, Top))).IsEqualTo("L1\r\nL2");
        await Assert.That(page.GetTextInRectangle(new(RightColumnLeft, BothLinesBottom, PageRight, Top))).IsEqualTo("R1\r\nR2");
    }

    /// <summary>Verifies that a rectangle across both columns on one line reads the pieces with a line break between them where it skips characters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rectangle_AcrossColumnsOnOneLine_SeparatesThePieces()
    {
        var page = TextTestDocument.Extract(Columns);

        await Assert.That(page.GetTextInRectangle(new(0, FirstLineBottom, PageRight, Top))).IsEqualTo("L1\r\nR1");
    }

    /// <summary>Verifies that a rectangle with no text in it gives an empty string, and that its corners may be given in either order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Rectangle_WithNoText_IsEmptyAndAcceptsCornersInAnyOrder()
    {
        var page = TextTestDocument.Extract(Columns);

        await Assert.That(page.GetTextInRectangle(new(0, 0, LeftColumnRight, BelowTheText))).IsEmpty();
        await Assert.That(page.GetTextInRectangle(new(LeftColumnRight, Top, 0, BothLinesBottom))).IsEqualTo("L1\r\nL2");
    }

    /// <summary>Verifies that article order follows the thread, so a thread that starts in the right column reads it first.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArticleText_FollowsTheThreadsBeads()
    {
        var document = TextTestDocument.Open(TextTestDocument.Create(Columns).ToBytes());
        _ = PdfDocumentThreadEditing.AddThread(
            document,
            "Story",
            [
                new PdfBead(FirstPage, new PdfRectangle(RightColumnLeft, BothLinesBottom, PageRight, Top)),
                new PdfBead(FirstPage, new PdfRectangle(0, BothLinesBottom, LeftColumnRight, Top)),
            ]);

        await Assert.That(PdfDocumentText.GetArticleText(document, FirstPage)).IsEqualTo("R1\r\nR2\r\nL1\r\nL2");
        await Assert.That(await PdfDocumentText.GetArticleTextAsync(document, FirstPage, CancellationToken.None)).IsEqualTo("R1\r\nR2\r\nL1\r\nL2");
    }

    /// <summary>Verifies that a page with no beads has no article text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ArticleText_OfAPageWithNoBeads_IsNull()
    {
        var document = TextTestDocument.Open(TextTestDocument.Create(Columns).ToBytes());

        await Assert.That(PdfDocumentText.GetArticleText(document, FirstPage)).IsNull();
    }
}

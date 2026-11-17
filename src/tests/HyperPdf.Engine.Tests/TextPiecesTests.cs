// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>A text a page draws in pieces, such as a letter and a mark a font lifts with a text rise, is one text to the page's caller.</summary>
public sealed class TextPiecesTests
{
    /// <summary>The words of the text drawn in pieces.</summary>
    private const string Pieces = "Hello";

    /// <summary>The words of the text drawn in one piece.</summary>
    private const string Whole = "Other";

    /// <summary>The number of objects the page lists.</summary>
    private const int ListedObjects = 2;

    /// <summary>More than the width of the first piece, "He", at twelve points, so the box holds all the pieces.</summary>
    private const float WiderThanTwoLetters = 20F;

    /// <summary>The page: one text drawn in three shows, one of them lifted, inside one run of replacement text, and one text drawn at once.</summary>
    private const string Content = $"""
        /Span << /ActualText ({Pieces}) >> BDC BT /F1 12 Tf 72 700 Td [(He)] TJ 3 Ts [(l)] TJ 0 Ts [(lo)] TJ ET EMC
        /Span << /ActualText ({Whole}) >> BDC BT /F1 12 Tf 72 600 Td ({Whole}) Tj ET EMC
        """;

    /// <summary>The pieces of one text are listed as one text with the words and the box of all of them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PiecesAreListedAsOneText()
    {
        using var test = new HyperPdfTestDocument(Build());
        var objects = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, 0, CancellationToken.None);

        await Assert.That(objects.Length).IsEqualTo(ListedObjects);
        await Assert.That(objects[0].Text).IsEqualTo(Pieces);
        await Assert.That(objects[0].Bounds.Width).IsGreaterThan(WiderThanTwoLetters);
    }

    /// <summary>Deleting a text drawn in pieces deletes every piece.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletingTheTextDeletesEveryPiece()
    {
        using var test = new HyperPdfTestDocument(Build());
        var objects = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, 0, CancellationToken.None);
        await HyperPdfDocumentPageContentEditing.DeleteAsync(test.Document, 0, objects[0].Index, CancellationToken.None);
        var after = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, 0, CancellationToken.None);

        await Assert.That(after.Length).IsEqualTo(1);
        await Assert.That(after[0].Text).IsEqualTo(Whole);
    }

    /// <summary>Retyping a text drawn in pieces writes the new words once and leaves none of the old.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetypingTheTextReplacesEveryPiece()
    {
        const string Bye = "Goodbye";
        using var test = new HyperPdfTestDocument(Build());
        var objects = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, 0, CancellationToken.None);
        await HyperPdfDocumentPageContentEditing.ReplaceTextAsync(test.Document, 0, objects[0].Index, Bye, CancellationToken.None);
        var after = await HyperPdfDocumentPageContentEditing.ReadObjectsAsync(test.Document, 0, CancellationToken.None);

        await Assert.That(after.Select(static o => o.Text).Order(StringComparer.Ordinal)).IsEquivalentTo([Bye, Whole]);
    }

    /// <summary>Builds the PDF of the page.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
        MiniPdf.Stream(string.Empty, Content),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
}

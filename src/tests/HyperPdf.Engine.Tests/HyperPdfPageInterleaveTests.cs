// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;
using ReactiveUI.Primitives;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests for shuffling a file's pages in with <see cref="HyperPdfPageManager.Interleave"/>.</summary>
public sealed class HyperPdfPageInterleaveTests
{
    /// <summary>The pages of the open document.</summary>
    private const int OwnPages = 3;

    /// <summary>The pages of the file shuffled in.</summary>
    private const int FilePages = 2;

    /// <summary>How far apart two sides may be and still be the same, in points.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The side of the small page that marks the file's second page, in points.</summary>
    private const float SmallSide = 100;

    /// <summary>Where the file's second page lands: after the document's second page.</summary>
    private const int SmallPlace = 3;

    /// <summary>
    /// The file's pages go one after each of the document's: the document's first, the file's first, the document's second, the
    /// file's second, then the document's third. It is one undo step.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Interleave_PutsEachOfTheFilesPagesAfterTheDocumentsPage()
    {
        var file = Path.Combine(Path.GetTempPath(), $"interleave-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(file, FileWithASmallSecondPage());
            using var test = new HyperPdfTestDocument(TestPdf.Create(OwnPages));
            var manager = HyperPdfDocumentPageManagement.GetPageManager(test.Document);

            PageEditReport report = new();
            await manager.Interleave(file, report).ToTask();
            var widths = new float[test.Document.PageCount];
            for (var page = 0; page < widths.Length; page++)
            {
                widths[page] = PdfDocumentPages.GetPage(test.Document.Document, page).MediaBox.Width;
            }

            await Assert.That(test.Document.PageCount).IsEqualTo(OwnPages + FilePages);
            await Assert.That(Math.Abs(widths[SmallPlace] - SmallSide)).IsLessThan(Tolerance);
            await Assert.That(Array.FindAll(widths, static width => Math.Abs(width - SmallSide) < Tolerance).Length).IsEqualTo(1);
            await Assert.That(manager.UndoLabel).IsEqualTo("Interleave pages");
            await Assert.That(await manager.Undo().ToTask()).IsTrue();
            await Assert.That(test.Document.PageCount).IsEqualTo(OwnPages);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Makes a two-page PDF whose second page is small, so where it lands can be seen.</summary>
    /// <returns>The file's bytes.</returns>
    private static byte[] FileWithASmallSecondPage()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        PdfBlankPages.InsertPage(document, 1, SmallSide, SmallSide);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
    }
}

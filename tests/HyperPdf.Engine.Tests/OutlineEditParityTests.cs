// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that PDFium reads an edited outline the way HyperPDF does, in an incremental update and in a compact rewrite.</summary>
public sealed class OutlineEditParityTests
{
    /// <summary>Pages in the sample.</summary>
    private const int Pages = 4;

    /// <summary>The second page's index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>A web address with an escaped space.</summary>
    private const string Address = "https://example.com/a%20b";

    /// <summary>An edited outline gives PDFium the same titles, nesting, targets and open states.</summary>
    /// <param name="compact">Whether the document is saved as a compact rewrite rather than an incremental update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PdfiumReadsTheEditedOutline(bool compact)
    {
        using var edited = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var ids = await PdfDocumentOutlineEditing.AddBookmarksAsync(
            edited,
            default,
            -1,
            [
                Item("Part one", 0) with { IsOpen = true, Children = [Item("Chapter A", SecondPage), Item("Chapter B", ThirdPage)] },
                Item("Part two", ThirdPage) with { Children = [Item("Chapter C", ThirdPage)] },
                new("Website", PdfBookmarkTarget.ToUri(Address)),
            ],
            CancellationToken.None);
        PdfDocumentOutlineEditing.RenameBookmark(edited, ids[0], "Part 1");
        PdfDocumentOutlineEditing.MoveBookmark(edited, ids[2], default, 0);
        PdfDocumentOutlineEditing.SetBookmarkOpen(edited, ids[1], true);
        var bytes = compact ? PdfCompactWriter.Save(edited.Objects, PdfCompactOptions.Default) : PdfIncrementalWriter.Save(edited.Objects);

        using var pair = new EnginePair(bytes);

        var pdfium = PdfiumReferenceBookmarks.GetBookmarks(pair.Pdfium);
        var library = PdfDocumentNavigation.GetOutline(pair.Library);
        await AssertSame(library, pdfium);
        await Assert.That(pdfium[0].Uri).IsEqualTo(Address);
        await Assert.That(pdfium[1].Title).IsEqualTo("Part 1");
        await Assert.That(pdfium[1].Children[SecondPage].PageIndex).IsEqualTo(ThirdPage);
    }

    /// <summary>Makes a bookmark that goes to the top of a page.</summary>
    /// <param name="title">The title.</param>
    /// <param name="page">The page.</param>
    /// <returns>The bookmark.</returns>
    private static PdfBookmarkSpec Item(string title, int page) => new(title, PdfBookmarkTarget.ToPage(PdfExplicitDestination.FitPage(page)));

    /// <summary>Checks that PDFium's entries match HyperPDF's, level by level.</summary>
    /// <param name="library">HyperPDF's entries.</param>
    /// <param name="pdfium">PDFium's entries.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSame(IReadOnlyList<PdfOutlineItem> library, IReadOnlyList<PdfiumBookmark> pdfium)
    {
        await Assert.That(pdfium.Count).IsEqualTo(library.Count);
        for (var i = 0; i < library.Count; i++)
        {
            await Assert.That(pdfium[i].Title).IsEqualTo(library[i].Title);
            await Assert.That(pdfium[i].Count > 0).IsEqualTo(library[i].IsOpen);
            await Assert.That(pdfium[i].PageIndex).IsEqualTo(library[i].Action.Value is GoToAction go ? go.Destination.PageIndex : -1);
            await AssertSame(library[i].Children, pdfium[i].Children);
        }
    }
}

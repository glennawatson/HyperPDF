// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentOutlineEditing"/>.</summary>
public sealed class OutlineEditTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 4;

    /// <summary>The top level bookmarks in the large outline.</summary>
    private const int Chapters = 100;

    /// <summary>The bookmarks below each chapter in the large outline.</summary>
    private const int Sections = 100;

    /// <summary>Bookmarks added one at a time to the large outline.</summary>
    private const int Singles = 500;

    /// <summary>The second page's index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>Every other bookmark added to the large outline is removed again.</summary>
    private const int Gap = 2;

    /// <summary>The title of the second chapter of the sample book.</summary>
    private const string SecondChapter = "Chapter 2";

    /// <summary>The title of a bookmark added and moved by the tests.</summary>
    private const string Introduction = "Introduction";

    /// <summary>A blue component of a title colour.</summary>
    private const float HalfBlue = 0.5F;

    /// <summary>A zoom factor of 200 percent.</summary>
    private const float DoubleZoom = 2F;

    /// <summary>A left position on a page.</summary>
    private const float LeftMargin = 10F;

    /// <summary>A top position on a page.</summary>
    private const float TopPosition = 700F;

    /// <summary>The number of top level entries, and of the sections under a chapter, in the sample book.</summary>
    private const int Chapters2 = 2;

    /// <summary>The root count of the sample book: two chapters and the two sections of the open one.</summary>
    private const int RootCount = 4;

    /// <summary>A nested outline is written with correct links, counts, targets and open states.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuildsANestedOutline()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);

        var ids = await AddBookAsync(document);

        await Assert.That(ids.Count).IsEqualTo(Chapters2);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(document.Catalog.GetDictionary(KnownName.Outlines)!.GetInteger(KnownName.Count)).IsEqualTo(RootCount).Because(OutlineInvariants.Describe(document));
        var outline = PdfDocumentNavigation.GetOutline(document);
        await Assert.That(outline.Count).IsEqualTo(Chapters2);
        await Assert.That(outline[0].IsOpen).IsTrue();
        await Assert.That(outline[0].Children.Length).IsEqualTo(Chapters2);
        await Assert.That(outline[1].IsOpen).IsFalse();
        await Assert.That(outline[0].Children[1].Title).IsEqualTo("Section 1.2");
        await Assert.That(outline[0].Id).IsEqualTo(ids[0]);
    }

    /// <summary>Insert, rename, move and remove keep every link and count correct at each step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsKeepLinksAndCounts()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var ids = await AddBookAsync(document);
        var chapterOne = ids[0];
        var chapterTwo = ids[1];

        var added = PdfDocumentOutlineEditing.AddBookmark(document, chapterOne, 0, Leaf("Overview", 0));
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[0].Children[0].Title).IsEqualTo("Overview");

        PdfDocumentOutlineEditing.RenameBookmark(document, added, Introduction);
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[0].Children[0].Title).IsEqualTo(Introduction);

        PdfDocumentOutlineEditing.MoveBookmark(document, added, chapterTwo, -1);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[1].Children[^1].Title).IsEqualTo(Introduction);

        PdfDocumentOutlineEditing.MoveBookmark(document, chapterTwo, default, 0);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[0].Title).IsEqualTo(SecondChapter);

        PdfDocumentOutlineEditing.RemoveBookmark(document, chapterOne);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(PdfDocumentNavigation.GetOutline(document).Count).IsEqualTo(1);
        await Assert.That(StoreReading.GetObject(document.Objects, chapterOne).IsNull).IsTrue();

        PdfDocumentOutlineEditing.RemoveBookmark(document, chapterTwo);
        await Assert.That(PdfDocumentNavigation.GetOutline(document)).IsEmpty();
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
    }

    /// <summary>Opening and closing an entry changes its count and its ancestors'.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenAndCloseChangeCounts()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var ids = await AddBookAsync(document);

        PdfDocumentOutlineEditing.SetBookmarkOpen(document, ids[0], false);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(document.Catalog.GetDictionary(KnownName.Outlines)!.GetInteger(KnownName.Count)).IsEqualTo(RootCount - Chapters2);

        PdfDocumentOutlineEditing.SetBookmarkOpen(document, ids[1], true);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[1].IsOpen).IsTrue();

        // A child added under a closed entry changes that entry's count but not the root's.
        var before = document.Catalog.GetDictionary(KnownName.Outlines)!.GetInteger(KnownName.Count);
        _ = PdfDocumentOutlineEditing.AddBookmark(document, ids[0], Leaf("Hidden", 0));
        await Assert.That(document.Catalog.GetDictionary(KnownName.Outlines)!.GetInteger(KnownName.Count)).IsEqualTo(before);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
    }

    /// <summary>Colour, style flags and each kind of target are written and read back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StyleAndTargetsRoundTrip()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfDocumentNamedDestinations.SetNamedDestination(document, "start", PdfExplicitDestination.FitPage(ThirdPage));
        var id = PdfDocumentOutlineEditing.AddBookmark(document, default, Leaf("Styled", 0));

        PdfDocumentOutlineEditing.SetBookmarkStyle(document, id, new(1, 0, HalfBlue, true, true));
        var style = PdfDocumentNavigation.GetOutline(document)[0].Style;
        await Assert.That(style).IsNotNull();
        await Assert.That(style!.Red).IsEqualTo(1F);
        await Assert.That(style.Blue).IsEqualTo(HalfBlue);
        await Assert.That(style.Italic).IsTrue();
        await Assert.That(style.Bold).IsTrue();
        PdfDocumentOutlineEditing.SetBookmarkStyle(document, id, null);
        await Assert.That(PdfDocumentNavigation.GetOutline(document)[0].Style).IsNull();

        PdfDocumentOutlineEditing.SetBookmarkTarget(document, id, PdfBookmarkTarget.ToPage(PdfExplicitDestination.At(SecondPage, LeftMargin, TopPosition, DoubleZoom)));
        var go = (GoToAction)PdfDocumentNavigation.GetOutline(document)[0].Action.Value!;
        await Assert.That(go.Destination.PageIndex).IsEqualTo(SecondPage);
        await Assert.That(go.Destination.Zoom).IsEqualTo(DoubleZoom);

        PdfDocumentOutlineEditing.SetBookmarkTarget(document, id, PdfBookmarkTarget.ToNamedDestination("start"));
        await Assert.That(((GoToAction)PdfDocumentNavigation.GetOutline(document)[0].Action.Value!).Destination.PageIndex).IsEqualTo(ThirdPage);

        PdfDocumentOutlineEditing.SetBookmarkTarget(document, id, PdfBookmarkTarget.ToUri("https://example.com/a%20b"));
        await Assert.That(((UriAction)PdfDocumentNavigation.GetOutline(document)[0].Action.Value!).Uri).IsEqualTo("https://example.com/a%20b");

        PdfDocumentOutlineEditing.SetBookmarkTarget(document, id, PdfBookmarkTarget.ToNamedAction("NextPage"));
        await Assert.That(((NamedAction)PdfDocumentNavigation.GetOutline(document)[0].Action.Value!).Name).IsEqualTo("NextPage");

        await Assert.That(() => PdfDocumentOutlineEditing.SetBookmarkTarget(document, id, PdfBookmarkTarget.ToNamedAction("Print"))).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentOutlineEditing.SetBookmarkTarget(document, id, PdfBookmarkTarget.ToUri("https://example.com/é"))).Throws<ArgumentException>();
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
    }

    /// <summary>A bookmark cannot move into itself or its descendants, and a failed edit changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CannotMoveIntoItself()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var ids = await AddBookAsync(document);
        var section = PdfDocumentNavigation.GetOutline(document)[0].Children[0].Id;
        var before = OutlineInvariants.Describe(document);

        await Assert.That(() => PdfDocumentOutlineEditing.MoveBookmark(document, ids[0], section, 0)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentOutlineEditing.MoveBookmark(document, ids[0], ids[0], 0)).Throws<ArgumentException>();
        await Assert.That(() => PdfDocumentOutlineEditing.RemoveBookmark(document, PdfDocumentPages.GetPage(document, 0).Id)).Throws<ArgumentException>();

        await Assert.That(OutlineInvariants.Describe(document)).IsEqualTo(before);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
    }

    /// <summary>Undo restores the outline each edit changed, one edit at a time, and redo applies it again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoRestoresEachEdit()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var snapshots = new List<string> { OutlineInvariants.Describe(document) };
        var ids = await AddBookAsync(document);
        snapshots.Add(OutlineInvariants.Describe(document));
        PdfDocumentOutlineEditing.RenameBookmark(document, ids[0], "Renamed");
        snapshots.Add(OutlineInvariants.Describe(document));
        PdfDocumentOutlineEditing.MoveBookmark(document, ids[1], ids[0], 0);
        snapshots.Add(OutlineInvariants.Describe(document));
        PdfDocumentOutlineEditing.SetBookmarkOpen(document, ids[0], false);
        snapshots.Add(OutlineInvariants.Describe(document));
        PdfDocumentOutlineEditing.RemoveBookmark(document, ids[0]);
        snapshots.Add(OutlineInvariants.Describe(document));

        for (var i = snapshots.Count - 1 - 1; i >= 0; i--)
        {
            await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
            await Assert.That(OutlineInvariants.Describe(document)).IsEqualTo(snapshots[i]);
            await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        }

        await Assert.That(PdfDocumentEditing.Undo(document)).IsFalse();
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(OutlineInvariants.Describe(document)).IsEqualTo(snapshots[1]);
    }

    /// <summary>The edited outline saves both ways, reopens the same, passes qpdf, and qpdf reads the same titles.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAndReopensTheSame(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var ids = await AddBookAsync(document);
        PdfDocumentOutlineEditing.SetBookmarkStyle(document, ids[0], new(0, 0, 1, false, true));
        PdfDocumentOutlineEditing.MoveBookmark(document, ids[1], default, 0);
        var expected = OutlineInvariants.Describe(document);

        var incremental = EditChecks.SaveIncremental(document);
        var compact = EditChecks.SaveCompact(document);
        foreach (var file in new[] { incremental, compact })
        {
            using var reopened = PdfDocumentReader.Open(file, null);
            await Assert.That(OutlineInvariants.Describe(reopened)).IsEqualTo(expected);
            await Assert.That(OutlineInvariants.Check(reopened)).IsEmpty();
        }

        await EditChecks.AssertQpdfCleanAsync("outline-incremental", incremental, cancellationToken);
        await EditChecks.AssertQpdfCleanAsync("outline-compact", compact, cancellationToken);
        var outlines = await Writing.QpdfJson.ReadAsync(compact, "outlines", cancellationToken);
        if (outlines is not { } json)
        {
            return;
        }

        await Assert.That(json.GetArrayLength()).IsEqualTo(Chapters2);
        await Assert.That(json[0].GetProperty("title").GetString()).IsEqualTo(SecondChapter);
        await Assert.That(json[1].GetProperty("kids").GetArrayLength()).IsEqualTo(Chapters2);
    }

    /// <summary>A large outline is built in one edit, and single adds, inserts and removes stay correct.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeOutlineStaysCorrect()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var specs = new List<PdfBookmarkSpec>();
        for (var chapter = 0; chapter < Chapters; chapter++)
        {
            var sections = new List<PdfBookmarkSpec>();
            for (var section = 0; section < Sections; section++)
            {
                sections.Add(Leaf(string.Create(CultureInfo.InvariantCulture, $"Section {chapter}.{section}"), chapter % Pages));
            }

            specs.Add(Leaf(string.Create(CultureInfo.InvariantCulture, $"Chapter {chapter}"), chapter % Pages) with { Children = sections, IsOpen = chapter % Pages == 0 });
        }

        var ids = await PdfDocumentOutlineEditing.AddBookmarksAsync(document, default, -1, specs, CancellationToken.None);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
        for (var i = 0; i < Singles; i++)
        {
            var extra = PdfDocumentOutlineEditing.AddBookmark(document, ids[i % Chapters], i % Sections, Leaf("Extra", 0));
            if (i % Gap == 0)
            {
                PdfDocumentOutlineEditing.RemoveBookmark(document, extra);
            }

            _ = PdfDocumentOutlineEditing.AddBookmark(document, default, Leaf("Again", 0));
        }

        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
    }

    /// <summary>A certified document refuses outline edits and is left as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertifiedDocumentRefusesOutlineEdits()
    {
        using var document = PdfDocumentReader.Open(EditGuardSamples.Certified(1), null);

        var thrown = await Assert.That(() => PdfDocumentOutlineEditing.AddBookmark(document, default, Leaf("A", 0))).Throws<InvalidOperationException>();

        await Assert.That(thrown!.Message).Contains("certified");
        await Assert.That(document.Catalog.GetDictionary(KnownName.Outlines)).IsNull();
    }

    /// <summary>Counts missing from an outline made by another program are worked out again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepairWritesMissingCounts()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = await AddBookAsync(document);
        var rootId = document.Catalog.GetRaw(KnownName.Outlines).AsReference();
        var transaction = PdfDocumentEditing.BeginEdit(document, "Break counts");
        var broken = transaction.CloneDictionary(rootId)!;
        _ = broken.Remove(KnownName.Count);
        transaction.Replace(rootId, PdfValue.FromDictionary(broken));
        transaction.Commit();
        await Assert.That(OutlineInvariants.Check(document)).IsNotEmpty();

        var changed = await PdfDocumentOutlineEditing.RepairBookmarkCountsAsync(document, CancellationToken.None);

        await Assert.That(changed).IsGreaterThan(0);
        await Assert.That(OutlineInvariants.Check(document)).IsEmpty();
    }

    /// <summary>Adds the sample book to the top level of an outline.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The objects of the two chapters.</returns>
    private static async Task<IReadOnlyList<PdfObjectId>> AddBookAsync(PdfDocument document) =>
        await PdfDocumentOutlineEditing.AddBookmarksAsync(document, default, -1, Book(), CancellationToken.None);

    /// <summary>Makes a bookmark with no children.</summary>
    /// <param name="title">The title.</param>
    /// <param name="page">The page it goes to.</param>
    /// <returns>The bookmark.</returns>
    private static PdfBookmarkSpec Leaf(string title, int page) => new(title, PdfBookmarkTarget.ToPage(PdfExplicitDestination.FitPage(page)));

    /// <summary>Makes two chapters: the first open with two sections, the second closed with two sections.</summary>
    /// <returns>The bookmarks.</returns>
    private static List<PdfBookmarkSpec> Book() =>
    [
        Leaf("Chapter 1", 0) with { IsOpen = true, Children = [Leaf("Section 1.1", 0), Leaf("Section 1.2", SecondPage)] },
        Leaf(SecondChapter, SecondPage) with { Children = [Leaf("Section 2.1", SecondPage), Leaf("Section 2.2", ThirdPage)] },
    ];
}

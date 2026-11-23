// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests shuffling a file's pages in and saving the document in parts that start at its bookmarks.</summary>
public sealed class PagePartsTests
{
    /// <summary>The pages of the open document.</summary>
    private const int OwnPages = 3;

    /// <summary>The pages of the file shuffled in.</summary>
    private const int FilePages = 2;

    /// <summary>The pages of the document split at bookmarks.</summary>
    private const int BookPages = 4;

    /// <summary>Where the file's pages land: after the document's first and second pages.</summary>
    private static readonly int[] ShuffledIn = [1, 3];

    /// <summary>The files the split makes.</summary>
    private static readonly string[] Parts = ["book-part-1.pdf", "book-part-2.pdf", "book-part-3.pdf", "book-part-4.pdf"];

    /// <summary>The file's pages go between the document's, are selected afterwards, and one undo takes them out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShufflesAFilesPagesIn()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("fronts.pdf", OwnPages)]);
        var backs = test.CreateDocument("backs.pdf", FilePages);
        var tab = main.SelectedTab!;
        using var choose = tab.Pages.InsertInteraction.RegisterHandler(context => context.SetOutput([backs]));

        _ = await tab.Pages.InterleaveCommand.Execute().ToTask();
        var afterShuffle = tab.PageCount;
        var selected = tab.Pages.SelectedPages.ToArray();
        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        await Assert.That(afterShuffle).IsEqualTo(OwnPages + FilePages);
        await Assert.That(selected).IsEquivalentTo(ShuffledIn);
        await Assert.That(tab.PageCount).IsEqualTo(OwnPages);
    }

    /// <summary>Each top bookmark starts a part, and each part is saved as a file of its own. The sample has a bookmark on every page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesThePartsBetweenBookmarks()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("book.pdf", BookPages)]);
        var tab = main.SelectedTab!;
        var folder = Path.Combine(test.Directory, "parts");
        _ = Directory.CreateDirectory(folder);
        using var choose = tab.Pages.SplitFolderInteraction.RegisterHandler(context => context.SetOutput(folder));

        _ = await tab.Pages.SplitAtBookmarksCommand.Execute().ToTask();
        var written = new List<string>();
        foreach (var file in new DirectoryInfo(folder).GetFiles())
        {
            written.Add(file.Name);
        }

        await Assert.That(written).IsEquivalentTo(Parts);
    }
}

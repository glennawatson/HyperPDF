// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;

namespace HyperPdf.App.Tests;

/// <summary>Checks Search in Folder: results arrive, a result opens at its page, and the window is accessible.</summary>
public sealed class FolderSearchTests
{
    /// <summary>The pages in each test document.</summary>
    private const int Pages = 3;

    /// <summary>The documents searched.</summary>
    private const int Documents = 2;

    /// <summary>The page opened from the results, zero-based.</summary>
    private const int ThirdPage = 2;

    /// <summary>The words searched for.</summary>
    private const string Words = "brown fox";

    /// <summary>Every page of every PDF in the folder is searched and listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsEveryMatch()
    {
        using var test = new TestServices();
        _ = test.CreateDocument("one.pdf", Pages);
        _ = test.CreateDocument("two.pdf", Pages);
        using var main = new MainViewModel(test.Services);
        var search = main.FolderSearch;
        search.Folder = test.Directory;
        search.Query = Words;

        await search.SearchAsync();

        await Assert.That(search.Results.Count).IsEqualTo(Pages * Documents);
        await Assert.That(search.Results.All(static r => r.IsMatch && r.Snippet.Contains(Words, StringComparison.Ordinal))).IsTrue();
        await Assert.That(search.Status).StartsWith("6 matches in 2 files");
        await Assert.That(search.IsSearching).IsFalse();
    }

    /// <summary>Opening a result opens its file at its page with the words shown in the tab's search.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensAResultAtItsPage()
    {
        using var test = new TestServices();
        var path = test.CreateDocument("open.pdf", Pages);
        using var main = new MainViewModel(test.Services);

        main.OpenAt(path, ThirdPage, Words);
        var tab = main.SelectedTab!;

        await Assert.That(tab.FilePath).IsEqualTo(path);
        await Assert.That(tab.Search.Query).IsEqualTo(Words);
        await Assert.That(tab.Search.IsOpen).IsTrue();
        await Assert.That(await UiWait.UntilAsync(() => tab.CurrentPageIndex == ThirdPage)).IsTrue();
    }

    /// <summary>Nothing to search is said plainly, and an empty folder too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsWhenThereIsNothingToSearch()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var search = main.FolderSearch;
        search.Folder = test.Directory;

        await search.SearchAsync();
        var noWords = search.Status;
        search.Query = Words;
        await search.SearchAsync();

        await Assert.That(noWords).IsEqualTo("Choose a folder and the words to find.");
        await Assert.That(search.Status).IsEqualTo("There are no PDF files in this folder.");
    }

    /// <summary>Every control of the window has a name a screen reader can say.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowControlsHaveNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = new FolderSearchWindow { ViewModel = main.FolderSearch };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.IsVisible);

            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(window))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }
}

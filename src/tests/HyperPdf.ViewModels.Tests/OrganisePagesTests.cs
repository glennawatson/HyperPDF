// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Sidebar;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests the tools that organise pages: adding a blank page, reversing, replacing, saving each page, and choosing pages.</summary>
public sealed class OrganisePagesTests
{
    /// <summary>The pages of the starting document.</summary>
    private const int StartPages = 5;

    /// <summary>The pages of the file whose pages replace others.</summary>
    private const int ReplacementPages = 3;

    /// <summary>The index of the page that is chosen before a blank page is added after it.</summary>
    private const int ChosenPage = 1;

    /// <summary>The index of the third page, which is also where a blank page added after the second lands.</summary>
    private const int ThirdPage = 2;

    /// <summary>The third page alone.</summary>
    private static readonly int[] ThirdOnly = [ThirdPage];

    /// <summary>The first page and the third.</summary>
    private static readonly int[] FirstAndThird = [0, ThirdPage];

    /// <summary>Where the pages of the replacement file land.</summary>
    private static readonly int[] ReplacedPages = [1, 2, 3];

    /// <summary>The files saving each odd page makes.</summary>
    private static readonly string[] SplitFiles = ["split-page-1.pdf", "split-page-3.pdf", "split-page-5.pdf"];

    /// <summary>The odd pages of the starting document, as indexes.</summary>
    private static readonly int[] OddPages = [0, 2, 4];

    /// <summary>The even pages of the starting document, as indexes.</summary>
    private static readonly int[] EvenPages = [1, 3];

    /// <summary>Two pages next to each other, which are replaced or reversed.</summary>
    private static readonly int[] MiddlePages = [1, 2];

    /// <summary>A blank page goes after the chosen page, is chosen in its place, is as big as the page it follows, and undo takes it away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsABlankPageAfterTheChosenPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("blank.pdf", StartPages)]);
        var tab = main.SelectedTab!;
        tab.Pages.SelectPages([ChosenPage]);

        _ = await tab.Pages.InsertBlankPageCommand.Execute().ToTask();
        var afterAdd = tab.PageCount;
        var selected = tab.Pages.SelectedPages.ToArray();
        var sameSize = tab.Source.PageSizes[ThirdPage] == tab.Source.PageSizes[ChosenPage];
        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(afterAdd).IsEqualTo(StartPages + 1);
            await Assert.That(selected).IsEquivalentTo(ThirdOnly);
            await Assert.That(sameSize).IsTrue();
            await Assert.That(tab.PageCount).IsEqualTo(StartPages);
        }
    }

    /// <summary>Reversing needs two pages, keeps the same pages chosen, changes no page count and can be undone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReversesTheChosenPages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("reverse.pdf", StartPages)]);
        var tab = main.SelectedTab!;
        tab.Pages.SelectPages([0]);
        var canWithOne = ((ICommand)tab.Pages.ReverseCommand).CanExecute(null);
        tab.Pages.SelectPages(MiddlePages);

        _ = await tab.Pages.ReverseCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(canWithOne).IsFalse();
            await Assert.That(tab.PageCount).IsEqualTo(StartPages);
            await Assert.That(tab.Pages.SelectedPages).IsEquivalentTo(MiddlePages);
            await Assert.That(tab.Pages.CanUndo).IsTrue();
        }
    }

    /// <summary>The odd pages and the even pages are chosen by their numbers as a person counts them, from one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesTheOddAndTheEvenPages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("choose.pdf", StartPages)]);
        var pages = main.SelectedTab!.Pages;

        _ = await pages.SelectOddCommand.Execute().ToTask();
        var odd = pages.SelectedPages.ToArray();
        _ = await pages.SelectEvenCommand.Execute().ToTask();
        var even = pages.SelectedPages.ToArray();

        await Assert.That(odd).IsEquivalentTo(OddPages);
        await Assert.That(even).IsEquivalentTo(EvenPages);
    }

    /// <summary>The box under a page adds it to the chosen pages, a second press takes it out, and choosing only one drops the rest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TicksAndUnticksOnePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tick.pdf", StartPages)]);
        var pages = main.SelectedTab!.Pages;
        var third = new ThumbnailItemViewModel(ThirdPage, "3", 1, 1);
        pages.SelectPages([0]);

        _ = await pages.TogglePageCommand.Execute(third).ToTask();
        var added = pages.SelectedPages.ToArray();
        _ = await pages.TogglePageCommand.Execute(third).ToTask();
        var removed = pages.SelectedPages.ToArray();
        pages.SelectPages(OddPages);
        _ = await pages.SelectOnlyCommand.Execute(third).ToTask();

        await Assert.That(added).IsEquivalentTo(FirstAndThird);
        await Assert.That(removed).IsEquivalentTo([0]);
        await Assert.That(pages.SelectedPages).IsEquivalentTo(ThirdOnly);
    }

    /// <summary>Replacing takes the chosen pages out and puts the pages of a file where the first of them was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacesTheChosenPagesWithAFilesPages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("replace.pdf", StartPages)]);
        var replacement = test.CreateDocument("replacement.pdf", ReplacementPages);
        var tab = main.SelectedTab!;
        using var choose = tab.Pages.InsertInteraction.RegisterHandler(context => context.SetOutput([replacement]));
        tab.Pages.SelectPages(MiddlePages);

        _ = await tab.Pages.ReplaceCommand.Execute().ToTask();

        await Assert.That(tab.PageCount).IsEqualTo(StartPages - MiddlePages.Length + ReplacementPages);
        await Assert.That(tab.Pages.SelectedPages).IsEquivalentTo(ReplacedPages);
    }

    /// <summary>Each chosen page is saved as a file of its own, named with the page's number, and the open document is left as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesEachChosenPageAsItsOwnFile()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("split.pdf", StartPages)]);
        var folder = Path.Combine(test.Directory, "parts");
        _ = Directory.CreateDirectory(folder);
        var tab = main.SelectedTab!;
        using var choose = tab.Pages.SplitFolderInteraction.RegisterHandler(context => context.SetOutput(folder));
        tab.Pages.SelectPages(OddPages);

        _ = await tab.Pages.SplitPagesCommand.Execute().ToTask();
        var written = new List<string>();
        foreach (var file in new DirectoryInfo(folder).GetFiles())
        {
            written.Add(file.Name);
        }

        await Assert.That(written).IsEquivalentTo(SplitFiles);
        await Assert.That(tab.PageCount).IsEqualTo(StartPages);
    }

    /// <summary>Starting to organise shows the pages in the side panel and chooses the page being read; stopping leaves the panel alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrganisingShowsThePagesAndChoosesTheOneBeingRead()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("organise.pdf", StartPages)]);
        var tab = main.SelectedTab!;
        tab.SidebarVisible = false;
        tab.SidebarMode = SidebarMode.Outline;

        _ = await tab.ToggleOrganisePagesCommand.Execute().ToTask();
        var organising = tab.Pages.IsOrganising;
        var chosen = tab.Pages.SelectedPages.ToArray();
        _ = await tab.ToggleOrganisePagesCommand.Execute().ToTask();

        await Assert.That(organising).IsTrue();
        await Assert.That(tab.SidebarVisible).IsTrue();
        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Thumbnails);
        await Assert.That(chosen).IsEquivalentTo([0]);
        await Assert.That(tab.Pages.IsOrganising).IsFalse();
    }
}

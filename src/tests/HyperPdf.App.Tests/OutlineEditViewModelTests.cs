// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.Controls;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the bookmark editor adds, renames, moves and removes bookmarks, takes them back with undo, and leaves the reader where it is.</summary>
public sealed class OutlineEditViewModelTests
{
    /// <summary>The number of pages, and so of bookmarks, in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The index of the last page and bookmark.</summary>
    private const int LastIndex = Pages - 1;

    /// <summary>The number of bookmarks whose order a move test compares.</summary>
    private const int Pair = 2;

    /// <summary>The title typed for a new or renamed bookmark.</summary>
    private const string Typed = "Chapter Typed";

    /// <summary>Adds a bookmark to the page the reader is on and shows it in the list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsABookmarkForTheCurrentPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-add.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.ReportPosition(new(LastIndex, 0), LastIndex);
        using var answer = AnswerWith(tab, Typed);

        _ = await tab.OutlineEditing.AddCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Outline.Count).IsEqualTo(Pages + 1);
            await Assert.That(tab.Outline[Pages].Title).IsEqualTo(Typed);
            await Assert.That(tab.OutlineEditing.Status).IsEqualTo($"Added bookmark {Typed}.");
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(LastIndex);
        }
    }

    /// <summary>Does nothing when the title prompt is cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesTheBookmarksAloneWhenThePromptIsCancelled()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-cancel.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var answer = AnswerWith(tab, null);

        _ = await tab.OutlineEditing.AddCommand.Execute().ToTask();

        await Assert.That(tab.Outline.Count).IsEqualTo(Pages);
    }

    /// <summary>Renames the chosen bookmark and keeps it chosen, without moving the reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenamesTheChosenBookmarkWithoutMovingTheReader()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-rename.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var chosen = tab.Outline[LastIndex];
        var jumps = new List<NavigationRequest>();
        using var navigation = tab.Destinations.SubscribeSafe(jumps.Add, static _ => { });
        tab.SelectedOutlineItem = chosen;
        var jumpsToChoose = jumps.Count;
        using var answer = AnswerWith(tab, Typed);

        _ = await tab.OutlineEditing.RenameCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(jumpsToChoose).IsEqualTo(1);
            await Assert.That(tab.Outline[LastIndex].Title).IsEqualTo(Typed);
            await Assert.That(tab.SelectedOutlineItem?.Id).IsEqualTo(chosen.Id);
            await Assert.That(jumps.Count).IsEqualTo(jumpsToChoose);
        }
    }

    /// <summary>Moves, nests and un-nests the chosen bookmark.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesNestsAndUnnestsTheChosenBookmark()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-move.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var first = tab.Outline[0];
        var second = tab.Outline[1];
        tab.SelectedOutlineItem = first;
        var edit = tab.OutlineEditing;
        await Assert.That(edit.CanMoveUp).IsFalse();
        await Assert.That(edit.CanOutdent).IsFalse();

        _ = await edit.MoveDownCommand.Execute().ToTask();
        var afterDown = tab.Outline.Select(static item => item.Id).ToArray();
        _ = await edit.MoveUpCommand.Execute().ToTask();
        var afterUp = tab.Outline.Select(static item => item.Id).ToArray();
        tab.SelectedOutlineItem = tab.Outline[1];
        _ = await edit.IndentCommand.Execute().ToTask();
        var nested = tab.Outline[0].Children.Select(static item => item.Id).ToArray();
        var topLevelAfterNest = tab.Outline.Count;
        tab.SelectedOutlineItem = tab.Outline[0].Children[0];
        _ = await edit.OutdentCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(afterDown.Take(Pair).ToArray()).IsEquivalentTo([second.Id, first.Id]);
            await Assert.That(afterUp.Take(Pair).ToArray()).IsEquivalentTo([first.Id, second.Id]);
            await Assert.That(nested).IsEquivalentTo([second.Id]);
            await Assert.That(topLevelAfterNest).IsEqualTo(Pages - 1);
            await Assert.That(tab.Outline.Count).IsEqualTo(Pages);
            await Assert.That(tab.Outline[1].Id).IsEqualTo(second.Id);
        }
    }

    /// <summary>Removes the chosen bookmark, and undo puts it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesTheChosenBookmarkAndUndoPutsItBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-remove.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var removed = tab.Outline[1];
        var jumps = new List<NavigationRequest>();
        using var navigation = tab.Destinations.SubscribeSafe(jumps.Add, static _ => { });
        tab.SelectedOutlineItem = removed;
        var jumpsToChoose = jumps.Count;

        _ = await tab.OutlineEditing.RemoveCommand.Execute().ToTask();
        var afterRemove = tab.Outline.Count;
        var jumpsAfterRemove = jumps.Count;
        await Assert.That(tab.OutlineEditing.CanChange).IsFalse();

        _ = await tab.Pages.UndoCommand.Execute().ToTask();
        _ = await UiWait.UntilAsync(() => tab.Outline.Count == Pages);

        using (Assert.Multiple())
        {
            await Assert.That(afterRemove).IsEqualTo(Pages - 1);
            await Assert.That(jumpsAfterRemove).IsEqualTo(jumpsToChoose);
            await Assert.That(tab.Outline.Any(item => item.Id == removed.Id)).IsTrue();
        }
    }

    /// <summary>Answers the tab's title prompt with a fixed title.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="title">The answer; <see langword="null"/> cancels.</param>
    /// <returns>The registration; disposing it removes the answer.</returns>
    private static IDisposable AnswerWith(DocumentTabViewModel tab, string? title) =>
        tab.OutlineEditing.PromptInteraction.RegisterHandler(context => context.SetOutput(title));
}

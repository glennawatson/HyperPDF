// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>
/// Checks that a window following the controls hears of a change to a tab it is not showing, and of a tab closing. Each tab is
/// followed once for as long as it is open, so these are the changes a tab that was followed before the others opened must still report.
/// </summary>
public sealed class TabNameWatchTests
{
    /// <summary>The tabs left open after one of three closes.</summary>
    private const int TabsLeft = 2;

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>A tab that is not the one shown tells the window when it gains unsaved changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsavedMarkOfATabNotShownIsReported()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", 1), test.CreateDocument("second.pdf", 1)]);
        var hidden = await ShowAnotherTabAsync(main);
        var changes = 0;
        using var following = main.ControlChanges.SubscribeSafe(_ => Interlocked.Increment(ref changes), static _ => { });
        using var prompt = hidden.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("A note"));
        var before = Volatile.Read(ref changes);

        _ = await hidden.Annotations.AddNote(0, NoteAt).ToTask();

        _ = await TestWait.UntilAsync(() => hidden.HasUnsavedChanges && Volatile.Read(ref changes) > before, () => $"unsaved {hidden.HasUnsavedChanges}, changes {changes} after {before}.");
        await Assert.That(hidden.HasUnsavedChanges).IsTrue();
        await Assert.That(Volatile.Read(ref changes)).IsGreaterThan(before);
    }

    /// <summary>Closing a tab tells the window, and the tabs left open go on telling it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingATabIsReportedAndTheRestAreStillFollowed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", 1), test.CreateDocument("second.pdf", 1), test.CreateDocument("third.pdf", 1)]);
        var changes = 0;
        using var following = main.ControlChanges.SubscribeSafe(_ => Interlocked.Increment(ref changes), static _ => { });
        var before = Volatile.Read(ref changes);

        main.CloseTabWithoutAsking(main.Tabs.First(tab => !ReferenceEquals(tab, main.SelectedTab)));

        _ = await TestWait.UntilAsync(() => Volatile.Read(ref changes) > before, () => $"changes {changes} after {before}.");
        var afterClose = Volatile.Read(ref changes);
        var hidden = await ShowAnotherTabAsync(main);
        using var prompt = hidden.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("A note"));
        var beforeNote = Volatile.Read(ref changes);
        _ = await hidden.Annotations.AddNote(0, NoteAt).ToTask();
        _ = await TestWait.UntilAsync(() => hidden.HasUnsavedChanges && Volatile.Read(ref changes) > beforeNote, () => $"unsaved {hidden.HasUnsavedChanges}, changes {changes} after {beforeNote}.");

        await Assert.That(afterClose).IsGreaterThan(before);
        await Assert.That(main.Tabs.Count).IsEqualTo(TabsLeft);
        await Assert.That(Volatile.Read(ref changes)).IsGreaterThan(beforeNote);
    }

    /// <summary>Shows another tab and waits for it to load, which leaves the tab shown before loaded and out of view. A tab loads when it is first shown.</summary>
    /// <param name="main">The viewer.</param>
    /// <returns>The tab that was shown before.</returns>
    private static async Task<DocumentTabViewModel> ShowAnotherTabAsync(MainViewModel main)
    {
        var hidden = main.SelectedTab!;
        var shown = main.Tabs.First(tab => !ReferenceEquals(tab, hidden));
        main.SelectedTab = shown;
        _ = await TestWait.UntilAsync(() => shown.IsLoaded, () => shown.ErrorMessage ?? "The tab shown has not loaded.");
        return hidden;
    }
}

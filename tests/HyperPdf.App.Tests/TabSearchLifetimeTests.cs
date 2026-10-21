// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks a tab's find-in-document search across tab switches and typing.</summary>
public sealed class TabSearchLifetimeTests
{
    /// <summary>The pages of the searched document.</summary>
    private const int Pages = 3;

    /// <summary>The words every generated page contains.</summary>
    private const string Words = "brown fox";

    /// <summary>The rounds the dispatcher runs so a search that was released has finished.</summary>
    private const int SettleRounds = 25;

    /// <summary>A search a tab switch stopped half way starts again when the tab is selected, instead of staying partial.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchStoppedByATabSwitchRunsAgainWhenTheTabIsSelected()
    {
        var engine = new PausingEngine();
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("first.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var gate = engine.Arm();
        tab.Search.Query = Words;
        _ = await UiWait.UntilAsync(() => gate.Reached.Task.IsCompleted, static () => "The search did not start.");

        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("second.pdf", 1)]);
        engine.Disarm();
        gate.Release();

        // The stopped search must have ended before the tab is selected again, or the tab would resume a live search.
        _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching, () => tab.Search.Status);
        main.SelectedTab = tab;

        _ = await UiWait.UntilAsync(() => tab.Search.Results.Count == Pages && !tab.Search.IsSearching, () => $"{tab.Search.Status}; {tab.Search.Results.Count} results");
        await Assert.That(tab.Search.Status).EndsWith($"of {Pages}");
    }

    /// <summary>An older search that finishes after the query changed leaves the newer search's busy flag and status alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OlderSearchFinishingLateLeavesTheNewerSearchRunning()
    {
        var engine = new PausingEngine();
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("late.pdf", 1)]);
        var tab = main.SelectedTab!;
        var older = engine.ArmSearch();
        tab.Search.Query = "zebra";
        _ = await UiWait.UntilAsync(() => older.Reached.Task.IsCompleted, static () => "The older search did not start.");

        // The older search is now inside its last page's text work, past the point where cancelling stops it.
        var newer = engine.Arm();
        tab.Search.Query = Words;
        _ = await UiWait.UntilAsync(() => newer.Reached.Task.IsCompleted, static () => "The newer search did not start.");

        older.Release();
        await older.Found.Task;
        await UiWait.PumpAsync(SettleRounds);
        var searchingWhileNewerIsHeld = tab.Search.IsSearching;
        var statusWhileNewerIsHeld = tab.Search.Status;
        engine.Disarm();
        newer.Release();

        _ = await UiWait.UntilAsync(() => tab.Search.Results.Count == 1 && !tab.Search.IsSearching, () => tab.Search.Status);
        using (Assert.Multiple())
        {
            await Assert.That(searchingWhileNewerIsHeld).IsTrue();
            await Assert.That(statusWhileNewerIsHeld).IsEqualTo("Searching…");
        }
    }
}

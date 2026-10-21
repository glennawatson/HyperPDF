// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the open actions of every page that comes into view run, even while an earlier page's actions are still running.</summary>
public sealed class FormPageOpenedTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 3;

    /// <summary>The first page shown after the one the document opens at.</summary>
    private const int FirstPage = 1;

    /// <summary>The page shown next.</summary>
    private const int SecondPage = 2;

    /// <summary>Pages that report no actions queue no open or close run, and a page turn that follows still runs once they have some.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesWithoutActionsRunNothing()
    {
        var engine = new PausingEngine { ExposesFeatures = true, PagesHaveActions = false };
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages-quiet.pdf", Pages)]);
        var tab = main.SelectedTab!;

        tab.ReportPosition(new(FirstPage, 0), FirstPage);
        tab.ReportPosition(new(SecondPage, 0), SecondPage);
        await UiWait.PumpAsync(1);
        await Assert.That(engine.PageActions.IsEmpty).IsTrue();

        engine.PagesHaveActions = true;
        tab.ReportPosition(new(FirstPage, 0), FirstPage);
        _ = await UiWait.UntilAsync(() => engine.OpenedPages.Count == 1, static () => "The page's open actions did not run.");
        await Assert.That(engine.PageActions.ToArray().Select(static action => action.Page).ToArray()).IsEquivalentTo([SecondPage, FirstPage]);
    }

    /// <summary>The page that opens while the first page's actions still run waits its turn and then runs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageOpenedWhileAnotherRunsStillRuns()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages-opened.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.FormGate = gate;

        // The forms follow the page shown, which the pages report as the reader scrolls.
        tab.ReportPosition(new(FirstPage, 0), FirstPage);
        _ = await UiWait.UntilAsync(() => engine.OpenedPages.Count == 1, static () => "The first page's actions did not start.");
        tab.ReportPosition(new(SecondPage, 0), SecondPage);
        await UiWait.PumpAsync(1);
        _ = gate.TrySetResult();

        _ = await UiWait.UntilAsync(() => engine.OpenedPages.Count == Pages - 1, () => $"Pages run: {string.Join(',', engine.OpenedPages)}.");
        await Assert.That(engine.OpenedPages.ToArray()).IsEquivalentTo([FirstPage, SecondPage]);
    }
}

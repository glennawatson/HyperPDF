// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Navigation;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the tab's drag tools, auto-scroll speed and select all requests without a window.</summary>
public sealed class PageToolViewModelTests
{
    /// <summary>Verifies choosing a tool sets exactly one tool flag, and Snapshot toggles back to selecting text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesOneToolAtATime()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tools.pdf", 1)]);
        var tab = main.SelectedTab!;
        var start = tab.IsSelectTextTool;

        _ = await tab.SetPageToolCommand.Execute(PageTool.Hand).ToTask();
        var hand = (tab.IsHandTool, tab.IsSelectTextTool, tab.IsZoomAreaTool, tab.IsSnapshotTool);
        _ = await tab.ToggleSnapshotToolCommand.Execute().ToTask();
        var snapshot = tab.IsSnapshotTool;
        _ = await tab.ToggleSnapshotToolCommand.Execute().ToTask();

        await Assert.That(start).IsTrue();
        await Assert.That(hand).IsEqualTo((true, false, false, false));
        await Assert.That(snapshot).IsTrue();
        await Assert.That(tab.PageTool).IsEqualTo(PageTool.SelectText);
    }

    /// <summary>Verifies the auto-scroll speed stays within its limits and its text tells the speed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoScrollSpeedStaysInRange()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("speed.pdf", 1)]);
        var tab = main.SelectedTab!;

        for (var i = 0; i < AutoScroller.MaxSpeed; i++)
        {
            _ = await tab.FasterAutoScrollCommand.Execute().ToTask();
        }

        var fastest = tab.AutoScrollSpeed;
        var beyond = tab.ChangeAutoScrollSpeed(1);
        for (var i = 0; i < AutoScroller.MaxSpeed; i++)
        {
            _ = await tab.SlowerAutoScrollCommand.Execute().ToTask();
        }

        _ = await tab.ToggleAutoScrollCommand.Execute().ToTask();
        var on = tab.IsAutoScrolling;
        _ = await tab.StopAutoScrollCommand.Execute().ToTask();

        await Assert.That(fastest).IsEqualTo(AutoScroller.MaxSpeed);
        await Assert.That(beyond).IsFalse();
        await Assert.That(tab.AutoScrollSpeed).IsEqualTo(AutoScroller.MinSpeed);
        await Assert.That(tab.AutoScrollText).Contains($"speed {AutoScroller.MinSpeed} of {AutoScroller.MaxSpeed}");
        await Assert.That(on).IsTrue();
        await Assert.That(tab.IsAutoScrolling).IsFalse();
    }

    /// <summary>Verifies Select All asks the view to select the current page's text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectAllAsksTheView()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("select.pdf", 1)]);
        var tab = main.SelectedTab!;
        var requests = 0;
        using var watch = tab.SelectAllRequests.SubscribeSafe(_ => requests++, static _ => { });

        _ = await tab.SelectAllCommand.Execute().ToTask();

        await Assert.That(requests).IsEqualTo(1);
    }
}

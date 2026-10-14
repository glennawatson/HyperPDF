// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Navigation;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

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
        var start = tab.PageTools.IsSelectTextTool;

        _ = await tab.PageTools.SetPageToolCommand.Execute(PageTool.Hand).ToTask();
        var hand = (tab.PageTools.IsHandTool, tab.PageTools.IsSelectTextTool, tab.PageTools.IsZoomAreaTool, tab.PageTools.IsSnapshotTool);
        _ = await tab.PageTools.ToggleSnapshotToolCommand.Execute().ToTask();
        var snapshot = tab.PageTools.IsSnapshotTool;
        _ = await tab.PageTools.ToggleSnapshotToolCommand.Execute().ToTask();

        await Assert.That(start).IsTrue();
        await Assert.That(hand).IsEqualTo((true, false, false, false));
        await Assert.That(snapshot).IsTrue();
        await Assert.That(tab.PageTools.PageTool).IsEqualTo(PageTool.SelectText);
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
            _ = await tab.PageTools.FasterAutoScrollCommand.Execute().ToTask();
        }

        var fastest = tab.PageTools.AutoScrollSpeed;
        var beyond = tab.PageTools.ChangeAutoScrollSpeed(1);
        for (var i = 0; i < AutoScroller.MaxSpeed; i++)
        {
            _ = await tab.PageTools.SlowerAutoScrollCommand.Execute().ToTask();
        }

        _ = await tab.PageTools.ToggleAutoScrollCommand.Execute().ToTask();
        var on = tab.PageTools.IsAutoScrolling;
        _ = await tab.PageTools.StopAutoScrollCommand.Execute().ToTask();

        await Assert.That(fastest).IsEqualTo(AutoScroller.MaxSpeed);
        await Assert.That(beyond).IsFalse();
        await Assert.That(tab.PageTools.AutoScrollSpeed).IsEqualTo(AutoScroller.MinSpeed);
        await Assert.That(tab.PageTools.AutoScrollText).Contains($"speed {AutoScroller.MinSpeed} of {AutoScroller.MaxSpeed}");
        await Assert.That(on).IsTrue();
        await Assert.That(tab.PageTools.IsAutoScrolling).IsFalse();
    }
}

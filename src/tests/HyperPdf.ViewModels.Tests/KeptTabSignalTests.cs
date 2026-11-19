// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>
/// Checks the signals a tab keeps connected for the window: the same signal is given each time, a follower that comes later
/// still hears changes, the controls signal starts with a value for the tab as it is, and a tab left long enough makes them again.
/// </summary>
public sealed class KeptTabSignalTests
{
    /// <summary>The pages of each document.</summary>
    private const int Pages = 3;

    /// <summary>How many times larger the zoom is made.</summary>
    private const double Enlarged = 2;

    /// <summary>How many times a signal is made when it is made again after the tab was left.</summary>
    private const int MadeTwice = 2;

    /// <summary>Names the signal the tests keep.</summary>
    private static readonly object ZoomKey = new();

    /// <summary>A kept signal is made once: asking again gives the same one, and a second follower hears a change the first also hears.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptSignalIsMadeOnceAndSharedByItsFollowers()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("kept-shared.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var made = 0;
        IObservable<double> Keep() => tab.KeepSignal(ZoomKey, shown =>
        {
            made++;
            return shown.WhenChanged(static t => t.Zoom).Skip(1).Publish();
        });

        var first = Keep();
        var second = Keep();
        var heardByFirst = 0;
        var heardBySecond = 0;
        using (first.Subscribe(_ => heardByFirst++))
        using (second.Subscribe(_ => heardBySecond++))
        {
            tab.SetZoom(tab.Zoom * Enlarged);
        }

        await Assert.That(made).IsEqualTo(1);
        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(heardByFirst).IsEqualTo(1);
        await Assert.That(heardBySecond).IsEqualTo(1);
    }

    /// <summary>The controls signal gives one value at once for the tab as it is, then one for each change, also to a follower that comes after an earlier one left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlChangesStartWithTheTabAsItIsForEveryFollower()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("kept-controls.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var early = 0;
        tab.ControlChanges.Subscribe(_ => early++).Dispose();

        var atStart = 0;
        var afterChange = 0;
        using (tab.ControlChanges.Subscribe(_ => afterChange++))
        {
            atStart = afterChange;
            tab.SidebarVisible = !tab.SidebarVisible;
        }

        await Assert.That(early).IsEqualTo(1);
        await Assert.That(atStart).IsEqualTo(1);
        await Assert.That(afterChange).IsGreaterThan(atStart);
    }

    /// <summary>A tab left long enough ends its kept signals, and makes them again when it is next asked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ATabLeftLongEnoughMakesItsSignalsAgain()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("kept-left.pdf", Pages), test.CreateDocument("kept-shown.pdf", Pages)]);
        var left = main.SelectedTab!;
        var made = 0;
        IObservable<double> Keep() => left.KeepSignal(ZoomKey, shown =>
        {
            made++;
            return shown.WhenChanged(static t => t.Zoom).Skip(1).Publish();
        });

        var before = Keep();
        main.SelectedTab = main.Tabs[0];
        left.ReleaseIdleMemory(left.LeftAt);
        var after = Keep();
        var heard = 0;
        using (after.Subscribe(_ => heard++))
        {
            left.SetZoom(left.Zoom * Enlarged);
        }

        await Assert.That(made).IsEqualTo(MadeTwice);
        await Assert.That(after).IsNotSameReferenceAs(before);
        await Assert.That(heard).IsEqualTo(1);
    }
}

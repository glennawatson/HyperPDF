// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks which tabs let go of what they keep only to draw pages faster: the ones left unshown long enough, once.</summary>
public sealed class IdleTabMemoryTests
{
    /// <summary>The pages of each document.</summary>
    private const int Pages = 2;

    /// <summary>A tab that is shown records when it is left, and the tab shown in its place is not marked as left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavingATabRecordsWhenItWasLeft()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("idle-first.pdf", Pages), test.CreateDocument("idle-second.pdf", Pages)]);
        var shown = main.SelectedTab!;
        var other = main.Tabs[0];
        var beforeLeaving = shown.LeftAt;

        main.SelectedTab = other;

        await Assert.That(beforeLeaving).IsEqualTo(0);
        await Assert.That(shown.LeftAt).IsNotEqualTo(0);
        await Assert.That(other.IsSelected).IsTrue();
    }

    /// <summary>A tab left before the moment asked about lets go once; one left after it, and the tab shown, keep what they have.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyATabLeftLongEnoughLetsGo()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("idle-left.pdf", Pages), test.CreateDocument("idle-shown.pdf", Pages)]);
        var left = main.SelectedTab!;
        main.SelectedTab = main.Tabs[0];
        var shown = main.SelectedTab!;
        var leftAt = left.LeftAt;

        left.ReleaseIdleMemory(leftAt - 1);
        var afterTooSoon = left.LeftAt;
        left.ReleaseIdleMemory(leftAt);
        var afterLongEnough = left.LeftAt;
        shown.ReleaseIdleMemory(long.MaxValue);

        await Assert.That(afterTooSoon).IsEqualTo(leftAt);
        await Assert.That(afterLongEnough).IsEqualTo(0);
        await Assert.That(shown.IsSelected).IsTrue();
        await Assert.That(DocumentFeature<IIdleRenderMemory>.Get(left.TryGetDocument())).IsNotNull();
    }
}

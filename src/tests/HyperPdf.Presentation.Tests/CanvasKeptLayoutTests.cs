// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks that a canvas coming back to a tab uses the layout it made for that tab before, and makes a new one when the pages change.</summary>
public sealed class CanvasKeptLayoutTests
{
    /// <summary>The pages of the first document.</summary>
    private const int FirstPages = 3;

    /// <summary>How many times larger the zoom is made.</summary>
    private const double Enlarged = 2;

    /// <summary>The pages of the second document.</summary>
    private const int SecondPages = 5;

    /// <summary>Showing another tab and coming back gives the layout made the first time, not a new one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ComingBackToATabUsesTheLayoutMadeBefore()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("kept-first.pdf", FirstPages), test.CreateDocument("kept-second.pdf", SecondPages)]);
        var first = main.SelectedTab!;
        var second = main.Tabs[0];
        var (_, state) = CanvasUnderTest.Create(first);
        state.RebuildLayout();
        var made = state.Layout;

        state.Tab = second;
        state.RebuildLayout();
        var other = state.Layout;
        state.Tab = first;
        state.RebuildLayout();

        await Assert.That(made.PageCount).IsEqualTo(SecondPages);
        await Assert.That(other).IsNotSameReferenceAs(made);
        await Assert.That(state.Layout).IsSameReferenceAs(made);
    }

    /// <summary>A layout kept for a tab is not used after the tab's zoom changed while another tab was shown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptLayoutIsNotUsedAfterTheZoomChanged()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("zoom-first.pdf", FirstPages), test.CreateDocument("zoom-second.pdf", SecondPages)]);
        var first = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(first);
        state.RebuildLayout();
        var made = state.Layout;

        state.Tab = main.Tabs[0];
        state.RebuildLayout();
        first.SetZoom(first.Zoom * Enlarged);
        state.Tab = first;
        state.RebuildLayout();

        await Assert.That(state.Layout).IsNotSameReferenceAs(made);
        await Assert.That(state.Layout.Options.Scale).IsGreaterThan(made.Options.Scale);
    }

    /// <summary>Pages that changed are laid out again, even when the options are the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesThatChangedAreLaidOutAgain()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("changed.pdf", FirstPages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();
        var made = state.Layout;

        state.RelayoutAfterPagesChange();

        await Assert.That(state.Layout).IsNotSameReferenceAs(made);
        await Assert.That(state.Layout.PageCount).IsEqualTo(FirstPages);
    }
}

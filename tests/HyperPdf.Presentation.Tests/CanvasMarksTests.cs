// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks the marks over the pages: the sentence being read aloud and the focus band, through a painter with no toolkit behind it.</summary>
public sealed class CanvasMarksTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The width of the viewport.</summary>
    private const double ViewWidth = 800;

    /// <summary>The height of the viewport.</summary>
    private const double ViewHeight = 600;

    /// <summary>With nothing searched for or read, nothing is marked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingIsMarkedWithoutHitsOrReading()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("marks-none.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeMarksPainter();

        state.DrawMarks(painter, tab, View());

        await Assert.That(painter.Marks).IsEqualTo(0);
    }

    /// <summary>Search hits in view are drawn, and the current one is told apart.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchHitsAreDrawnAndTheCurrentOneIsTold()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("marks-hits.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeMarksPainter();

        tab.Search.Open();
        tab.Search.Query = "quick brown";
        var found = await TestWait.UntilAsync(() => tab.Search.Results.Count > 0 && tab.Search.HasHits);
        state.DrawMarks(painter, tab, View());

        await Assert.That(found).IsTrue();
        await Assert.That(painter.Hits).IsGreaterThan(0);
        await Assert.That(painter.CurrentHits).IsLessThanOrEqualTo(painter.Hits);
    }

    /// <summary>The sentence being read is filled and its word underlined, on the page it is on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpokenSentenceIsFilledOnItsPage()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("marks-spoken.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var painter = new FakeMarksPainter();

        tab.ReadAloud.IsOpen = true;
        var marked = await TestWait.UntilAsync(() => tab.ReadAloud.SpokenBounds.Count > 0);
        state.DrawMarks(painter, tab, View());

        await Assert.That(marked).IsTrue();
        await Assert.That(painter.Spoken).IsGreaterThan(0);
        tab.ReadAloud.IsOpen = false;
    }

    /// <summary>The focus band dims the other pages and the parts of the page above and below the sentence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusBandDimsAwayFromTheSentence()
    {
        var speech = new FakeSpeech(true, true);
        using var test = new TestServices(new FallbackPlatform(), speech);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("marks-band.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.FocusMode.FocusBand = true;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var dimming = new FakeMarksPainter();
        var bare = new FakeMarksPainter { HasDim = false };

        tab.ReadAloud.IsOpen = true;
        var marked = await TestWait.UntilAsync(() => tab.ReadAloud.SpokenBounds.Count > 0);
        state.DrawMarks(dimming, tab, View());
        state.DrawMarks(bare, tab, View());

        await Assert.That(marked).IsTrue();
        await Assert.That(dimming.Dims).IsGreaterThan(0);
        await Assert.That(bare.Dims).IsEqualTo(0);
        tab.ReadAloud.IsOpen = false;
    }

    /// <summary>Makes the part of the canvas in view.</summary>
    /// <returns>The viewport.</returns>
    private static LayoutRect View() => new(0, 0, ViewWidth, ViewHeight);
}

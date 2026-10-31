// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Tests;
using ReactiveUI.Primitives;

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

    /// <summary>In Edit mode the picked object is outlined as picked, and the page is not marked otherwise.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditModeOutlinesThePickedObject()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("marks-edit.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var before = new FakeMarksPainter();
        var after = new FakeMarksPainter();
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var target = Array.Find(await editor.ReadObjectsAsync(0, CancellationToken.None), static o => o.Kind == PageObjectKind.Text);

        state.DrawMarks(before, tab, View());
        _ = await tab.ContentEdit.ToggleEditingCommand.Execute().ToTask();
        await tab.ContentEdit.PressAsync(0, new(target.Bounds.Left + 1, target.Bounds.Top + 1), false);
        state.DrawMarks(after, tab, View());

        using (Assert.Multiple())
        {
            await Assert.That(before.EditOutlines).IsEqualTo(0);
            await Assert.That(after.PickedOutlines).IsEqualTo(1);
        }
    }

    /// <summary>Delete in Edit mode removes the picked object, and the key is used.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteKeyRemovesThePickedObjectInEditMode()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keys-edit.pdf", TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var target = Array.Find(await editor.ReadObjectsAsync(0, CancellationToken.None), static o => o.Kind == PageObjectKind.Text);
        _ = await tab.ContentEdit.ToggleEditingCommand.Execute().ToTask();
        await tab.ContentEdit.PressAsync(0, new(target.Bounds.Left + 1, target.Bounds.Top + 1), false);

        var effects = state.PressKey(ViewerKey.Delete, ViewerModifiers.None);
        var written = await TestWait.UntilAsync(() => tab.ContentEdit.Selected is null && tab.ContentEdit.Status == "Removed.");
        var after = await editor.ReadObjectsAsync(0, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(effects.Handled).IsTrue();
            await Assert.That(written).IsTrue();
            await Assert.That(Array.Exists(after, item => item.Text == target.Text)).IsFalse();
        }
    }

    /// <summary>Makes the part of the canvas in view.</summary>
    /// <returns>The viewport.</returns>
    private static LayoutRect View() => new(0, 0, ViewWidth, ViewHeight);
}

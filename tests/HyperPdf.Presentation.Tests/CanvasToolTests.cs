// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks panning, holding Space, dragging an area and auto-scrolling through a host with no toolkit behind it.</summary>
public sealed class CanvasToolTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>How far the test drags the pointer.</summary>
    private const double Drag = 40;

    /// <summary>How far a test auto-scrolls.</summary>
    private const double Glide = 25;

    /// <summary>A pointer travel that is still a click.</summary>
    private const double Wobble = 1;

    /// <summary>A pointer travel that is a drag.</summary>
    private const double Travel = 10;

    /// <summary>Where the area test starts dragging.</summary>
    private const double AreaFrom = 50;

    /// <summary>Where the area test stops dragging.</summary>
    private const double AreaTo = 20;

    /// <summary>The pointer moving the way the pages go moves the view the other way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PanMovesTheViewAgainstThePointer()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-pan.pdf", Pages)]);
        var (host, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();
        host.ScrollOffset = new(Drag, Drag);

        state.BeginPan(new(Travel, Travel));
        var used = state.ContinuePan(new(Travel + Drag, Travel));

        await Assert.That(used).IsTrue();
        await Assert.That(host.ScrollOffset.X).IsEqualTo(0);
        await Assert.That(host.ScrollOffset.Y).IsEqualTo(Drag);
    }

    /// <summary>Without a pan under way the pointer does not move the view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PointerMovesNothingWithoutAPan()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-nopan.pdf", Pages)]);
        var (host, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        await Assert.That(state.ContinuePan(new(Drag, Drag))).IsFalse();
        await Assert.That(host.ScrollOffset.X).IsEqualTo(0);
    }

    /// <summary>Space starts a hold once, however long the key repeats, and a pan during it stops the page turn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpaceStartsOneHoldAndAPanCancelsThePageTurn()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-space.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.RebuildLayout();

        var first = state.PressSpace(ViewerKey.Space, ViewerModifiers.None, out var firstStarted);
        var repeat = state.PressSpace(ViewerKey.Space, ViewerModifiers.None, out var repeatStarted);
        state.BeginPan(new(0, 0));
        state.Tools.Panning = false;

        await Assert.That(first).IsTrue();
        await Assert.That(firstStarted).IsTrue();
        await Assert.That(repeat).IsTrue();
        await Assert.That(repeatStarted).IsFalse();
        await Assert.That(state.EndSpaceHold()).IsFalse();
    }

    /// <summary>Letting go of Space without moving the pages turns the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpaceWithoutAPanTurnsThePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-turn.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);

        _ = state.PressSpace(ViewerKey.Space, ViewerModifiers.None, out _);

        await Assert.That(state.EndSpaceHold()).IsTrue();
        await Assert.That(state.Tools.SpaceHeld).IsFalse();
    }

    /// <summary>Other keys and other modifiers are not Space holds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlySpaceWithShiftAtMostIsAHold()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-keys.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);

        await Assert.That(state.PressSpace(ViewerKey.Enter, ViewerModifiers.None, out _)).IsFalse();
        await Assert.That(state.PressSpace(ViewerKey.Space, ViewerModifiers.Control, out _)).IsFalse();
        await Assert.That(state.PressSpace(ViewerKey.Space, ViewerModifiers.Shift, out _)).IsTrue();
    }

    /// <summary>Losing the keyboard forgets the hold.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LosingTheKeyboardForgetsTheHold()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-release.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        _ = state.PressSpace(ViewerKey.Space, ViewerModifiers.None, out _);

        state.ReleaseSpace();

        await Assert.That(state.Tools.SpaceHeld).IsFalse();
        await Assert.That(state.Tools.SpacePanned).IsFalse();
    }

    /// <summary>The area is the same whichever way it was dragged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AreaIsTheSameWhicheverWayItWasDragged()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-area.pdf", Pages)]);
        var (_, state) = CanvasUnderTest.Create(main.SelectedTab!);
        state.Tools.AreaStart = new(AreaFrom, AreaFrom);
        state.Tools.AreaEnd = new(AreaTo, AreaTo);

        var area = state.GetArea();

        await Assert.That(area.X).IsEqualTo(AreaTo);
        await Assert.That(area.Y).IsEqualTo(AreaTo);
        await Assert.That(area.Width).IsEqualTo(AreaFrom - AreaTo);
        await Assert.That(area.Height).IsEqualTo(AreaFrom - AreaTo);
    }

    /// <summary>A small wobble is a click and a longer travel is a drag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClicksStayCloseToWhereTheyStarted()
    {
        await Assert.That(CanvasMetrics.IsClick(new(0, 0), new(Wobble, Wobble))).IsTrue();
        await Assert.That(CanvasMetrics.IsClick(new(0, 0), new(Travel, 0))).IsFalse();
    }

    /// <summary>Auto-scroll moves down and stops at the end of the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoScrollMovesDownAndStopsAtTheEnd()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tool-glide.pdf", Pages)]);
        var tab = main.SelectedTab!;
        tab.IsPageByPage = false;
        var (host, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        host.ExtentSize = new(CanvasUnderTest.ViewWidth, state.Layout.ExtentHeight);

        var moved = state.AutoScrollBy(Glide);
        var first = host.ScrollOffset.Y;
        host.ScrollOffset = new(0, host.ExtentSize.Height - host.ViewportSize.Height);
        var atEnd = state.AutoScrollBy(Glide);

        await Assert.That(moved).IsTrue();
        await Assert.That(first).IsEqualTo(Glide);
        await Assert.That(atEnd).IsFalse();
    }
}

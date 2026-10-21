// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks placing the corners of a polygon before it is finished.</summary>
public sealed class PolygonDraftTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>The page the corners are placed on.</summary>
    private const int OnPage = 1;

    /// <summary>The corners a triangle needs.</summary>
    private const int Triangle = 3;

    /// <summary>The first corner.</summary>
    private static readonly PagePoint First = new(100, 100);

    /// <summary>The second corner.</summary>
    private static readonly PagePoint Second = new(200, 100);

    /// <summary>The third corner.</summary>
    private static readonly PagePoint Third = new(150, 200);

    /// <summary>A press places a corner and starts the draft on that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstPressStartsTheDraft()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-first.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        var draft = new PolygonDraft();

        var changed = draft.Press(annotations, OnPage, First, false);

        await Assert.That(changed).IsTrue();
        await Assert.That(draft.IsActive).IsTrue();
        await Assert.That(draft.Page).IsEqualTo(OnPage);
        await Assert.That(draft.Corners.Count).IsEqualTo(1);
        await Assert.That(draft.Hover).IsEqualTo(First);
    }

    /// <summary>A press on another page, or on no page, changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressOnAnotherPageIsIgnored()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-page.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        var draft = new PolygonDraft();
        _ = draft.Press(annotations, OnPage, First, false);

        var elsewhere = draft.Press(annotations, 0, Second, false);
        var nowhere = draft.Press(annotations, -1, Second, false);

        await Assert.That(elsewhere).IsFalse();
        await Assert.That(nowhere).IsFalse();
        await Assert.That(draft.Corners.Count).IsEqualTo(1);
    }

    /// <summary>The free end follows the pointer only while a draft is under way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoverFollowsThePointerWhileDrafting()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-hover.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        var draft = new PolygonDraft();

        var idle = draft.MoveHover(Second);
        _ = draft.Press(annotations, OnPage, First, false);
        var moved = draft.MoveHover(Second);

        await Assert.That(idle).IsFalse();
        await Assert.That(moved).IsTrue();
        await Assert.That(draft.Hover).IsEqualTo(Second);
    }

    /// <summary>Backspace takes back a corner, and taking back the last one ends the draft.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackspaceTakesBackCorners()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-back.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        var draft = new PolygonDraft();
        _ = draft.Press(annotations, OnPage, First, false);
        _ = draft.Press(annotations, OnPage, Second, false);

        var first = draft.HandleKey(annotations, ViewerKey.Back);
        var afterFirst = draft.Corners.Count;
        var second = draft.HandleKey(annotations, ViewerKey.Back);

        await Assert.That(first).IsTrue();
        await Assert.That(afterFirst).IsEqualTo(1);
        await Assert.That(second).IsTrue();
        await Assert.That(draft.IsActive).IsFalse();
    }

    /// <summary>Escape forgets every corner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EscapeStartsAgain()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-escape.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        var draft = new PolygonDraft();
        _ = draft.Press(annotations, OnPage, First, false);

        var used = draft.HandleKey(annotations, ViewerKey.Escape);

        await Assert.That(used).IsTrue();
        await Assert.That(draft.IsActive).IsFalse();
        await Assert.That(draft.Corners.Count).IsEqualTo(0);
    }

    /// <summary>Keys do nothing when no corner is placed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeysNeedACorner()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-idle.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        var draft = new PolygonDraft();

        await Assert.That(draft.HandleKey(annotations, ViewerKey.Enter)).IsFalse();
        await Assert.That(draft.Cancel()).IsFalse();
        await Assert.That(draft.Finish(annotations)).IsFalse();
    }

    /// <summary>Enter finishes the polygon, adds it to the page and starts again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EnterAddsThePolygon()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-finish.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        annotations.Tool = AnnotationTool.Polygon;
        var draft = new PolygonDraft();
        _ = draft.Press(annotations, OnPage, First, false);
        _ = draft.Press(annotations, OnPage, Second, false);
        _ = draft.Press(annotations, OnPage, Third, false);
        var corners = draft.Corners.Count;

        var finished = draft.HandleKey(annotations, ViewerKey.Enter);

        await Assert.That(corners).IsEqualTo(Triangle);
        await Assert.That(finished).IsTrue();
        await Assert.That(draft.IsActive).IsFalse();
        await Assert.That(annotations.Items.Count).IsEqualTo(1);
        await Assert.That(annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.Polygon);
    }

    /// <summary>A double-click finishes the polygon too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DoubleClickAddsThePolygon()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygon-double.pdf", Pages)]);
        var annotations = main.SelectedTab!.Annotations;
        annotations.Tool = AnnotationTool.Polygon;
        var draft = new PolygonDraft();
        _ = draft.Press(annotations, OnPage, First, false);
        _ = draft.Press(annotations, OnPage, Second, false);
        _ = draft.Press(annotations, OnPage, Third, false);

        var finished = draft.Press(annotations, OnPage, Third, true);

        await Assert.That(finished).IsTrue();
        await Assert.That(draft.IsActive).IsFalse();
        await Assert.That(annotations.Items.Count).IsEqualTo(1);
    }
}

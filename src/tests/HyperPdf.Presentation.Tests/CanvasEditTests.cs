// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks selecting, moving, resizing and nudging annotations through a host with no toolkit behind it.</summary>
public sealed class CanvasEditTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 1;

    /// <summary>How far the test drags an annotation on the canvas.</summary>
    private const double Drag = 60;

    /// <summary>A point on the page with nothing on it.</summary>
    private const float Blank = 10F;

    /// <summary>The corner of the test rectangle.</summary>
    private const float RectangleCorner = 100F;

    /// <summary>The side of the test rectangle.</summary>
    private const float RectangleSide = 200F;

    /// <summary>A page that does not exist.</summary>
    private const int MissingPage = 5;

    /// <summary>The note's position on the page.</summary>
    private static readonly PagePoint NoteAt = new(300, 300);

    /// <summary>A press on an empty part of the page selects nothing and starts no drag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PressOnBlankPageStartsNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-blank.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var blank = new PagePoint(Blank, Blank);

        var outcome = state.SelectAndTryStartMove(tab, 0, blank, state.ToCanvas(0, blank));

        await Assert.That(outcome.Session).IsNull();
        await Assert.That(outcome.Handled).IsFalse();
        await Assert.That(tab.Annotations.Selected).IsNull();
    }

    /// <summary>Dragging a picked note moves it, and releasing past the drag distance commits the move.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DraggingANoteMovesIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-move.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Move me"));
        await tab.Annotations.AddNoteAsync(0, NoteAt);
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var note = tab.Annotations.Items[0].Annotation;
        var start = state.ToCanvas(0, new(note.Bounds.Left + 1, note.Bounds.Top + 1));

        var outcome = state.SelectAndTryStartMove(tab, 0, new(note.Bounds.Left + 1, note.Bounds.Top + 1), start);
        state.Tools.ActiveEdit = outcome.Session;
        var changed = state.MoveEdit(tab, new(start.X + Drag, start.Y + Drag));
        var ended = state.EndEdit(tab, new(start.X + Drag, start.Y + Drag));

        await Assert.That(outcome.Session).IsNotNull();
        await Assert.That(changed).IsTrue();
        await Assert.That(ended).IsTrue();
        await Assert.That(state.Tools.ActiveEdit).IsNull();
        await Assert.That(tab.Annotations.Items[0].Annotation.Bounds).IsNotEqualTo(note.Bounds);
    }

    /// <summary>With no drag active, moving and ending do nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoDragMeansNothingToMoveOrEnd()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-none.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();

        await Assert.That(state.MoveEdit(tab, new(Drag, Drag))).IsFalse();
        await Assert.That(state.EndEdit(tab, new(Drag, Drag))).IsFalse();
    }

    /// <summary>An arrow key nudges the picked note, and a key that is not an arrow does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrowKeysNudgeThePickedNote()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-nudge.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Nudge me"));
        await tab.Annotations.AddNoteAsync(0, NoteAt);
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        tab.Annotations.Select(tab.Annotations.Items[0].Annotation);
        var before = tab.Annotations.Selected!.Bounds;

        var other = state.NudgeSelected(ViewerKey.Enter, ViewerModifiers.None);
        var nudged = state.NudgeSelected(ViewerKey.Right, ViewerModifiers.None);

        await Assert.That(other).IsFalse();
        await Assert.That(nudged).IsTrue();
        await Assert.That(tab.Annotations.Items[0].Annotation.Bounds.Left).IsGreaterThan(before.Left);
    }

    /// <summary>The outline of an annotation is a little bigger than its bounds on the canvas.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineSitsOutsideTheBounds()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-outline.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var (_, state) = CanvasUnderTest.Create(tab);
        state.RebuildLayout();
        var bounds = new PageRect(RectangleCorner, RectangleCorner, RectangleSide, RectangleSide);

        var outline = state.GetOutline(tab, 0, bounds);
        var inner = state.GetCanvasRect(0, bounds);

        await Assert.That(outline.X).IsLessThan(inner.X);
        await Assert.That(outline.Width).IsGreaterThan(inner.Width);
        await Assert.That(state.GetOutline(tab, MissingPage, bounds)).IsEqualTo(default(HyperPdf.Core.Layout.LayoutRect));
    }
}

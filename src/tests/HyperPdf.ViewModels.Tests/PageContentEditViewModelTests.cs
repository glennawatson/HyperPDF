// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Markup;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks Edit mode: turning it on, picking what is on the page, and moving, retyping and removing it.</summary>
public sealed class PageContentEditViewModelTests
{
    /// <summary>The scale the group resize test uses.</summary>
    private const float Doubled = 2F;

    /// <summary>Red as 0xRRGGBB.</summary>
    private const uint RedFill = 0xFF0000;

    /// <summary>How many copies the duplicate and paste test makes.</summary>
    private const int CopiesMade = 2;

    /// <summary>The distance the move test shifts the picked object, in points.</summary>
    private const float Shift = 30;

    /// <summary>Half, for finding the middle of a box and a shift.</summary>
    private const float Half = 0.5F;

    /// <summary>The words typed over the picked text.</summary>
    private const string NewWords = "Fresh words";

    /// <summary>The length of the file header checked, such as <c>%PDF-2.0</c>.</summary>
    private const int HeaderLength = 8;

    /// <summary>The seconds a test waits for an edit to reach the page.</summary>
    private const int WaitSeconds = 10;

    /// <summary>The milliseconds between looks at whether an edit has reached the page.</summary>
    private const int PollMilliseconds = 20;

    /// <summary>The side of the big box in the hit test, in points.</summary>
    private const float BoxSize = 200;

    /// <summary>The left and top of the text in the hit test, in points.</summary>
    private const float TextEdge = 20;

    /// <summary>The width of the text in the hit test, in points.</summary>
    private const float TextWidth = 50;

    /// <summary>The height of the text in the hit test, in points.</summary>
    private const float TextHeight = 10;

    /// <summary>A point inside the text in the hit test, across.</summary>
    private const float InTextX = 30;

    /// <summary>A point inside the text in the hit test, down.</summary>
    private const float InTextY = 25;

    /// <summary>A point inside the big box but outside the text in the hit test.</summary>
    private const float InBox = 150;

    /// <summary>A point outside both in the hit test.</summary>
    private const float Far = 500;

    /// <summary>Picks nothing until Edit mode is on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicksNothingUntilEditModeIsOn()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-off.pdf", TestPdf.CreateTagged())]);
        var edit = main.SelectedTab!.ContentEdit;

        _ = await edit.Press(0, new(1, 1), false).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(edit.IsEditing).IsFalse();
            await Assert.That(edit.Selected).IsNull();
        }
    }

    /// <summary>Picks the text under a press and says what was picked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicksTheTextUnderAPress()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-pick.pdf");

        _ = await edit.Press(0, Centre(target), false).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(edit.IsEditing).IsTrue();
            await Assert.That(edit.Selected).IsNotNull();
            await Assert.That(edit.Selected!.Object.Text).IsEqualTo(target.Text);
            await Assert.That(edit.Status).Contains(target.Text, StringComparison.Ordinal);
            await Assert.That(tab.PageCount).IsGreaterThan(0);
        }
    }

    /// <summary>Moves the picked text, keeps it picked, and leaves the page changed for the tiles to be drawn again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesThePickedObject()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-move.pdf");
        _ = await edit.Press(0, Centre(target), false).ToTask();
        var version = tab.ContentVersion;
        var layout = tab.LayoutVersion;

        var used = edit.MoveSelected(Shift, Shift);

        // The tab raises ContentVersion after the pick moves, on its own scheduler, so wait for both.
        var moved = await WaitForAsync(() => edit.Selected is { } pick && pick.Object.Bounds.Left > target.Bounds.Left + (Shift * Half) && tab.ContentVersion > version);

        using (Assert.Multiple())
        {
            await Assert.That(used).IsTrue();
            await Assert.That(moved).IsTrue().Because(await DescribeAsync(tab, edit, target, layout));
            await Assert.That(edit.Selected!.Object.Text).IsEqualTo(target.Text);
            await Assert.That(tab.ContentVersion).IsGreaterThan(version);
        }
    }

    /// <summary>Dragging the picked object writes the move when the pointer is let go.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DraggingThePickedObjectMovesIt()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-drag.pdf");
        var start = Centre(target);
        _ = await edit.Press(0, start, false).ToTask();
        var layout = tab.LayoutVersion;

        var began = edit.BeginDrag(0, start);
        edit.DragTo(new(start.X + Shift, start.Y + Shift));
        var shown = edit.DragDelta.X;
        var ended = edit.EndDrag();
        var moved = await WaitForAsync(() => edit.Selected is { } pick && pick.Object.Bounds.Left > target.Bounds.Left + (Shift * Half));

        using (Assert.Multiple())
        {
            await Assert.That(began).IsTrue();
            await Assert.That(shown).IsEqualTo(Shift).Within(Half);
            await Assert.That(ended).IsTrue();
            await Assert.That(moved).IsTrue().Because(await DescribeAsync(tab, edit, target, layout));
            await Assert.That(edit.DragPage).IsEqualTo(-1);
        }
    }

    /// <summary>Removes the picked text, and the document's undo brings it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesThePickedObjectAndUndoBringsItBack()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-remove.pdf");
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        _ = await edit.Press(0, Centre(target), false).ToTask();

        var used = edit.DeleteSelected();
        var gone = await WaitForAsync(() => edit.Selected is null && edit.Status.Length > 0 && !edit.Status.StartsWith("Picked", StringComparison.Ordinal));
        var after = await editor.ReadObjects(0).ToTask();
        _ = await tab.Pages.UndoCommand.Execute().ToTask();
        var restored = await editor.ReadObjects(0).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(used).IsTrue();
            await Assert.That(gone).IsTrue();
            await Assert.That(HasText(after, target.Text)).IsFalse();
            await Assert.That(HasText(restored, target.Text)).IsTrue();
        }
    }

    /// <summary>Picks every object of a page, moves them together, and one undo puts them all back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesSeveralPickedObjectsInOneStep()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, _) = await StartEditingAsync(test, main, "edit-group.pdf");
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var before = await editor.ReadObjects(0).ToTask();

        _ = await edit.PickEveryObject(0).ToTask();
        var picked = edit.PickedCount;
        var used = edit.MoveSelected(Shift, Shift);
        var moved = await WaitForAsync(() => edit.Status == "Moved.");
        var after = await editor.ReadObjects(0).ToTask();
        _ = await tab.Pages.UndoCommand.Execute().ToTask();
        var restored = await editor.ReadObjects(0).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(picked).IsEqualTo(before.Length);
            await Assert.That(used).IsTrue();
            await Assert.That(moved).IsTrue();
            await Assert.That(after.Length).IsEqualTo(before.Length);
            await Assert.That(after[0].Bounds.Left).IsEqualTo(before[0].Bounds.Left + Shift).Within(1);
            await Assert.That(after[^1].Bounds.Top).IsEqualTo(before[^1].Bounds.Top + Shift).Within(1);
            await Assert.That(restored[0].Bounds.Left).IsEqualTo(before[0].Bounds.Left).Within(1);
        }
    }

    /// <summary>Resizes and recolours several picked objects together, each as one step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResizesAndRecoloursSeveralPickedObjects()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, _) = await StartEditingAsync(test, main, "edit-group-shape.pdf");
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var before = await editor.ReadObjects(0).ToTask();
        _ = await edit.PickEveryObject(0).ToTask();

        var scaled = edit.ScaleSelected(Doubled);
        var resized = await WaitForAsync(() => edit.Status == "Resized.");
        var afterScale = await editor.ReadObjects(0).ToTask();
        var painted = edit.SetFill(RedFill);
        var recoloured = await WaitForAsync(() => edit.Status == "Changed the fill colour.");

        using (Assert.Multiple())
        {
            await Assert.That(scaled).IsTrue();
            await Assert.That(resized).IsTrue();
            await Assert.That(afterScale.Length).IsEqualTo(before.Length);
            await Assert.That(afterScale[^1].Bounds.Width).IsGreaterThan(before[^1].Bounds.Width);
            await Assert.That(painted).IsTrue();
            await Assert.That(recoloured).IsTrue();
        }
    }

    /// <summary>Changes the dashes of several picked objects in one step, and says so plainly when text is asked to be made see-through.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesTheLookOfSeveralPickedObjects()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, _) = await StartEditingAsync(test, main, "edit-group-look.pdf");
        _ = await edit.PickEveryObject(0).ToTask();

        ((ICommand)edit.SetPickedDashCommand).Execute(nameof(PageLineDash.Dashed));
        var dashed = await WaitForAsync(() => edit.Status == "Changed the dashes.");
        ((ICommand)edit.SetPickedOpacityCommand).Execute("50");
        var refused = await WaitForAsync(() => tab.Notice?.Contains("see-through", StringComparison.Ordinal) == true);

        using (Assert.Multiple())
        {
            await Assert.That(dashed).IsTrue();
            await Assert.That(refused).IsTrue();
        }
    }

    /// <summary>Drags the handle on the box around several picked objects and they all grow with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResizesSeveralPickedObjectsFromTheirBox()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, _) = await StartEditingAsync(test, main, "edit-group-handles.pdf");
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var before = await editor.ReadObjects(0).ToTask();
        _ = await edit.PickEveryObject(0).ToTask();
        var box = edit.GroupBounds();
        var corner = new PagePoint(box.Right, box.Bottom);

        var started = edit.BeginResize(0, corner, 1);
        edit.ResizeTo(new(box.Left + (box.Width * Doubled), box.Top + (box.Height * Doubled)), false);
        var ended = edit.EndResize();
        var resized = await WaitForAsync(() => edit.Status == "Resized.");
        var after = await editor.ReadObjects(0).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(started).IsTrue();
            await Assert.That(ended).IsTrue();
            await Assert.That(resized).IsTrue();
            await Assert.That(after.Length).IsEqualTo(before.Length);
            await Assert.That(after[^1].Bounds.Width).IsGreaterThan(before[^1].Bounds.Width);
            await Assert.That(edit.PickedCount).IsEqualTo(before.Length);
        }
    }

    /// <summary>Duplicates the picked object, then copies it and pastes it, each adding one object to the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DuplicatesAndPastesThePickedObject()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-duplicate.pdf");
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        _ = await edit.Press(0, Centre(target), false).ToTask();
        var before = (await editor.ReadObjects(0).ToTask()).Length;

        var duplicating = edit.DuplicatePicked();
        var duplicated = await WaitForAsync(() => edit.Status == "Duplicated.");
        var afterDuplicate = (await editor.ReadObjects(0).ToTask()).Length;
        _ = await edit.Press(0, Centre(target), false).ToTask();
        var copying = edit.CopyPicked();
        var pasting = edit.PasteCopied(0);
        var pasted = await WaitForAsync(() => edit.Status == "Pasted.");
        var afterPaste = (await editor.ReadObjects(0).ToTask()).Length;

        using (Assert.Multiple())
        {
            await Assert.That(duplicating).IsTrue();
            await Assert.That(duplicated).IsTrue();
            await Assert.That(afterDuplicate).IsEqualTo(before + 1);
            await Assert.That(copying).IsTrue();
            await Assert.That(pasting).IsTrue();
            await Assert.That(pasted).IsTrue();
            await Assert.That(afterPaste).IsEqualTo(before + CopiesMade);
        }
    }

    /// <summary>Retypes the picked text on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetypesThePickedText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-retype.pdf");
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        _ = await edit.Press(0, Centre(target), true).ToTask();

        var editing = edit.TextEdit is not null;
        edit.EditingText = NewWords;
        edit.CommitText();
        var written = await WaitForAsync(() => edit.Status == "Changed the text.");
        var after = await editor.ReadObjects(0).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(editing).IsTrue();
            await Assert.That(edit.TextEdit).IsNull();
            await Assert.That(written).IsTrue();
            await Assert.That(HasText(after, NewWords)).IsTrue();
        }
    }

    /// <summary>Saving after editing the page writes a PDF 2.0 file, and saving an untouched document keeps its own version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingAfterEditingWritesPdf20()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (tab, edit, target) = await StartEditingAsync(test, main, "edit-save.pdf");
        var untouched = Path.Combine(test.Directory, "untouched.pdf");
        var upgraded = Path.Combine(test.Directory, "upgraded.pdf");
        var savedUntouched = await tab.SaveTo(untouched).ToTask();
        _ = await edit.Press(0, Centre(target), false).ToTask();
        _ = edit.DeleteSelected();
        var written = await WaitForAsync(() => edit.Status == "Removed.");

        var savedEdited = await tab.SaveTo(upgraded).ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(savedUntouched).IsTrue();
            await Assert.That(written).IsTrue();
            await Assert.That(savedEdited).IsTrue();
            await Assert.That(await HeaderOfAsync(untouched)).IsNotEqualTo("%PDF-2.0");
            await Assert.That(await HeaderOfAsync(upgraded)).IsEqualTo("%PDF-2.0");
        }
    }

    /// <summary>Escape lets go of the picked object first, then leaves Edit mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EscapeLetsGoThenLeavesEditMode()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (_, edit, target) = await StartEditingAsync(test, main, "edit-escape.pdf");
        _ = await edit.Press(0, Centre(target), false).ToTask();

        var first = edit.Escape();
        var stillEditing = edit.IsEditing;
        var second = edit.Escape();

        using (Assert.Multiple())
        {
            await Assert.That(first).IsTrue();
            await Assert.That(stillEditing).IsTrue();
            await Assert.That(second).IsTrue();
            await Assert.That(edit.IsEditing).IsFalse();
        }
    }

    /// <summary>Finds the smallest object under a point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitTestPrefersTheSmallestObject()
    {
        PageObjectInfo[] objects =
        [
            new(0, PageObjectKind.Drawing, new(0, 0, BoxSize, BoxSize), string.Empty),
            new(1, PageObjectKind.Text, new(TextEdge, TextEdge, TextWidth, TextHeight), "Hello"),
        ];

        var inText = PageObjectHits.Find(objects, new(InTextX, InTextY));
        var inBox = PageObjectHits.Find(objects, new(InBox, InBox));
        var nowhere = PageObjectHits.Find(objects, new(Far, Far));

        using (Assert.Multiple())
        {
            await Assert.That(inText).IsEqualTo(1);
            await Assert.That(inBox).IsEqualTo(0);
            await Assert.That(nowhere).IsEqualTo(-1);
        }
    }

    /// <summary>Turns Edit mode on for a document and finds a text object to work on.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The viewer.</param>
    /// <param name="name">The document's file name.</param>
    /// <returns>The tab, its Edit mode and the first text object of page one.</returns>
    private static async Task<EditStart> StartEditingAsync(TestServices test, MainViewModel main, string name)
    {
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(name, TestPdf.CreateTagged())]);
        var tab = main.SelectedTab!;
        var edit = tab.ContentEdit;
        _ = await edit.ToggleEditingCommand.Execute().ToTask();
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var target = Array.Find(await editor.ReadObjects(0).ToTask(), static o => o.Kind == PageObjectKind.Text);

        // Hovering reads the page, so the first press does not wait for it.
        edit.HoverAt(0, Centre(target));
        return new(tab, edit, target);
    }

    /// <summary>Says why a move did not show up: what the view model reports and what the document's page holds now.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="edit">Its Edit mode.</param>
    /// <param name="target">The text object before the move.</param>
    /// <param name="layout">The tab's layout version when the move began.</param>
    /// <returns>The description.</returns>
    private static async Task<string> DescribeAsync(DocumentTabViewModel tab, PageContentEditViewModel edit, PageObjectInfo target, int layout)
    {
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var now = await editor.ReadObjects(0).ToTask();
        var page = string.Join(" | ", now.Select(static o => $"{o.Kind} '{o.Text}' {o.Bounds.Left}"));
        return $"status: {edit.Status}; notice: {tab.Notice}; picked: {edit.Selected?.Object.Bounds}; was: {target.Bounds}; layout {layout} -> {tab.LayoutVersion}; page now: {page}";
    }

    /// <summary>Reads the first bytes of a file as text.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The header, such as <c>%PDF-2.0</c>.</returns>
    private static async Task<string> HeaderOfAsync(string path)
    {
        var header = new byte[HeaderLength];
        await using var file = File.OpenRead(path);
        _ = await file.ReadAsync(header);
        return System.Text.Encoding.ASCII.GetString(header);
    }

    /// <summary>Gets the middle of an object's box.</summary>
    /// <param name="info">The object.</param>
    /// <returns>The point in page space.</returns>
    private static PagePoint Centre(in PageObjectInfo info) => new(info.Bounds.Left + (info.Bounds.Width * Half), info.Bounds.Top + (info.Bounds.Height * Half));

    /// <summary>Tells whether any object holds the words.</summary>
    /// <param name="objects">The objects of a page.</param>
    /// <param name="words">The words.</param>
    /// <returns><see langword="true"/> when an object's text contains them.</returns>
    private static bool HasText(PageObjectInfo[] objects, string words) => Array.Exists(objects, item => item.Text.Contains(words, StringComparison.Ordinal));

    /// <summary>Waits for a condition the view model reaches once an edit has been written.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true"/> when the condition was met before the wait limit.</returns>
    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(WaitSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(PollMilliseconds));
        while (!condition())
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(limit.Token))
                {
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                return condition();
            }
        }

        return true;
    }

    /// <summary>What a test starts from: the tab, its Edit mode and a text object to work on.</summary>
    /// <param name="Tab">The tab.</param>
    /// <param name="Edit">Its Edit mode.</param>
    /// <param name="Target">The first text object of page one.</param>
    private sealed record EditStart(DocumentTabViewModel Tab, PageContentEditViewModel Edit, PageObjectInfo Target);
}

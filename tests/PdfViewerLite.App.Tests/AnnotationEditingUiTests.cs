#pragma warning disable
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Headless checks of editing annotations on the page: dragging to move, dragging a corner to resize, arrow keys to
/// nudge, the undo and redo buttons and keys, the polygon, cloud and callout tools, picture stamps, and filtering the
/// comment list.
/// </summary>
public sealed class AnnotationEditingUiTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>How far the pointer drags, in window units.</summary>
    private const double Drag = 60;

    /// <summary>How far a corner is dragged out, in window units.</summary>
    private const double Stretch = 40;

    /// <summary>How far past the outline the resize handle sits from the annotation's corner.</summary>
    private const double HandleOffset = 2;

    /// <summary>The smallest change, in points, that shows a move happened.</summary>
    private const float MinMove = 10;

    /// <summary>How close positions must be, in points.</summary>
    private const float Tolerance = 0.5F;

    /// <summary>How far a resized edge may shift, as the handle sits just outside the outline.</summary>
    private const float HandleSlack = 4;

    /// <summary>The arrow key presses.</summary>
    private const int Nudges = 2;

    /// <summary>Two comments.</summary>
    private const int Two = 2;

    /// <summary>Halves a size to find a middle.</summary>
    private const float Half = 0.5F;

    /// <summary>A distance on the page between annotations, in points.</summary>
    private const float Spread = 40;

    /// <summary>How many drags down the cloud is drawn below the polygon.</summary>
    private const double CloudRows = 3;

    /// <summary>How many drags right the callout is drawn.</summary>
    private const double CalloutColumns = 5;

    /// <summary>The picture's side in pixels.</summary>
    private const int PictureSide = 4;

    /// <summary>The picture's dots per inch.</summary>
    private const double PictureDpi = 96;

    /// <summary>A polygon's corners in window units from the page's top-left.</summary>
    private static readonly Vector[] Corners = [new(100, 120), new(200, 120), new(150, 200)];

    /// <summary>Where the stamp goes.</summary>
    private static readonly PagePoint StampAt = new(200, 300);

    /// <summary>
    /// Dragging the stamp moves it, dragging its corner handle resizes it, the arrow keys nudge it, and Ctrl+Z puts it
    /// back one step at a time, the nudges together.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesResizesAndNudgesWithThePointerAndKeys()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("move.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            tab.SidebarMode = SidebarMode.Annotations;
            var canvas = await ReadyAsync(window, tab);
            _ = annotations.AddStamp(0, StampAt);
            var placed = annotations.Items[0].Annotation;
            canvas.InvalidateVisual();

            var grab = ToWindow(canvas, window, new(placed.Bounds.Left + (placed.Bounds.Width * Half), placed.Bounds.Top + (placed.Bounds.Height * Half)));
            window.MouseDown(grab, MouseButton.Left);
            window.MouseMove(grab + new Vector(Drag, Drag), RawInputModifiers.LeftMouseButton);
            window.MouseUp(grab + new Vector(Drag, Drag), MouseButton.Left);
            var moved = annotations.Items[0].Annotation;

            var corner = ToWindow(canvas, window, new(moved.Bounds.Right, moved.Bounds.Bottom)) + new Vector(HandleOffset, HandleOffset);
            window.MouseDown(corner, MouseButton.Left);
            window.MouseMove(corner + new Vector(Stretch, Stretch), RawInputModifiers.LeftMouseButton);
            window.MouseUp(corner + new Vector(Stretch, Stretch), MouseButton.Left);
            var resized = annotations.Items[0].Annotation;

            _ = canvas.Focus();
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            var nudged = annotations.Items[0].Annotation;
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
            var undoneNudge = annotations.Items[0].Annotation;
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
            var undoneMove = annotations.Items[0].Annotation;
            window.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, null);
            var redone = annotations.Items[0].Annotation;

            await Assert.That(moved.Bounds.Left - placed.Bounds.Left).IsGreaterThan(MinMove);
            await Assert.That(moved.Bounds.Top - placed.Bounds.Top).IsGreaterThan(MinMove);
            await Assert.That(resized.Bounds.Width - moved.Bounds.Width).IsGreaterThan(MinMove);
            await Assert.That(Math.Abs(resized.Bounds.Left - moved.Bounds.Left)).IsLessThan(HandleSlack);
            await Assert.That(Math.Abs(nudged.Bounds.Left - (resized.Bounds.Left + (Nudges * SignatureMarkLayout.MoveStep)))).IsLessThan(1F);
            await Assert.That(Math.Abs(undoneNudge.Bounds.Left - resized.Bounds.Left)).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(undoneMove.Bounds.Left - placed.Bounds.Left)).IsLessThan(Tolerance);
            await Assert.That(Math.Abs(redone.Bounds.Left - moved.Bounds.Left)).IsLessThan(Tolerance);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Undo and Redo are on the tool bar with their names, and are only enabled when there is something to undo or redo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoAndRedoButtonsFollowTheHistory()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("buttons.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var atStart = (view.UndoButton.IsEnabled, view.RedoButton.IsEnabled);
            _ = tab.Annotations.AddShape(0, AnnotationKind.Rectangle, StampAt, new(StampAt.X + Spread, StampAt.Y + Spread));
            var afterAdding = await UiWait.UntilAsync(() => view.UndoButton.IsEnabled && !view.RedoButton.IsEnabled);
            view.UndoButton.Command!.Execute(null);
            var afterUndo = await UiWait.UntilAsync(() => !view.UndoButton.IsEnabled && view.RedoButton.IsEnabled);
            window.KeyPress(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.Z, null);
            var afterRedo = await UiWait.UntilAsync(() => tab.Annotations.Items.Count == 1);

            await Assert.That(atStart).IsEqualTo((false, false));
            await Assert.That(afterAdding && afterUndo && afterRedo).IsTrue();
            await Assert.That(Avalonia.Automation.AutomationProperties.GetName(view.RedoButton)).IsEqualTo("Redo");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The polygon tool places corners with clicks and finishes with Enter; the cloud tool finishes with a double click; the callout tool is dragged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsPolygonsCloudsAndCallouts()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("polygons.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            tab.SidebarMode = SidebarMode.Annotations;
            var canvas = await ReadyAsync(window, tab);

            // Registered after the window's own handler, so the test answers first.
            using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Look here"));
            var origin = ToWindow(canvas, window, default);

            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Polygon).ToTask();
            var label = annotations.ShapeLabel;
            foreach (var corner in Corners)
            {
                Click(window, origin + corner);
            }

            _ = canvas.Focus();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Cloud).ToTask();
            var shift = new Vector(0, Drag * CloudRows);
            foreach (var corner in Corners)
            {
                Click(window, origin + corner + shift);
            }

            // A second click on the last corner makes a double click, which finishes the cloud.
            Click(window, origin + Corners[^1] + shift);

            _ = await annotations.SetToolCommand.Execute(AnnotationTool.Callout).ToTask();
            var target = origin + new Vector(Drag * CalloutColumns, Drag);
            window.MouseDown(target, MouseButton.Left);
            window.MouseMove(target + new Vector(Drag, Drag), RawInputModifiers.LeftMouseButton);
            window.MouseUp(target + new Vector(Drag, Drag), MouseButton.Left);
            _ = await UiWait.UntilAsync(() => annotations.Items.Count == 3);

            await Assert.That(label).IsEqualTo("Polygon");
            await Assert.That(annotations.Items.Select(static i => i.Annotation.Kind).ToArray()).IsEquivalentTo([AnnotationKind.Polygon, AnnotationKind.Cloud, AnnotationKind.Callout]);
            await Assert.That(annotations.Items.Single(static i => i.Annotation.Kind == AnnotationKind.Callout).Annotation.Contents).IsEqualTo("Look here");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A picture chosen for the stamp tool is placed where the page is clicked, as a stamp.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesAPictureStamp()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("picture.pdf", 1)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;
        var picture = Path.Combine(test.Directory, "stamp.png");
        using (var bitmap = new WriteableBitmap(new(PictureSide, PictureSide), new(PictureDpi, PictureDpi), PixelFormat.Bgra8888, AlphaFormat.Unpremul))
        {
            bitmap.Save(picture, new PngBitmapEncoderOptions());
        }

        using var choose = annotations.ChoosePictureInteraction.RegisterHandler(context => context.SetOutput(picture));

        _ = await annotations.PictureStampCommand.Execute().ToTask();
        var placed = annotations.AddStamp(0, StampAt);

        await Assert.That(annotations.Tool).IsEqualTo(AnnotationTool.Stamp);
        await Assert.That(annotations.StampButtonLabel).IsEqualTo("Stamp: Picture");
        await Assert.That(placed).IsTrue();
        await Assert.That(annotations.Items.Single().Annotation.Kind).IsEqualTo(AnnotationKind.Stamp);
    }

    /// <summary>Typing in the comment list's filter box hides comments that do not match, and says so when none do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiltersTheCommentList()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("filter.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            tab.SidebarMode = SidebarMode.Annotations;
            _ = await ReadyAsync(window, tab);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var answers = new Queue<string>(["Check the total", "Spelling"]);
            using var prompt = annotations.PromptInteraction.RegisterHandler(context => context.SetOutput(answers.Dequeue()));
            await annotations.AddNoteAsync(0, StampAt);
            await annotations.AddNoteAsync(0, new(StampAt.X, StampAt.Y + Spread));
            _ = await UiWait.UntilAsync(() => view.AnnotationList.ItemCount == Two);

            view.AnnotationFilterBox.Text = "total";
            var filtered = await UiWait.UntilAsync(() => view.AnnotationList.ItemCount == 1);
            view.AnnotationFilterBox.Text = "nothing like this";
            var none = await UiWait.UntilAsync(() => view.NoMatchesText.IsVisible && view.AnnotationList.ItemCount == 0);
            view.AnnotationFiltersToggle.IsChecked = true;
            var shown = await UiWait.UntilAsync(() => view.AnnotationFiltersPanel.IsVisible);
            view.ClearAnnotationFilterButton.Command!.Execute(null);
            var all = await UiWait.UntilAsync(() => view.AnnotationList.ItemCount == Two && !view.NoMatchesText.IsVisible);

            await Assert.That(filtered && none && shown && all).IsTrue();
            await Assert.That(view.AnnotationFilterBox.Text).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits until the document is shown and drawn, with the annotation tools out.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(Window window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        _ = await tab.Annotations.StartCommand.Execute().ToTask();
        var hub = tab.RenderHub;
        _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Converts a point on the first page to window coordinates.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="window">The window.</param>
    /// <param name="point">The page point.</param>
    /// <returns>The window point.</returns>
    private static Point ToWindow(PageCanvas canvas, Window window, PagePoint point) => canvas.TranslatePoint(canvas.PageToCanvas(0, point), window)!.Value;

    /// <summary>Clicks a window point with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point.</param>
    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}

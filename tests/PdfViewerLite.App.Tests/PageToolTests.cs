// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Navigation;
using PdfViewerLite.Core.Settings;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Moving around and grabbing content in the real window: the hand tool, Space and middle button drags, zoom to area,
/// pinch zoom, select all, snapshots, auto-scroll and Home and End.
/// </summary>
public sealed class PageToolTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 6;

    /// <summary>How far drags move, in device independent pixels.</summary>
    private const double DragDistance = 200;

    /// <summary>How close, in pixels, a moved offset must be to the drag.</summary>
    private const double Tolerance = 2;

    /// <summary>The zoom tolerance.</summary>
    private const double ZoomTolerance = 0.01;

    /// <summary>The share of the viewport across from its left edge where drags start.</summary>
    private const double StartX = 0.5;

    /// <summary>The share of the viewport down from its top where drags start.</summary>
    private const double StartY = 0.6;

    /// <summary>The width of the area dragged for zoom to area and snapshots.</summary>
    private const double AreaWidth = 240;

    /// <summary>The height of the area dragged for zoom to area and snapshots.</summary>
    private const double AreaHeight = 120;

    /// <summary>The pinch that doubles the zoom.</summary>
    private const double PinchScale = 2;

    /// <summary>How many times sharper than the screen a snapshot is at least, at 100% on a plain screen.</summary>
    private const double SnapshotSharpness = 2;

    /// <summary>The zoom set before dragging sideways, wide enough to scroll across.</summary>
    private const string WideZoom = "300";

    /// <summary>Verifies the Hand tool moves the pages with a left drag, and the View menu shows it is chosen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandToolDragsThePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "hand.pdf");
        try
        {
            var (tab, canvas, scroller, view) = Parts(window, main);
            _ = await tab.SetPageToolCommand.Execute(PageTool.Hand).ToTask();
            _ = await tab.SetZoomCommand.Execute(WideZoom).ToTask();
            _ = await UiWait.UntilAsync(() => scroller.Extent.Width > scroller.Viewport.Width);
            var before = scroller.Offset;

            var start = Start(scroller, window);
            Drag(window, start, start - new Vector(DragDistance, DragDistance), MouseButton.Left, RawInputModifiers.None);
            var moved = await UiWait.UntilAsync(() => Math.Abs(scroller.Offset.Y - before.Y - DragDistance) < Tolerance);

            await Assert.That(moved).IsTrue();
            await Assert.That(scroller.Offset.X - before.X).IsEqualTo(DragDistance).Within(Tolerance);
            await Assert.That(canvas.GetSelectedText()).IsEqualTo(string.Empty);
            await Assert.That(view.HandToolItem.IsChecked).IsTrue();
            await Assert.That(view.SelectTextToolItem.IsChecked).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the middle button and a drag with Space held move the pages while selecting text is chosen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MiddleButtonAndSpaceDragThePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "middle.pdf");
        try
        {
            var (tab, _, scroller, _) = Parts(window, main);
            var start = Start(scroller, window);
            var before = scroller.Offset.Y;
            Drag(window, start, start - new Vector(0, DragDistance), MouseButton.Middle, RawInputModifiers.MiddleMouseButton);
            var middle = await UiWait.UntilAsync(() => Math.Abs(scroller.Offset.Y - before - DragDistance) < Tolerance);

            before = scroller.Offset.Y;
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Drag(window, start, start - new Vector(0, DragDistance), MouseButton.Left, RawInputModifiers.None);
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            var space = await UiWait.UntilAsync(() => Math.Abs(scroller.Offset.Y - before - DragDistance) < Tolerance);

            await Assert.That(tab.PageTool).IsEqualTo(PageTool.SelectText);
            await Assert.That(middle).IsTrue();
            await Assert.That(space).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Space tapped without a drag still turns the page when viewing page by page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpaceTapTurnsThePageByPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "space.pdf");
        try
        {
            var (tab, _, scroller, _) = Parts(window, main);
            tab.ZoomMode = ZoomMode.FitPage;
            tab.IsPageByPage = true;
            _ = await UiWait.UntilAsync(() => Math.Abs(scroller.Extent.Height - (scroller.Viewport.Height * Pages)) < Tolerance);

            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            var turned = await UiWait.UntilAsync(() => tab.CurrentPageIndex == 1);

            await Assert.That(turned).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Zoom to Area zooms until the dragged box fills the view, and Escape cancels a drag.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomToAreaFillsTheView()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "area.pdf");
        try
        {
            var (tab, _, scroller, view) = Parts(window, main);
            _ = await tab.SetZoomCommand.Execute("100").ToTask();
            _ = await tab.SetPageToolCommand.Execute(PageTool.ZoomArea).ToTask();
            var start = Start(scroller, window);

            // Escape during the drag leaves the zoom alone.
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Vector(AreaWidth, AreaHeight));
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.MouseUp(start + new Vector(AreaWidth, AreaHeight), MouseButton.Left);
            _ = await UiWait.UntilAsync(static () => true);
            var cancelled = tab.Zoom;

            var viewport = scroller.Viewport;
            var expected = Math.Min(viewport.Width / AreaWidth, viewport.Height / AreaHeight);
            Drag(window, start, start + new Vector(AreaWidth, AreaHeight), MouseButton.Left, RawInputModifiers.None);
            var zoomed = await UiWait.UntilAsync(() => Math.Abs(tab.Zoom - expected) < ZoomTolerance);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            var back = await UiWait.UntilAsync(() => tab.PageTool == PageTool.SelectText);

            await Assert.That(cancelled).IsEqualTo(1).Within(ZoomTolerance);
            await Assert.That(zoomed).IsTrue();
            await Assert.That(back).IsTrue();
            await Assert.That(view.ZoomAreaToolItem.IsChecked).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies a touch pinch zooms by its scale, measured from the start of the gesture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PinchZoomsAroundTheFingers()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "pinch.pdf");
        try
        {
            var (tab, canvas, scroller, _) = Parts(window, main);
            _ = await tab.SetZoomCommand.Execute("100").ToTask();
            var origin = new Point(scroller.Offset.X + (scroller.Viewport.Width * StartX), scroller.Offset.Y + (scroller.Viewport.Height * StartY));
            canvas.RaiseEvent(new PinchEventArgs(Math.Sqrt(PinchScale), origin));
            canvas.RaiseEvent(new PinchEventArgs(PinchScale, origin));
            canvas.RaiseEvent(new PinchEndedEventArgs());
            var doubled = await UiWait.UntilAsync(() => Math.Abs(tab.Zoom - PinchScale) < ZoomTolerance);

            canvas.RaiseEvent(new PinchEventArgs(1 / PinchScale, origin));
            var back = await UiWait.UntilAsync(() => Math.Abs(tab.Zoom - 1) < ZoomTolerance);

            await Assert.That(doubled).IsTrue();
            await Assert.That(back).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Ctrl+A and the menu item select all the text on the current page, Ctrl+C copies it and caret mode follows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectAllSelectsTheCurrentPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "all.pdf");
        try
        {
            var (tab, canvas, _, _) = Parts(window, main);
            var document = tab.TryGetDocument()!;
            var count = document.GetCharacterCount(0);
            var text = document.GetText(0, 0, count);

            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            var selected = await UiWait.UntilAsync(() => canvas.GetSelectedText() == text);
            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
            var clipboard = TopLevel.GetTopLevel(canvas)!.Clipboard!;
            _ = await UiWait.UntilAsync(static () => true);
            var copied = await clipboard.TryGetTextAsync();

            canvas.ClearSelection();
            _ = await tab.ToggleCaretModeCommand.Execute().ToTask();
            _ = await tab.SelectAllCommand.Execute().ToTask();
            var fromMenu = await UiWait.UntilAsync(() => canvas.GetSelectedText() == text);

            await Assert.That(count).IsGreaterThan(0);
            await Assert.That(selected).IsTrue();
            await Assert.That(copied).IsEqualTo(text);
            await Assert.That(fromMenu).IsTrue();
            await Assert.That(canvas.Caret).IsEqualTo((0, count - 1));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the Snapshot tool puts a sharp picture of the dragged area on the clipboard and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SnapshotCopiesAnImage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "snapshot.pdf");
        try
        {
            var (tab, canvas, scroller, view) = Parts(window, main);
            _ = await tab.SetZoomCommand.Execute("100").ToTask();
            _ = await tab.ToggleSnapshotToolCommand.Execute().ToTask();
            var chosen = view.SnapshotToolItem.IsChecked;
            var start = Start(scroller, window);
            Drag(window, start, start + new Vector(AreaWidth, AreaHeight), MouseButton.Left, RawInputModifiers.None);
            var told = await UiWait.UntilAsync(() => tab.Notice is not null);
            var clipboard = TopLevel.GetTopLevel(canvas)!.Clipboard!;
            var image = await clipboard.TryGetBitmapAsync();

            await Assert.That(chosen).IsTrue();
            await Assert.That(told).IsTrue();
            await Assert.That(tab.Notice).StartsWith("Copied an image of the area");
            await Assert.That(image).IsNotNull();

            // At 100% on a plain screen the picture is rendered at 200 dots per inch, sharper than the screen.
            await Assert.That((double)image!.PixelSize.Width).IsGreaterThan(AreaWidth * SnapshotSharpness);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies auto-scroll moves the pages, the arrows change its speed, and Escape and a click stop it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoScrollMovesAndStops()
    {
        using var test = new TestServices();
        test.Services.Settings.Motion = MotionPreference.Normal;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "auto.pdf");
        try
        {
            var (tab, canvas, scroller, view) = Parts(window, main);
            var before = scroller.Offset.Y;
            Shortcut(window, Key.H, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.H);
            var moving = await UiWait.UntilAsync(() => scroller.Offset.Y > before + Tolerance);
            var barShown = view.AutoScrollBar.IsVisible;
            window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
            var faster = tab.AutoScrollSpeed;
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            var stopped = await UiWait.UntilAsync(() => !tab.IsAutoScrolling && !canvas.IsAutoScrollRunning && !view.AutoScrollBar.IsVisible);

            _ = await tab.ToggleAutoScrollCommand.Execute().ToTask();
            var restarted = await UiWait.UntilAsync(() => canvas.IsAutoScrollRunning && view.AutoScrollBar.IsVisible);
            window.UpdateLayout();
            window.MouseDown(Start(scroller, window), MouseButton.Left);
            window.MouseUp(Start(scroller, window), MouseButton.Left);
            var clickStopped = await UiWait.UntilAsync(() => !tab.IsAutoScrolling && !view.AutoScrollBar.IsVisible);

            await Assert.That(moving).IsTrue();
            await Assert.That(barShown).IsTrue();
            await Assert.That(faster).IsEqualTo(AutoScroller.DefaultSpeed + 1);
            await Assert.That(stopped).IsTrue();
            await Assert.That(restarted).IsTrue();
            await Assert.That(clickStopped).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies auto-scroll steps whole lines when movement is reduced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotionAutoScrollStepsLines()
    {
        using var test = new TestServices();
        test.Services.Settings.Motion = MotionPreference.Reduced;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "steps.pdf");
        try
        {
            var (tab, _, scroller, _) = Parts(window, main);
            var offsets = new List<double>();
            _ = await tab.ToggleAutoScrollCommand.Execute().ToTask();
            var start = scroller.Offset.Y;
            _ = await UiWait.UntilAsync(() =>
            {
                offsets.Add(scroller.Offset.Y - start);
                return scroller.Offset.Y > start + AutoScroller.LineDistance;
            });
            tab.IsAutoScrolling = false;

            // Every offset seen is a whole number of lines: the pages never glide between them.
            var partial = offsets.Where(static offset => Math.Abs(Math.IEEERemainder(offset, AutoScroller.LineDistance)) > Tolerance).ToArray();
            await Assert.That(offsets.Max()).IsGreaterThanOrEqualTo(AutoScroller.LineDistance);
            await Assert.That(partial).IsEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Home and End go to the first and last page, scrolling and page by page, but not in caret mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HomeAndEndGoToTheEnds()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = await OpenAsync(test, main, "ends.pdf");
        try
        {
            var (tab, canvas, _, view) = Parts(window, main);
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            var last = await UiWait.UntilAsync(() => tab.CurrentPageIndex == Pages - 1);
            window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null);
            var first = await UiWait.UntilAsync(() => tab.CurrentPageIndex == 0);

            tab.IsPageByPage = true;
            _ = await UiWait.UntilAsync(static () => true);
            _ = canvas.Focus();
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            var lastByPage = await UiWait.UntilAsync(() => tab.CurrentPageIndex == Pages - 1);
            _ = await tab.FirstPageCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => tab.CurrentPageIndex == 0);

            _ = await tab.ToggleCaretModeCommand.Execute().ToTask();
            window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null);
            _ = await UiWait.UntilAsync(static () => true);

            await Assert.That(last).IsTrue();
            await Assert.That(first).IsTrue();
            await Assert.That(lastByPage).IsTrue();
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(0);
            await Assert.That(view.FirstPageItem.InputGesture?.ToString()).IsEqualTo("Ctrl+Home");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Opens a document in a window and waits until its first pages are drawn.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The window.</returns>
    private static async Task<Window> OpenAsync(TestServices test, MainViewModel main, string name)
    {
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument(name, Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0 && window.GetVisualDescendants().OfType<PageCanvas>().Any(static c => c.IsFocused));
        return window;
    }

    /// <summary>Finds the tab, its pages, their scroll viewer and the document view.</summary>
    /// <param name="window">The window.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>The parts.</returns>
    private static (DocumentTabViewModel Tab, PageCanvas Canvas, ScrollViewer Scroller, DocumentView View) Parts(Window window, MainViewModel main)
    {
        var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
        return (main.SelectedTab!, canvas, canvas.FindAncestorOfType<ScrollViewer>()!, window.GetVisualDescendants().OfType<DocumentView>().Single());
    }

    /// <summary>Gets where drags start, in window coordinates.</summary>
    /// <param name="scroller">The pages' scroll viewer.</param>
    /// <param name="window">The window.</param>
    /// <returns>The point.</returns>
    private static Point Start(ScrollViewer scroller, Window window) =>
        scroller.TranslatePoint(new(scroller.Viewport.Width * StartX, scroller.Viewport.Height * StartY), window)!.Value;

    /// <summary>Drags with a mouse button from one point to another.</summary>
    /// <param name="window">The window.</param>
    /// <param name="from">The start, in window coordinates.</param>
    /// <param name="to">The end, in window coordinates.</param>
    /// <param name="button">The button.</param>
    /// <param name="held">The button as a modifier, while it is held.</param>
    private static void Drag(Window window, Point from, Point to, MouseButton button, RawInputModifiers held)
    {
        window.MouseDown(from, button);
        window.MouseMove(from + ((to - from) * StartX), held);
        window.MouseMove(to, held);
        window.MouseUp(to, button);
    }

    /// <summary>Presses a key with modifiers, as a person would for a shortcut.</summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers.</param>
    /// <param name="physical">The physical key.</param>
    private static void Shortcut(Window window, Key key, RawInputModifiers modifiers, PhysicalKey physical) => window.KeyPress(key, modifiers, physical, null);
}

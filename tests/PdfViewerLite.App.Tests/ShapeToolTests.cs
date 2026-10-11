// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the shape and stamp tools: choosing them, drawing and placing, the sidebar list, undo and the tool bar.</summary>
public sealed class ShapeToolTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Half, for finding the middle of a control.</summary>
    private const double Half = 0.5;

    /// <summary>How far below the top of the page canvas the stamp is placed.</summary>
    private const double StampTop = 120;

    /// <summary>The width of the area checked for the drawn stamp, in pixels.</summary>
    private const int StampAreaWidth = 80;

    /// <summary>The height of the area checked for the drawn stamp, in pixels.</summary>
    private const int StampAreaHeight = 24;

    /// <summary>The changed pixels that show the stamp was drawn.</summary>
    private const int MinStampPixels = 50;

    /// <summary>The stamps the click test places.</summary>
    private const int StampsPlaced = 2;

    /// <summary>The bytes in a captured pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The word on the Draft stamp.</summary>
    private const string Draft = "DRAFT";

    /// <summary>A shape's first corner.</summary>
    private static readonly PagePoint From = new(100, 100);

    /// <summary>A shape's opposite corner.</summary>
    private static readonly PagePoint To = new(220, 180);

    /// <summary>Shapes and a stamp are added in the deeper tone of the chosen colour, listed, and undone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsShapesAndPlacesStamps()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("shapes.pdf", 1)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;

        _ = await annotations.SetToolCommand.Execute(AnnotationTool.Arrow).ToTask();
        var label = annotations.ShapeLabel;
        var arrow = annotations.AddShape(0, AnnotationKind.Arrow, From, To);
        var box = annotations.AddShape(0, AnnotationKind.Rectangle, From, To);
        _ = await annotations.SetStampCommand.Execute(Draft).ToTask();
        var stampLabel = annotations.StampButtonLabel;
        var stamp = annotations.AddStamp(0, To);
        var kinds = annotations.Items.Select(static i => i.Annotation.Kind).ToArray();
        var color = annotations.Items.First(static i => i.Annotation.Kind == AnnotationKind.Rectangle).Annotation.Color;
        _ = await annotations.UndoCommand.Execute().ToTask();

        await Assert.That(label).IsEqualTo("Arrow");
        await Assert.That(stampLabel).IsEqualTo("Stamp: DRAFT");
        await Assert.That(arrow && box && stamp).IsTrue();
        await Assert.That(kinds).IsEquivalentTo([AnnotationKind.Arrow, AnnotationKind.Rectangle, AnnotationKind.Stamp]);
        await Assert.That(color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sand));
        await Assert.That(annotations.Items.Count).IsEqualTo(kinds.Length - 1);
    }

    /// <summary>Choosing a stamp from the button's menu and clicking the page places, lists and saves the stamp.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesAStampByClickingThePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("stamp.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            tab.SidebarMode = SidebarMode.Annotations;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await tab.Annotations.StartCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => view.StampButton.IsEffectivelyVisible);

            Click(window, view.StampButton);
            var menuShown = await UiWait.UntilAsync(() => view.DraftItem.IsEffectivelyVisible && TopLevel.GetTopLevel(view.DraftItem) is not null);
            await Assert.That(menuShown).IsTrue();
            Click(TopLevel.GetTopLevel(view.DraftItem)!, view.DraftItem);
            var chosen = await UiWait.UntilAsync(() => tab.Annotations.Tool == AnnotationTool.Stamp);
            await Assert.That(chosen).IsTrue();
            await Assert.That(tab.Annotations.StampButtonLabel).IsEqualTo("Stamp: DRAFT");

            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var target = canvas.TranslatePoint(new(canvas.Bounds.Width * Half, StampTop), window)!.Value;
            var hub = test.Services.RenderHub;
            _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
            var before = CaptureArea(window, target);

            window.MouseDown(target, MouseButton.Left);
            window.MouseUp(target, MouseButton.Left);
            _ = await UiWait.UntilAsync(() => tab.Annotations.Items.Count > 0);
            var drawn = await UiWait.UntilAsync(() => hub.Scheduler.QueueLength == 0 && Changed(before, CaptureArea(window, target)) > MinStampPixels);

            // A second click places a second stamp, so the page still takes input after the first.
            window.MouseDown(target, MouseButton.Left);
            window.MouseUp(target, MouseButton.Left);
            _ = await UiWait.UntilAsync(() => tab.Annotations.Items.Count > 1);

            var saved = Path.Combine(test.Directory, "stamped.pdf");
            var wrote = tab.Save(saved);

            await Assert.That(tab.Annotations.Items.Select(static i => i.Annotation.Kind)).IsEquivalentTo([AnnotationKind.Stamp, AnnotationKind.Stamp]);
            await Assert.That(tab.Annotations.Items[0].Annotation.Contents).IsEqualTo(Draft);
            await Assert.That(view.AnnotationList.ItemCount).IsEqualTo(StampsPlaced);
            await Assert.That(drawn).IsTrue();
            await Assert.That(wrote).IsTrue();
            await Assert.That(ReadKinds(test, saved)).IsEquivalentTo([AnnotationKind.Stamp, AnnotationKind.Stamp]);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A stamp chosen without a word keeps the current word, so placing it still works.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsTheStampWordWhenNoneIsGiven()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("blank-stamp.pdf", 1)]);
        var annotations = main.SelectedTab!.Annotations;

        _ = await annotations.SetStampCommand.Execute(null).ToTask();
        var placed = annotations.AddStamp(0, To);

        await Assert.That(annotations.Tool).IsEqualTo(AnnotationTool.Stamp);
        await Assert.That(annotations.StampButtonLabel).IsEqualTo("Stamp: APPROVED");
        await Assert.That(placed).IsTrue();
    }

    /// <summary>Menu items that pass a fixed value to their command keep that value after binding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MenuItemsKeepTheirCommandParameters()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("menus.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await UiWait.UntilAsync(() => view.DraftItem.CommandParameter is not null);
            object?[] parameters =
            [
                view.DraftItem.CommandParameter,
                view.RectangleItem.CommandParameter,
                view.RedItem.CommandParameter,
                view.Zoom100Item.CommandParameter,
                view.SingleLayoutItem.CommandParameter,
            ];

            object?[] expected = [Draft, AnnotationTool.Rectangle, "Red", "100", "Single"];

            await Assert.That(parameters).IsEquivalentTo(expected);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The shape and stamp buttons are on the annotation bar and every control on it has a name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsShapeAndStampButtons()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("bar.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await main.SelectedTab!.Annotations.StartCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<Button>().Any(static b => b.Name == "StampButton" && b.IsVisible));

            await Assert.That(window.GetVisualDescendants().OfType<Button>().Any(static b => b.Name == "ShapeButton" && b.IsVisible)).IsTrue();
            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(window))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Clicks the middle of a control with the left mouse button.</summary>
    /// <param name="root">The top level holding the control.</param>
    /// <param name="control">The control.</param>
    private static void Click(TopLevel root, Control control)
    {
        var centre = control.TranslatePoint(new(control.Bounds.Width * Half, control.Bounds.Height * Half), root)!.Value;
        root.MouseDown(centre, MouseButton.Left);
        root.MouseUp(centre, MouseButton.Left);
    }

    /// <summary>Copies the window pixels in the area where a stamp placed at a point is drawn.</summary>
    /// <param name="window">The window.</param>
    /// <param name="corner">The stamp's top-left corner in window coordinates.</param>
    /// <returns>The BGRA pixels, row by row.</returns>
    private static byte[] CaptureArea(Window window, Point corner)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        var scale = locked.Size.Width / window.Bounds.Width;
        var left = (int)(corner.X * scale);
        var top = (int)(corner.Y * scale);
        const int rowBytes = StampAreaWidth * BytesPerPixel;
        var area = new byte[rowBytes * StampAreaHeight];
        for (var row = 0; row < StampAreaHeight; row++)
        {
            Marshal.Copy(locked.Address + ((top + row) * locked.RowBytes) + (left * BytesPerPixel), area, row * rowBytes, rowBytes);
        }

        return area;
    }

    /// <summary>Counts the pixels that differ between two captures of the same area.</summary>
    /// <param name="before">The first capture.</param>
    /// <param name="after">The second capture.</param>
    /// <returns>The changed pixels.</returns>
    private static int Changed(byte[] before, byte[] after)
    {
        var changed = 0;
        for (var i = 0; i < before.Length; i += BytesPerPixel)
        {
            if (!before.AsSpan(i, BytesPerPixel).SequenceEqual(after.AsSpan(i, BytesPerPixel)))
            {
                changed++;
            }
        }

        return changed;
    }

    /// <summary>Opens a saved file and reads the kinds of annotation on its first page.</summary>
    /// <param name="test">The services.</param>
    /// <param name="path">The file.</param>
    /// <returns>The kinds.</returns>
    private static AnnotationKind[] ReadKinds(TestServices test, string path)
    {
        using var document = test.Services.Engine.Open(path, null);
        var annotations = new List<PageAnnotation>();
        ((IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!).GetAnnotations(0, annotations);
        return [.. annotations.Select(static a => a.Kind)];
    }
}

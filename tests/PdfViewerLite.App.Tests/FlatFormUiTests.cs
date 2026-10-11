// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Forms.Detection;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Headless checks of typing on a printed form with no fillable fields: the lines, boxes and the row of character
/// boxes are found, a click inside one starts typing fitted to it, and holding Alt places text freely instead.
/// </summary>
public sealed class FlatFormUiTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The character boxes in the printed row.</summary>
    private const int CombBoxes = 6;

    /// <summary>The printed row's width, in points.</summary>
    private const float CombWidth = 120;

    /// <summary>The printed box's width, in points.</summary>
    private const float BoxWidth = 200;

    /// <summary>The space kept inside both sides of a box, in points.</summary>
    private const float BoxPaddings = 4;

    /// <summary>How close widths must be, in points.</summary>
    private const float Slack = 3;

    /// <summary>The characters typed into the row of character boxes, one per box.</summary>
    private const string CombText = "AB1234";

    /// <summary>The least share of the row the typed letters may span: they spread one to a box, not bunched.</summary>
    private const double SpreadLow = 0.85;

    /// <summary>The most share of the row the typed letters may span.</summary>
    private const double SpreadHigh = 1.15;

    /// <summary>The places the form has: a line, a box and a row of character boxes.</summary>
    private const int Places = 3;

    /// <summary>A point inside the row of character boxes.</summary>
    private static readonly PagePoint InComb = new(150, 330);

    /// <summary>A point inside the box.</summary>
    private static readonly PagePoint InBox = new(200, 252);

    /// <summary>Clicking in the row of character boxes types one character per box across the row.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClickInCombSnaps()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("printed.pdf", TestPdf.CreateFlatForm())]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            var found = await FindPlacesAsync(annotations);
            Click(window, ToWindow(canvas, window, InComb), RawInputModifiers.None);
            var typing = await UiWait.UntilAsync(() => annotations.IsEditingText);
            var edit = annotations.TextEdit;
            var cells = annotations.CombCells;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            _ = await UiWait.UntilAsync(() => view.PageTextEditor.IsKeyboardFocusWithin);
            window.KeyTextInput(CombText);
            window.UpdateLayout();
            var presenter = view.PageTextEditor.GetVisualDescendants().OfType<TextPresenter>().Single();
            var spread = (view.PageTextEditor.Padding.Left + presenter.TextLayout.WidthIncludingTrailingWhitespace) / (CombWidth * canvas.PageScale);
            var oneLine = presenter.TextLayout.TextLines.Count;
            var scrolled = view.PageTextEditor.GetVisualDescendants().OfType<ScrollViewer>().Single().Offset.X;

            await Assert.That(found).IsTrue();
            await Assert.That(view.PageTextEditor.Text).IsEqualTo(CombText);
            await Assert.That(spread).IsGreaterThan(SpreadLow).And.IsLessThan(SpreadHigh);
            await Assert.That(oneLine).IsEqualTo(1);
            await Assert.That(scrolled).IsEqualTo(0);
            await Assert.That(typing).IsTrue();
            await Assert.That(cells).IsEqualTo(CombBoxes);
            await Assert.That(Math.Abs(edit!.WrapWidth - CombWidth)).IsLessThan(Slack);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Clicking in a box types inside its edges; holding Alt types where the pointer is, with no snapping.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AltClickPlacesFreely()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("printed.pdf", TestPdf.CreateFlatForm())]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var annotations = tab.Annotations;
            var canvas = await ReadyAsync(window, tab);
            _ = await FindPlacesAsync(annotations);
            Click(window, ToWindow(canvas, window, InBox), RawInputModifiers.None);
            _ = await UiWait.UntilAsync(() => annotations.IsEditingText);
            var snapped = annotations.TextEdit;
            annotations.CancelText();
            Click(window, ToWindow(canvas, window, InComb), RawInputModifiers.Alt);
            _ = await UiWait.UntilAsync(() => annotations.IsEditingText);
            var free = annotations.TextEdit;

            await Assert.That(snapped).IsNotNull();
            await Assert.That(Math.Abs(snapped!.WrapWidth - (BoxWidth - BoxPaddings))).IsLessThan(Slack);
            await Assert.That(free).IsNotNull();
            await Assert.That(annotations.CombCells).IsEqualTo(0);
            await Assert.That(Math.Abs(free!.WrapWidth - CombWidth)).IsGreaterThan(Slack);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Turns on the Text tool and waits for the first page's places to write.</summary>
    /// <param name="annotations">The annotations.</param>
    /// <returns><see langword="true"/> when the line, the box and the row were found.</returns>
    private static async Task<bool> FindPlacesAsync(AnnotationsViewModel annotations)
    {
        _ = await annotations.SetToolCommand.Execute(AnnotationTool.Text).ToTask();
        return await UiWait.UntilAsync(() =>
        {
            var regions = annotations.RegionsOn(0);
            return regions.Length == Places && Array.Exists(regions, static r => r.Kind == FormRegionKind.Comb);
        });
    }

    /// <summary>Waits for the first page to be drawn.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(Window window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        var hub = tab.RenderHub;
        _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Converts a point on the first page to window coordinates.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="window">The window.</param>
    /// <param name="point">The page point.</param>
    /// <returns>The window point.</returns>
    private static Point ToWindow(PageCanvas canvas, Window window, PagePoint point)
    {
        window.UpdateLayout();
        return canvas.TranslatePoint(canvas.PageToCanvas(0, point), window)!.Value;
    }

    /// <summary>Clicks a window point with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point.</param>
    /// <param name="modifiers">The keys held.</param>
    private static void Click(Window window, Point point, RawInputModifiers modifiers)
    {
        window.MouseDown(point, MouseButton.Left, modifiers);
        window.MouseUp(point, MouseButton.Left, modifiers);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the caret and freehand highlighter tools on the annotation bar: choosing them, clicking and dragging on the page, listing and saving.</summary>
public sealed class CaretAndHighlighterToolTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Half, for finding the middle of a control.</summary>
    private const double Half = 0.5;

    /// <summary>How far below the top of the page canvas the tools are used.</summary>
    private const double ToolTop = 160;

    /// <summary>How far the highlighter pen is dragged across the page.</summary>
    private const double DragLength = 120;

    /// <summary>The steps a drag is made in.</summary>
    private const int DragSteps = 6;

    /// <summary>The name of the caret button.</summary>
    private const string CaretButtonName = "CaretTool";

    /// <summary>The name of the highlighter button.</summary>
    private const string HighlighterButtonName = "FreehandHighlightTool";

    /// <summary>Choosing the Caret tool and clicking the page adds, lists and saves a caret.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClickingThePageWithTheCaretToolAddsACaret()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("caret.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await tab.Annotations.StartCommand.Execute().ToTask();
            var button = await FindButtonAsync(window, CaretButtonName);
            Click(window, button);
            var chosen = await UiWait.UntilAsync(() => tab.Annotations.Tool == AnnotationTool.Caret);
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var target = canvas.TranslatePoint(new(canvas.Bounds.Width * Half, ToolTop), window)!.Value;

            window.MouseDown(target, MouseButton.Left);
            window.MouseUp(target, MouseButton.Left);
            var added = await UiWait.UntilAsync(() => tab.Annotations.Items.Count > 0);
            var saved = Path.Combine(test.Directory, "caret-saved.pdf");
            var wrote = tab.Save(saved);

            await Assert.That(chosen).IsTrue();
            await Assert.That(added).IsTrue();
            await Assert.That(tab.Annotations.Items.Select(static i => i.Annotation.Kind)).IsEquivalentTo([AnnotationKind.Caret]);
            await Assert.That(wrote).IsTrue();
            await Assert.That(ReadKinds(test, saved)).IsEquivalentTo([AnnotationKind.Caret]);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Choosing the Highlighter and dragging across the page adds, lists and saves a freehand highlight in the chosen colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DraggingWithTheHighlighterAddsAFreehandHighlight()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("highlighter.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await tab.Annotations.StartCommand.Execute().ToTask();
            var button = await FindButtonAsync(window, HighlighterButtonName);
            Click(window, button);
            var chosen = await UiWait.UntilAsync(() => tab.Annotations.Tool == AnnotationTool.FreehandHighlight);
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var start = canvas.TranslatePoint(new(canvas.Bounds.Width * Half, ToolTop), window)!.Value;

            window.MouseDown(start, MouseButton.Left);
            for (var step = 1; step <= DragSteps; step++)
            {
                window.MouseMove(start + new Vector(DragLength * step / DragSteps, 0), RawInputModifiers.LeftMouseButton);
            }

            window.MouseUp(start + new Vector(DragLength, 0), MouseButton.Left);
            var added = await UiWait.UntilAsync(() => tab.Annotations.Items.Count > 0);
            var saved = Path.Combine(test.Directory, "highlighter-saved.pdf");
            var wrote = tab.Save(saved);

            await Assert.That(chosen).IsTrue();
            await Assert.That(added).IsTrue();
            await Assert.That(tab.Annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.FreehandHighlight);
            await Assert.That(tab.Annotations.Items[0].Annotation.Color).IsEqualTo(tab.Annotations.Color);
            await Assert.That(wrote).IsTrue();
            await Assert.That(ReadKinds(test, saved)).IsEquivalentTo([AnnotationKind.FreehandHighlight]);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Both buttons are named for screen readers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ButtonsHaveNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("names.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await main.SelectedTab!.Annotations.StartCommand.Execute().ToTask();
            var caret = await FindButtonAsync(window, CaretButtonName);
            var pen = await FindButtonAsync(window, HighlighterButtonName);

            await Assert.That(Avalonia.Automation.AutomationProperties.GetName(caret)).IsEqualTo("Caret");
            await Assert.That(Avalonia.Automation.AutomationProperties.GetName(pen)).IsEqualTo("Highlighter");
            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(window))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits for a visible tool button on the annotation bar.</summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The button's name.</param>
    /// <returns>The button.</returns>
    private static async Task<ToggleButton> FindButtonAsync(Window window, string name)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<ToggleButton>().Any(b => b.Name == name && b.IsEffectivelyVisible));
        return window.GetVisualDescendants().OfType<ToggleButton>().First(b => b.Name == name);
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

    /// <summary>Opens a saved file and reads the kinds of annotation on its first page.</summary>
    /// <param name="test">The services.</param>
    /// <param name="path">The file.</param>
    /// <returns>The kinds.</returns>
    private static AnnotationKind[] ReadKinds(TestServices test, string path)
    {
        using var document = test.Services.Engine.Open(path, null);
        var annotations = new List<PageAnnotation>();
        DocumentFeature<IAnnotationEditor>.Cast(document)!.GetAnnotations(0, annotations);
        return [.. annotations.Select(static a => a.Kind)];
    }
}

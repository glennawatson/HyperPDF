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
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the quick format row appears beside the text being typed and works without taking the keyboard from it.</summary>
public sealed class TextFormatBubbleUiTests
{
    /// <summary>The width of the test window.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The height of the test window.</summary>
    private const double WindowHeight = 800;

    /// <summary>Half of a length, to find a middle.</summary>
    private const double Half = 0.5;

    /// <summary>The words typed.</summary>
    private const string Words = "Quick words";

    /// <summary>Where text is typed.</summary>
    private static readonly Core.Geometry.PagePoint TypeAt = new(120, 300);

    /// <summary>The row shows above the text while it is typed, Bold from it styles the text and keeps the caret in the text, and the row goes when the text is finished.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowShowsAboveTheTextAndKeepsTheCaret()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("bubble.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await ReadyAsync(window, tab);
            var editorView = window.GetVisualDescendants().OfType<PageTextEditorView>().Single();
            var bubble = editorView.GetVisualDescendants().OfType<TextFormatBubbleView>().Single();
            var hiddenBefore = !bubble.IsVisible;
            _ = tab.Annotations.BeginText(0, TypeAt, 0);
            var editor = editorView.FindNamed<TextBox>("PageTextEditor");
            _ = await UiWait.UntilAsync(() => editor.IsKeyboardFocusWithin);
            window.KeyTextInput(Words);
            var bold = bubble.FindNamed<ToggleButton>("BoldToggle");
            _ = await UiWait.UntilAsync(() => bold.Bounds.Width > 0);
            var above = bubble.TranslatePoint(default, window)!.Value.Y < editor.TranslatePoint(default, window)!.Value.Y;

            Click(window, bold.TranslatePoint(new(bold.Bounds.Width * Half, bold.Bounds.Height * Half), window)!.Value);

            var styled = await UiWait.UntilAsync(() => tab.Annotations.IsBold && editor.FontWeight == Avalonia.Media.FontWeight.Bold);
            var kept = editor.IsKeyboardFocusWithin && tab.Annotations.IsEditingText && editor.Text == Words;
            _ = tab.Annotations.CommitText();
            var hiddenAfter = await UiWait.UntilAsync(() => !bubble.IsVisible);

            using (Assert.Multiple())
            {
                await Assert.That(hiddenBefore).IsTrue();
                await Assert.That(above).IsTrue();
                await Assert.That(styled).IsTrue();
                await Assert.That(kept).IsTrue();
                await Assert.That(hiddenAfter).IsTrue();
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits until the document and page canvas are ready.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> ReadyAsync(MainWindow window, DocumentTabViewModel tab)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        _ = await tab.Annotations.StartCommand.Execute().ToTask();
        var hub = tab.RenderHub;
        _ = await UiWait.UntilAsync(() => hub.Cache.Count > 0 && hub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Clicks a window point with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="point">The point.</param>
    private static void Click(MainWindow window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}

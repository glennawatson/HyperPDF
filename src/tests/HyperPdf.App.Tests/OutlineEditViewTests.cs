// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks the bookmark editor in a headless document view: labelled buttons, pointer clicks and keyboard shortcuts.</summary>
public sealed class OutlineEditViewTests
{
    /// <summary>The number of pages, and so of bookmarks, in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Half, to find the middle of a control.</summary>
    private const double Half = 0.5;

    /// <summary>The dispatcher rounds that let a view finish opening.</summary>
    private const int Settle = 10;

    /// <summary>The title typed for a renamed bookmark.</summary>
    private const string Typed = "Typed By Keyboard";

    /// <summary>The names screen readers hear for the buttons, with the control that carries each.</summary>
    private static readonly ButtonName[] ButtonNames =
    [
        new("AddButton", "Add bookmark"),
        new("RenameButton", "Rename bookmark"),
        new("RemoveButton", "Remove bookmark"),
        new("MoveUpButton", "Move bookmark up"),
        new("MoveDownButton", "Move bookmark down"),
        new("IndentButton", "Nest bookmark under the one above"),
        new("OutdentButton", "Move bookmark out of its parent"),
        new("UndoButton", "Undo document edit"),
    ];

    /// <summary>Every action is a labelled button with a name for screen readers and help text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsLabelledButtonsWithNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-names.pdf", Pages)]);
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            main.SelectedTab!.SidebarMode = SidebarMode.Outline;
            var tools = await FindToolsAsync(window);

            foreach (var (control, name) in ButtonNames)
            {
                var button = tools.FindControl<Button>(control)!;
                using (Assert.Multiple())
                {
                    await Assert.That(AutomationProperties.GetName(button)).IsEqualTo(name);
                    await Assert.That(button.Content as string).IsNotNull().And.IsNotEmpty();
                    await Assert.That(ControlHelp.GetText(button)).IsNotNull().And.IsNotEmpty();
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Clicking the buttons with the pointer removes a bookmark, and clicking Undo puts it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesWithTheMouseAndUndoes()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-mouse.pdf", Pages)]);
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            tab.SidebarMode = SidebarMode.Outline;
            var tools = await FindToolsAsync(window);
            tab.SelectedOutlineItem = tab.Outline[1];
            var remove = tools.FindControl<Button>("RemoveButton")!;
            _ = await UiWait.UntilAsync(() => remove.IsEnabled);

            Click(window, remove);
            _ = await UiWait.UntilAsync(() => tab.Outline.Count == Pages - 1);
            var undo = tools.FindControl<Button>("UndoButton")!;
            _ = await UiWait.UntilAsync(() => undo.IsEnabled);
            Click(window, undo);
            _ = await UiWait.UntilAsync(() => tab.Outline.Count == Pages);

            await Assert.That(tab.Outline.Count).IsEqualTo(Pages);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The keys in the bookmark list rename, move and remove the chosen bookmark, and the list keeps the focus.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsWithTheKeyboard()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outline-keys.pdf", Pages)]);
        var window = new MainWindow { ViewModel = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            tab.SidebarMode = SidebarMode.Outline;
            _ = await FindToolsAsync(window);

            // The view puts the focus on the pages once it has opened, so let that settle before focusing the list.
            await UiWait.PumpAsync(Settle);
            using var answer = tab.OutlineEditing.PromptInteraction.RegisterHandler(static context => context.SetOutput(Typed));
            var first = tab.Outline[0];
            var second = tab.Outline[1];
            tab.SelectedOutlineItem = first;
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single(static t => t.Name == "OutlineTree");
            _ = await UiWait.UntilAsync(() => tab.OutlineEditing.CanChange && tree.ContainerFromItem(first) is TreeViewItem { IsEffectivelyVisible: true } row && row.Focus());

            window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, null);
            _ = await UiWait.UntilAsync(() => tab.Outline[0].Title == Typed);

            // The renamed list has new rows; the chosen one holds the focus again, so the next key still reaches the list.
            window.KeyPress(Key.Down, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.ArrowDown, null);
            _ = await UiWait.UntilAsync(() => tab.Outline[1].Id == first.Id);
            var movedDown = tab.Outline[0].Id == second.Id;

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
            _ = await UiWait.UntilAsync(() => tab.Outline.Count == Pages - 1);

            using (Assert.Multiple())
            {
                await Assert.That(movedDown).IsTrue();
                await Assert.That(tab.Outline.Any(item => item.Id == first.Id)).IsFalse();
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Clicks the middle of a control with the left mouse button.</summary>
    /// <param name="window">The window.</param>
    /// <param name="control">The control.</param>
    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new(control.Bounds.Width * Half, control.Bounds.Height * Half), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    /// <summary>Waits for the bookmark tools to be shown.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The tools.</returns>
    private static async Task<OutlineEditView> FindToolsAsync(MainWindow window)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<OutlineEditView>().Any(static view => view.IsEffectivelyVisible && view.IsLoaded));
        return window.GetVisualDescendants().OfType<OutlineEditView>().Single();
    }

    /// <summary>A button's control name and the name a screen reader hears.</summary>
    /// <param name="Control">The control name.</param>
    /// <param name="Name">The accessible name.</param>
    private sealed record ButtonName(string Control, string Name);
}

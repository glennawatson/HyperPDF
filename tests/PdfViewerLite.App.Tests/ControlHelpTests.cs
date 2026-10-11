// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks every control explains what it does, both as a tooltip and to screen readers.</summary>
public sealed class ControlHelpTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>What comes between an explanation and its shortcut.</summary>
    private const string ShortcutJoin = " Shortcut: ";

    /// <summary>The fewest words an explanation may have, so a bare key name or one-word label fails.</summary>
    private const int MinWords = 3;

    /// <summary>Fewer controls than this in the main window means part of the tree was not walked.</summary>
    private const int MinMainWindowControls = 100;

    /// <summary>Fewer controls than this across the dialogs means part of a tree was not walked.</summary>
    private const int MinDialogControls = 40;

    /// <summary>Every control in the main window, the document view, its tool bars, menus and sidebar is explained.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MainWindowControlsAreExplained()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("help.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var tab = main.SelectedTab!;
            tab.Search.IsOpen = true;
            tab.Annotations.IsAnnotating = true;
            tab.FocusMode.IsOn = true;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<FocusView>().Any(static f => f.IsVisible));

            var controls = InteractiveControls(window);
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();

            await Assert.That(string.Join(", ", Unexplained(controls))).IsEqualTo(string.Empty);
            await Assert.That(controls.Count).IsGreaterThan(MinMainWindowControls);
            await Assert.That(controls.Contains(view.FindControl<MenuItem>("PresentItem")!)).IsTrue();
            await Assert.That(controls.Contains(view.FindControl<CheckBox>("WordMarkCheck")!)).IsTrue();
            await Assert.That(controls.Contains(view.FindControl<FocusView>("FocusPane")!.FindControl<Slider>("SizeSlider")!)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control of each dialog is explained.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogControlsAreExplained()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("help-dialogs.pdf", Pages)]);
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        Window[] windows =
        [
            new PreferencesWindow { ViewModel = new(test.Services) },
            new PrintPreviewWindow { ViewModel = preview, Width = WindowWidth, Height = WindowHeight },
            new FolderSearchWindow { ViewModel = main.FolderSearch },
            new ConfirmWindow { ViewModel = new(new("Delete?", "This cannot be undone.", "Delete")) },
            new PromptWindow { ViewModel = new(new TextPrompt("Note", "Write a note", string.Empty, "Save", false)) },
            new PropertiesWindow(),
            new LicencesWindow { ViewModel = new(PdfViewerLite.App.Services.LicenceNotices.Load()) },
            new SignaturesWindow(),
            new CertificateSignWindow(),
            new OptimizeCopyWindow(),
            new Window { Content = new LayerItemView() },
        ];
        foreach (var window in windows)
        {
            window.Show();
        }

        try
        {
            _ = await UiWait.UntilAsync(() => Array.TrueForAll(windows, static w => w.IsVisible));
            var missing = new List<string>();
            var count = 0;
            foreach (var window in windows)
            {
                var controls = InteractiveControls(window);
                count += controls.Count;
                missing.AddRange(Unexplained(controls).Select(name => $"{window.GetType().Name}.{name}"));
            }

            await Assert.That(string.Join(", ", missing)).IsEqualTo(string.Empty);
            await Assert.That(count).IsGreaterThan(MinDialogControls);
        }
        finally
        {
            foreach (var window in windows)
            {
                window.Close();
            }
        }
    }

    /// <summary>The automation peer, which feeds UIA, NSAccessibility and AT-SPI, gives screen readers the explanation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutomationPeersExposeTheExplanation()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("help-peers.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var next = ControlAutomationPeer.CreatePeerForElement(view.FindControl<Button>("NextPageButton")!);
            var back = ControlAutomationPeer.CreatePeerForElement(view.FindControl<Button>("BackButton")!);
            var pageBox = ControlAutomationPeer.CreatePeerForElement(view.FindControl<TextBox>("PageBox")!);

            await Assert.That(next.GetHelpText()).IsEqualTo(Descriptions.NextPage);
            await Assert.That(next.GetName()).IsEqualTo("Next page");
            await Assert.That(back.GetHelpText()).IsEqualTo(Descriptions.Back);
            await Assert.That(pageBox.GetHelpText()).IsEqualTo(Descriptions.PageNumber);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Changing a control's explanation updates its tooltip and what screen readers hear together.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplanationKeepsTooltipAndHelpTextTogether()
    {
        var button = new Button { Content = "Zoom" };
        ControlHelp.SetText(button, Descriptions.ZoomIn);
        var peer = ControlAutomationPeer.CreatePeerForElement(button);

        await Assert.That(ToolTip.GetTip(button)).IsEqualTo(Descriptions.ZoomIn);
        await Assert.That(peer.GetHelpText()).IsEqualTo(Descriptions.ZoomIn);

        ControlHelp.SetText(button, Descriptions.ZoomOut);

        await Assert.That(ToolTip.GetTip(button)).IsEqualTo(Descriptions.ZoomOut);
        await Assert.That(peer.GetHelpText()).IsEqualTo(Descriptions.ZoomOut);
        await Assert.That(ControlHelp.GetText(button)).IsEqualTo(Descriptions.ZoomOut);
    }

    /// <summary>Finds the controls a person operates in a window, including those in drop-down menus and flyouts.</summary>
    /// <param name="root">The window.</param>
    /// <returns>The controls.</returns>
    private static HashSet<Control> InteractiveControls(Control root)
    {
        var found = new HashSet<Control>();
        var pending = new Stack<Control>();
        pending.Push(root);
        var seen = new HashSet<Control>();
        while (pending.TryPop(out var control))
        {
            if (!seen.Add(control))
            {
                continue;
            }

            // Controls inside another control's template (scroll bar buttons, spinner buttons) are explained by their owner.
            if (IsInteractive(control) && control.TemplatedParent is null)
            {
                _ = found.Add(control);
            }

            foreach (var child in control.GetVisualChildren().OfType<Control>().Concat(control.GetLogicalChildren().OfType<Control>()))
            {
                pending.Push(child);
            }

            // Flyouts are not in the tree until opened.
            switch ((control as Button)?.Flyout)
            {
                case MenuFlyout menu:
                {
                    foreach (var item in menu.Items.OfType<Control>())
                    {
                        pending.Push(item);
                    }

                    break;
                }

                case Flyout { Content: Control content }:
                {
                    pending.Push(content);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>Lists the controls whose tooltip or help text is missing, differs, or gives only a shortcut.</summary>
    /// <param name="controls">The controls.</param>
    /// <returns>Each failing control's name or type.</returns>
    private static IEnumerable<string> Unexplained(IEnumerable<Control> controls) =>
        controls.Where(static control => !IsExplained(control)).Select(static control => control.Name ?? control.GetType().Name).Order(StringComparer.Ordinal);

    /// <summary>Determines whether a control's tooltip explains it in a sentence and matches its help text.</summary>
    /// <param name="control">The control.</param>
    /// <returns><see langword="true"/> when explained.</returns>
    private static bool IsExplained(Control control)
    {
        if (ToolTip.GetTip(control) is not string { Length: > 0 } tip || AutomationProperties.GetHelpText(control) != tip)
        {
            return false;
        }

        var shortcut = tip.IndexOf(ShortcutJoin, StringComparison.Ordinal);
        var explanation = shortcut < 0 ? tip : tip[..shortcut];
        return explanation.EndsWith('.')
            && explanation.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= MinWords
            && !explanation.Contains("Ctrl+", StringComparison.Ordinal)
            && !explanation.Contains("Alt+", StringComparison.Ordinal);
    }

    /// <summary>Determines whether a control is one a person operates.</summary>
    /// <param name="control">The control.</param>
    /// <returns><see langword="true"/> for buttons, toggles, check boxes, menu items, lists, text fields, sliders and splitters.</returns>
    private static bool IsInteractive(Control control) =>
        control is Button or MenuItem or ComboBox or TextBox or Slider or NumericUpDown or GridSplitter;
}

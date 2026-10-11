// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks every control can be reached by screen readers and the keyboard.</summary>
public sealed class AccessibilityTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The most tab stops followed before giving up.</summary>
    private const int MaxTabStops = 400;

    /// <summary>The rounding allowed when comparing where controls sit, in pixels.</summary>
    private const double LayoutTolerance = 1;

    /// <summary>The prefix of a fallback name made from a control's type.</summary>
    private const string AvaloniaNamespace = "Avalonia.";

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>A narrow window that makes the document actions wrap.</summary>
    private const double NarrowWindowWidth = 720;

    /// <summary>Every button, toggle, list, text field, check box and slider has a name a screen reader can say.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryControlHasAName()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("names.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            tab.ReadAloud.IsOpen = true;
            tab.Search.IsOpen = true;
            tab.Annotations.IsAnnotating = true;
            tab.FocusMode.IsOn = true;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<FocusView>().Any(static f => f.IsVisible));

            var unnamed = Unnamed(window);

            await Assert.That(string.Join(", ", unnamed)).IsEqualTo(string.Empty);
            tab.ReadAloud.IsOpen = false;
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control of the Preferences and Print windows has a name a screen reader can say.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogControlsHaveNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("dialogs.pdf", Pages)]);
        var preferences = new PreferencesWindow { ViewModel = new(test.Services) };
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var print = new PrintPreviewWindow { ViewModel = preview, Width = WindowWidth, Height = WindowHeight };
        preferences.Show();
        print.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => print.IsVisible && preferences.IsVisible);

            await Assert.That(string.Join(", ", Unnamed(preferences))).IsEqualTo(string.Empty);
            await Assert.That(string.Join(", ", Unnamed(print))).IsEqualTo(string.Empty);
        }
        finally
        {
            preferences.Close();
            print.Close();
        }
    }

    /// <summary>Verifies that printing and named document groups are visible without opening an overflow menu.</summary>
    /// <param name="width">The window width.</param>
    /// <param name="style">Whether toolbar icons have labels.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(WindowWidth, ToolbarStyle.TextBesideIcons)]
    [Arguments(NarrowWindowWidth, ToolbarStyle.TextBesideIcons)]
    [Arguments(NarrowWindowWidth, ToolbarStyle.IconsOnly)]
    public async Task DocumentActionsHaveVisibleLabels(double width, ToolbarStyle style)
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("actions.pdf", Pages)]);
        test.Services.Settings.ToolbarStyle = style;
        test.Services.ApplySettings();
        DesktopThemeApplier.Apply(Application.Current!, test.Services.CurrentTheme);
        var window = new MainWindow { DataContext = main, Width = width, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            var print = view.FindControl<Button>("PrintItem")!;
            await Assert.That(print.IsEffectivelyVisible).IsTrue();
            await Assert.That(ControlAutomationPeer.CreatePeerForElement(print).GetName()).IsEqualTo("Print");
            foreach (var name in new[] { "ViewMenuButton", "DocumentMenuButton", "ToolsMenuButton" })
            {
                var button = view.FindControl<Button>(name)!;
                await Assert.That(button.IsEffectivelyVisible).IsTrue();
                await Assert.That(button.Content is string { Length: > 0 }).IsTrue();
                var origin = button.TranslatePoint(default, window)!.Value;
                await Assert.That(origin.X + button.Bounds.Width).IsLessThanOrEqualTo(window.Width);
            }
        }
        finally
        {
            window.Close();
            DesktopThemeApplier.Apply(Application.Current!, ThemeResolver.Resolve(new(), null));
        }
    }

    /// <summary>
    /// Tab reaches every visible, enabled control and the pages, in order through the tool bar, and comes back round
    /// without getting stuck.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabReachesEveryControl()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keys.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);

            // Back is only enabled, and so only reachable, once there is a jump to return from.
            main.SelectedTab!.GoToPage(1);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var visited = TabStops(window);

            var missed = new List<string>();
            foreach (var visual in view.GetVisualDescendants())
            {
                if (IsInteractive(visual) && visual is Control { TemplatedParent: null, IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control && !visited.Contains(control))
                {
                    missed.Add(control.Name ?? control.GetType().Name);
                }
            }

            var canvas = view.GetVisualDescendants().OfType<Controls.PageCanvas>().Single();
            await Assert.That(string.Join(", ", missed)).IsEqualTo(string.Empty);
            await Assert.That(visited.Contains(canvas)).IsTrue();
            await Assert.That(visited.IndexOf(Find(view, "SidebarToggle"))).IsLessThan(visited.IndexOf(Find(view, "BackButton")));
            await Assert.That(visited.IndexOf(Find(view, "FocusToggle"))).IsLessThan(visited.IndexOf(Find(view, "ReadAloudToggle")));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>
    /// Tab follows reading order: along each row from left to right and down the window, never back up to a row
    /// above, in the document window with its tool rows open and in the print window.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabFollowsReadingOrder()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("order.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var print = new PrintPreviewWindow { ViewModel = preview, Width = WindowWidth, Height = WindowHeight };
        try
        {
            _ = await FindDocumentViewAsync(window);
            var tab = main.SelectedTab!;
            tab.ReadAloud.IsOpen = true;
            tab.Annotations.IsAnnotating = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await Assert.That(string.Join("; ", ReadingOrderBreaks(window, TabStops(window)))).IsEqualTo(string.Empty);

            print.Show();
            _ = await UiWait.UntilAsync(() => preview.IsValid && !preview.IsBuilding);
            await Assert.That(string.Join("; ", ReadingOrderBreaks(print, TabStops(print)))).IsEqualTo(string.Empty);
        }
        finally
        {
            print.Close();
            window.Close();
        }
    }

    /// <summary>Presses Tab until focus comes back round, listing each control reached.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The controls in the order Tab reached them.</returns>
    internal static List<Avalonia.Input.IInputElement> TabStops(Window window)
    {
        var visited = new List<Avalonia.Input.IInputElement>();
        for (var step = 0; step < MaxTabStops; step++)
        {
            window.KeyPress(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Tab, null);
            window.KeyRelease(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Tab, null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var current = window.FocusManager?.GetFocusedElement();
            if (current is null || visited.Contains(current))
            {
                break;
            }

            visited.Add(current);
        }

        return visited;
    }

    /// <summary>Lists each step of Tab that goes back left along a row or up to an earlier row.</summary>
    /// <param name="window">The window.</param>
    /// <param name="stops">The controls in Tab order.</param>
    /// <returns>Each backward step, as the two controls' names.</returns>
    internal static List<string> ReadingOrderBreaks(Window window, List<Avalonia.Input.IInputElement> stops)
    {
        var breaks = new List<string>();
        for (var i = 1; i < stops.Count; i++)
        {
            if (stops[i - 1] is Control from && stops[i] is Control to && GoesBack(from, to, window))
            {
                breaks.Add($"{from.Name ?? from.GetType().Name} -> {to.Name ?? to.GetType().Name}");
            }
        }

        return breaks;
    }

    /// <summary>Lists the interactive controls without an accessible name.</summary>
    /// <param name="root">The window.</param>
    /// <returns>Each one's name or type and its tooltip.</returns>
    internal static List<string> Unnamed(Control root)
    {
        var unnamed = new List<string>();
        foreach (var visual in root.GetVisualDescendants())
        {
            // Controls inside other controls' templates (scroll bar buttons, combo box toggles) are named by their owners.
            if (!IsInteractive(visual) || visual is not Control { TemplatedParent: null } control)
            {
                continue;
            }

            var name = ControlAutomationPeer.CreatePeerForElement(control).GetName();

            // A peer with nothing better falls back to its content's type name, which a screen reader reads out as is.
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith(AvaloniaNamespace, StringComparison.Ordinal))
            {
                unnamed.Add(control.Name ?? control.GetType().Name);
            }
        }

        return unnamed;
    }

    /// <summary>Determines whether a step of Tab goes back left along a row or up to an earlier row.</summary>
    /// <param name="from">The control left.</param>
    /// <param name="to">The control reached.</param>
    /// <param name="window">The window.</param>
    /// <returns><see langword="true"/> when the step goes against reading order.</returns>
    private static bool GoesBack(Control from, Control to, Window window)
    {
        if (ScreenBounds(from, window) is not { } a || ScreenBounds(to, window) is not { } b)
        {
            return false;
        }

        var sameRow = a.Top < b.Center.Y && b.Center.Y < a.Bottom && b.Top < a.Center.Y && a.Center.Y < b.Bottom;
        return (sameRow && b.Left < a.Left - LayoutTolerance) || b.Bottom <= a.Top + LayoutTolerance;
    }

    /// <summary>Gets where a control sits in its window.</summary>
    /// <param name="control">The control.</param>
    /// <param name="window">The window.</param>
    /// <returns>The bounds, or <see langword="null"/> when it is not in the window.</returns>
    private static Rect? ScreenBounds(Control control, Window window) =>
        control.TranslatePoint(default, window) is { } origin ? new Rect(origin, control.Bounds.Size) : null;

    /// <summary>Waits for the document view to appear.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The view.</returns>
    private static async Task<DocumentView> FindDocumentViewAsync(MainWindow window)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
        return window.GetVisualDescendants().OfType<DocumentView>().Single();
    }

    /// <summary>Finds a named control.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The name.</param>
    /// <returns>The control.</returns>
    private static Control Find(Control root, string name) => root.GetVisualDescendants().OfType<Control>().Single(control => control.Name == name);

    /// <summary>Determines whether a visual is a control a person operates.</summary>
    /// <param name="visual">The visual.</param>
    /// <returns><see langword="true"/> for buttons, toggles, lists, text fields, check boxes and sliders.</returns>
    private static bool IsInteractive(Visual visual) => visual is Button or ToggleButton or ComboBox or TextBox or CheckBox or Slider;
}

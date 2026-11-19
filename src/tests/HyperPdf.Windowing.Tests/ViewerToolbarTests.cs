// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Icons;
using ReactiveUI;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the toolbar: its buttons have icons, its groups are set apart by lines, and each button runs a command of the view models.</summary>
[NotInParallel]
public sealed class ViewerToolbarTests
{
    /// <summary>A window wide enough for every button to show its name beside its icon.</summary>
    private const int WideWidth = 2400;

    /// <summary>A window too narrow for the names, as a small laptop screen is, and wide enough for the icons alone.</summary>
    private const int NarrowWidth = 1100;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages of the test document.</summary>
    private const int Pages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 5;

    /// <summary>How long one turn of the session waits for work, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How long a change is given to happen, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The fewest lines that set the toolbar's groups apart.</summary>
    private const int FewestDividers = 4;

    /// <summary>The turns of the session pumped so work the failure posted has run.</summary>
    private const int Turns = 5;

    /// <summary>The name of the edit bar's button that retypes the picked text.</summary>
    private const string RetypeName = "Retype text";

    /// <summary>The name of the fill and sign bar's button that makes initials.</summary>
    private const string InitialsName = "Initials";

    /// <summary>The height below the toolbar that the bars under it are searched in, in pixels.</summary>
    private const int BarsRoom = 200;

    /// <summary>The name of the button that saves.</summary>
    private const string SaveName = "Save";

    /// <summary>The name of the button that shows or hides the side panel.</summary>
    private const string SidePanelName = "Side panel";

    /// <summary>The name of the search bar's switch that matches capital and small letters.</summary>
    private const string MatchCaseName = "Match case";

    /// <summary>The name of the button that shows the pages larger.</summary>
    private const string ZoomInName = "Zoom in";

    /// <summary>On a wide window every toolbar button has an icon and its name, and upright lines set the groups apart on one line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToolbarButtonsHaveIconsNamesAndDividers()
    {
        var outcome = await WithTab(WideWidth, static (session, tab) =>
        {
            var bars = session.Chrome.Bars;
            var buttons = ToolbarButtons(session);
            var dividers = bars.Count(static item => item.Kind == ChromeItemKind.Separator && item.Bounds.Height > item.Bounds.Width);
            var save = bars.First(static item => item.Text == SaveName);
            var zoom = bars.First(static item => item.Text == ZoomInName);
            return new ToolbarOutcome(
                buttons.Length,
                buttons.Count(static item => item.Icon != ChromeIcon.None),
                dividers,
                save.HidesText,
                zoom.HidesText,
                buttons.Select(static item => item.Bounds.Y).Distinct().Count());
        });

        // The page number button is text alone; every other button has an icon.
        await Assert.That(outcome.WithIcons).IsEqualTo(outcome.Buttons - 1);
        await Assert.That(outcome.Dividers).IsGreaterThanOrEqualTo(FewestDividers);
        await Assert.That(outcome.SaveHidesItsName).IsFalse();
        await Assert.That(outcome.ZoomHidesItsName).IsTrue();
        await Assert.That(outcome.Lines).IsEqualTo(1);
    }

    /// <summary>On a narrow window the toolbar drops the names and stays on one line, and each button keeps its name for a screen reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NarrowWindowShowsIconsAloneOnOneLine()
    {
        var outcome = await WithTab(NarrowWidth, static (session, tab) =>
        {
            var buttons = ToolbarButtons(session);
            var save = buttons.First(static item => item.Text == SaveName);
            return new ToolbarOutcome(
                buttons.Length,
                buttons.Count(static item => item.Icon != ChromeIcon.None),
                0,
                save.HidesText,
                true,
                buttons.Select(static item => item.Bounds.Y).Distinct().Count());
        });

        await Assert.That(outcome.SaveHidesItsName).IsTrue();
        await Assert.That(outcome.Lines).IsEqualTo(1);
        await Assert.That(outcome.WithIcons).IsEqualTo(outcome.Buttons - 1);
    }

    /// <summary>The setting for icons alone hides the names even where they would fit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IconsOnlySettingHidesTheNames()
    {
        var hidden = await WithTab(WideWidth, static (session, tab) =>
        {
            session.Chrome.ShowLabels = false;
            return session.Chrome.Bars.First(static item => item.Text == SaveName).HidesText;
        });

        await Assert.That(hidden).IsTrue();
    }

    /// <summary>A press on a toolbar button runs the view model's own command: the side panel opens and the pages grow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToolbarButtonsRunViewModelCommands()
    {
        var outcome = await WithTab(WideWidth, static (session, tab) =>
        {
            var chrome = session.Chrome;
            var panel = chrome.Bars.First(static item => item.Text == SidePanelName);
            var wired = ReferenceEquals(panel.Command, tab.ToggleSidebarCommand);
            var shownBefore = tab.SidebarVisible;
            var pressed = chrome.PressControl(false, IndexOf(chrome.Bars, SidePanelName));
            var toggled = PumpUntil(session, () => tab.SidebarVisible != shownBefore);
            var on = PumpUntil(session, () => chrome.Bars.First(static item => item.Text == SidePanelName).Selected == tab.SidebarVisible);

            var zoomBefore = tab.Zoom;
            _ = chrome.PressControl(false, IndexOf(chrome.Bars, ZoomInName));
            var larger = PumpUntil(session, () => tab.Zoom > zoomBefore);

            // Nothing has been jumped to yet, so the button that goes back has no command to run and cannot be pressed.
            var back = chrome.Bars.First(static item => item.Icon == ChromeIcon.Back);
            return new PressOutcome(wired, pressed, toggled, on, larger, back.Enabled);
        });

        await Assert.That(outcome.RunsTheTabCommand).IsTrue();
        await Assert.That(outcome.Pressed).IsTrue();
        await Assert.That(outcome.PanelToggled).IsTrue();
        await Assert.That(outcome.ShowsItIsOn).IsTrue();
        await Assert.That(outcome.Larger).IsTrue();
        await Assert.That(outcome.BackEnabled).IsFalse();
    }

    /// <summary>A command that fails with nobody watching is logged and the window carries on; it does not end the program.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnobservedCommandErrorDoesNotEndTheWindow()
    {
        var survived = await WithTab(WideWidth, static (session, tab) =>
        {
            using var failing = ReactiveCommand.Create(static () => throw new InvalidOperationException("The command failed."));
            ((ICommand)failing).Execute(null);
            for (var turn = 0; turn < Turns; turn++)
            {
                _ = session.PumpOnce(PumpMilliseconds);
            }

            return tab.IsLoaded;
        });

        await Assert.That(survived).IsTrue();
    }

    /// <summary>Turning on Edit or Fill and Sign shows a bar under the toolbar, and every button of it that can run has an icon.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToolBarsAppearWithIconsWhenAToolIsTurnedOn()
    {
        var outcome = await WithTab(WideWidth, static (session, tab) =>
        {
            var chrome = session.Chrome;
            var toolbarBottom = chrome.Bars.First(static item => item.Icon == ChromeIcon.FitPage).Bounds.Bottom;
            ((ICommand)tab.ContentEdit.ToggleEditingCommand).Execute(null);
            var editShown = PumpUntil(session, () => chrome.Bars.Exists(static item => item.Text == RetypeName));
            var editButtons = BarButtons(chrome.Bars, toolbarBottom);

            ((ICommand)tab.ContentEdit.ToggleEditingCommand).Execute(null);
            var editGone = PumpUntil(session, () => chrome.Bars.TrueForAll(static item => item.Text != RetypeName));

            ((ICommand)tab.FillAndSign.ToggleCommand).Execute(null);
            var signShown = PumpUntil(session, () => chrome.Bars.Exists(static item => item.Text == InitialsName));
            var signButtons = BarButtons(chrome.Bars, toolbarBottom);
            return new BarOutcome(
                editShown,
                editButtons.Length,
                editButtons.Count(static item => item.Icon != ChromeIcon.None),
                editGone,
                signShown,
                signButtons.Length,
                signButtons.Count(static item => item.Icon != ChromeIcon.None));
        });

        await Assert.That(outcome.EditShown).IsTrue();
        await Assert.That(outcome.EditButtons).IsGreaterThan(0);
        await Assert.That(outcome.EditWithIcons).IsEqualTo(outcome.EditButtons);
        await Assert.That(outcome.EditGone).IsTrue();
        await Assert.That(outcome.SignShown).IsTrue();
        await Assert.That(outcome.SignButtons).IsGreaterThan(0);
        await Assert.That(outcome.SignWithIcons).IsEqualTo(outcome.SignButtons);
    }

    /// <summary>The search bar's switches run the search's own commands, and each shows whether it is on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindBarSwitchesRunSearchCommands()
    {
        var outcome = await WithTab(WideWidth, static (session, tab) =>
        {
            var chrome = session.Chrome;
            chrome.ShowFind();
            var opened = PumpUntil(session, () => chrome.OverlayItems.Exists(static item => item.Text == MatchCaseName));
            var button = chrome.OverlayItems.First(static item => item.Text == MatchCaseName);
            var wired = ReferenceEquals(button.Command, tab.Search.ToggleMatchCaseCommand);
            var offAtFirst = !tab.Search.MatchCase && !button.Selected;
            _ = chrome.PressControl(true, IndexOf(chrome.OverlayItems, MatchCaseName));
            var on = PumpUntil(session, () => tab.Search.MatchCase && chrome.OverlayItems.First(static item => item.Text == MatchCaseName).Selected);
            var next = chrome.OverlayItems.First(static item => item.Icon == ChromeIcon.PageDown);
            return new FindOutcome(opened, wired, offAtFirst, on, ReferenceEquals(next.Command, tab.Search.NextCommand));
        });

        await Assert.That(outcome.Opened).IsTrue();
        await Assert.That(outcome.RunsTheSearchCommand).IsTrue();
        await Assert.That(outcome.OffAtFirst).IsTrue();
        await Assert.That(outcome.TurnedOn).IsTrue();
        await Assert.That(outcome.NextRunsTheSearchCommand).IsTrue();
    }

    /// <summary>Finds the buttons of the toolbar: those on the line under the tab strip.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The buttons.</returns>
    private static ChromeItem[] ToolbarButtons(ViewerSession session)
    {
        var bars = session.Chrome.Bars;
        var menu = bars.First(static item => item.Icon == ChromeIcon.Menu);
        var fit = bars.First(static item => item.Icon == ChromeIcon.FitPage);
        return [.. bars.Where(item => item.Kind == ChromeItemKind.Button && item.Bounds.Y >= menu.Bounds.Y && item.Bounds.Y <= fit.Bounds.Y)];
    }

    /// <summary>Finds the buttons of the bars under the toolbar.</summary>
    /// <param name="bars">The controls of the bars.</param>
    /// <param name="toolbarBottom">The bottom of the toolbar.</param>
    /// <returns>The buttons.</returns>
    private static ChromeItem[] BarButtons(List<ChromeItem> bars, double toolbarBottom) =>
        [.. bars.Where(item => item.Kind == ChromeItemKind.Button && item.Bounds.Y >= toolbarBottom && item.Bounds.Y < toolbarBottom + BarsRoom)];

    /// <summary>Finds the place of the first control with a name.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="text">The control's name.</param>
    /// <returns>The place, or -1.</returns>
    private static int IndexOf(List<ChromeItem> items, string text)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Text == text)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Opens a document in a window of a width and acts on its tab.</summary>
    /// <typeparam name="T">What the action finds.</typeparam>
    /// <param name="width">The window width in pixels.</param>
    /// <param name="act">The action.</param>
    /// <returns>What the action found.</returns>
    private static async Task<T> WithTab<T>(int width, Func<ViewerSession, DocumentTabViewModel, T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-toolbar-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "view.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            _ = session.PumpOnce(PumpMilliseconds);
            return act(session, session.Main.SelectedTab!);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Pumps the session until a condition holds or the time runs out.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when the condition held in time.</returns>
    private static bool PumpUntil(ViewerSession session, Func<bool> done)
    {
        var started = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                return false;
            }

            _ = session.PumpOnce(PumpMilliseconds);
        }

        return true;
    }

    /// <summary>What the bars under the toolbar showed.</summary>
    /// <param name="EditShown">Whether the edit bar appeared.</param>
    /// <param name="EditButtons">The buttons of the edit bar.</param>
    /// <param name="EditWithIcons">The buttons of the edit bar that have an icon.</param>
    /// <param name="EditGone">Whether the edit bar went away again.</param>
    /// <param name="SignShown">Whether the fill and sign bar appeared.</param>
    /// <param name="SignButtons">The buttons of the fill and sign bar.</param>
    /// <param name="SignWithIcons">The buttons of the fill and sign bar that have an icon.</param>
    private readonly record struct BarOutcome(bool EditShown, int EditButtons, int EditWithIcons, bool EditGone, bool SignShown, int SignButtons, int SignWithIcons);

    /// <summary>What the toolbar was laid out as.</summary>
    /// <param name="Buttons">The buttons on the toolbar.</param>
    /// <param name="WithIcons">The buttons that have an icon.</param>
    /// <param name="Dividers">The upright lines between groups.</param>
    /// <param name="SaveHidesItsName">Whether the save button shows its icon alone.</param>
    /// <param name="ZoomHidesItsName">Whether the zoom button shows its icon alone.</param>
    /// <param name="Lines">The lines the buttons are on.</param>
    private readonly record struct ToolbarOutcome(int Buttons, int WithIcons, int Dividers, bool SaveHidesItsName, bool ZoomHidesItsName, int Lines);

    /// <summary>What the search bar's controls did.</summary>
    /// <param name="Opened">Whether the search bar opened.</param>
    /// <param name="RunsTheSearchCommand">Whether the match case switch carries the search's own command.</param>
    /// <param name="OffAtFirst">Whether matching case starts off and the switch shows it.</param>
    /// <param name="TurnedOn">Whether a press turned matching case on and the switch shows it.</param>
    /// <param name="NextRunsTheSearchCommand">Whether the next match button carries the search's own command.</param>
    private readonly record struct FindOutcome(bool Opened, bool RunsTheSearchCommand, bool OffAtFirst, bool TurnedOn, bool NextRunsTheSearchCommand);

    /// <summary>What pressing toolbar buttons did.</summary>
    /// <param name="RunsTheTabCommand">Whether the side panel button carries the tab's own command.</param>
    /// <param name="Pressed">Whether the press was taken.</param>
    /// <param name="PanelToggled">Whether the side panel changed.</param>
    /// <param name="ShowsItIsOn">Whether the button shows the side panel's state.</param>
    /// <param name="Larger">Whether zooming in made the pages larger.</param>
    /// <param name="BackEnabled">Whether the back button can be pressed with nowhere to go back to.</param>
    private readonly record struct PressOutcome(bool RunsTheTabCommand, bool Pressed, bool PanelToggled, bool ShowsItIsOn, bool Larger, bool BackEnabled);
}

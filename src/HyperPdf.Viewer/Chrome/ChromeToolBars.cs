// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The bars under the toolbar that are only there while they have something to say: a notice about the document, and the state of
/// a tool that is running, such as read aloud, text recognition, measuring or filling and signing. Each bar is one line of text and
/// the buttons for what can be done about it, so the state of a tool is always in view and never has to be remembered.
/// </summary>
internal static class ChromeToolBars
{
    /// <summary>The text of the button that puts a notice away, in English.</summary>
    private const string DismissText = "Dismiss";

    /// <summary>The largest percentage, which a share of work done is multiplied by.</summary>
    private const double Percent = 100;

    /// <summary>Adds the bars that have something to say now.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">The top of the first bar.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">Receives the command of each button, at the place the button names.</param>
    /// <returns>The height the bars take together.</returns>
    internal static int Add(List<ChromeItem> items, DocumentTabViewModel tab, ChromeLook look, int top, int width, List<MenuEntry> actions)
    {
        actions.Clear();
        var bars = new List<ToolBar>();
        AddNotices(bars, tab);
        AddReadingAids(bars, tab);
        AddPageTools(bars, tab);
        var height = 0;
        foreach (var bar in bars)
        {
            height += AddBar(items, bar, look, top + height, width, actions);
        }

        return height;
    }

    /// <summary>Adds the notices about the document: what it needs, what was repaired, what cannot be shown and a newer copy on disk.</summary>
    /// <param name="bars">Receives the bars.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddNotices(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        var notices = tab.Notices;
        AddNotice(bars, tab.Notice, Item(DismissText, tab.DismissNoticeCommand));
        AddNotice(bars, notices.ContentWarning, Item(DismissText, notices.DismissContentWarningCommand));
        AddNotice(bars, notices.IntegrityNotice, Item(DismissText, notices.DismissIntegrityNoticeCommand));
        AddNotice(bars, Join(notices.RepairNotice, notices.RepairDetails), Item("Details", notices.ToggleRepairDetailsCommand), Item(DismissText, notices.DismissRepairNoticeCommand));
        AddNotice(
            bars,
            Join(notices.RequirementNotice, notices.RequirementDetails),
            Item("Details", notices.ToggleRequirementDetailsCommand),
            Item(DismissText, notices.DismissRequirementNoticeCommand));
        if (tab.HasPendingReload)
        {
            bars.Add(new(UiText.Get("This file has changed on disk."), [Item("Reload", tab.ReloadCommand), Item("Keep this version", tab.DismissReloadCommand)]));
        }
    }

    /// <summary>Adds the state of the reading aids that are running: read aloud, text recognition and focus mode.</summary>
    /// <param name="bars">Receives the bars.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddReadingAids(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        var aloud = tab.ReadAloud;
        if (aloud.IsOpen)
        {
            var status = aloud.IsDownloading ? Share(UiText.Get("Getting the voice"), aloud.DownloadProgress) : aloud.StatusText;
            bars.Add(new(
                Label(UiText.Get("Read aloud"), status),
                [
                    Item(aloud.PlayPauseText, aloud.PlayPauseCommand),
                    Item("Sentence before", aloud.PreviousCommand),
                    Item("Next sentence", aloud.NextCommand),
                    Item("Read this page", aloud.ReadThisPageCommand),
                    Item("Read to the end", aloud.ReadToEndCommand),
                    Item("Get a better voice", aloud.DownloadVoiceCommand),
                    Item("Close", aloud.CloseCommand),
                ]));
        }

        var text = tab.TextRecognition;
        if (text.IsBusy)
        {
            bars.Add(new(Share(Label(UiText.Get("Recognising text"), text.ProgressText), text.Progress), [Item("Stop", text.StopCommand)]));
        }

        if (text.IsLanguageBarOpen)
        {
            bars.Add(new(text.LanguageBarText, [Item(text.LanguageActionText, text.ConfirmLanguageCommand), Item("Close", text.CloseLanguageBarCommand)]));
        }

        var focus = tab.FocusMode;
        if (focus.IsOn)
        {
            bars.Add(new(
                UiText.Get("Focus mode: one column of plain text."),
                [Item("Larger", focus.LargerCommand), Item("Smaller", focus.SmallerCommand), Item("Back to the pages", focus.ToggleCommand)]));
        }
    }

    /// <summary>Adds the state of the tools that work on the page: scrolling by itself, measuring, filling and signing, and changing the page's own content.</summary>
    /// <param name="bars">Receives the bars.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddPageTools(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        var tools = tab.PageTools;
        if (tools.IsAutoScrolling)
        {
            bars.Add(new(
                UiText.Format("Scrolling by itself at speed {0}.", tools.AutoScrollSpeed),
                [Item("Slower", tools.SlowerAutoScrollCommand), Item("Faster", tools.FasterAutoScrollCommand), Item("Stop", tools.StopAutoScrollCommand)]));
        }

        var measure = tab.Measure;
        if (measure.IsOn)
        {
            var result = measure.Result.Length > 0 ? measure.Result : UiText.Get("Click the points to measure between.");
            bars.Add(new(Label(UiText.Get("Measure"), result), [Item("Keep as a mark", measure.KeepCommand), Item("Clear", measure.ClearCommand), Item("Done", measure.ToggleCommand)]));
        }

        var sign = tab.FillAndSign;
        if (sign.IsActive)
        {
            bars.Add(new(
                sign.Hint,
                [
                    Item("Signature", sign.SignatureCommand),
                    Item("Initials", sign.InitialsCommand),
                    Item("Put it down", sign.PlaceCommand),
                    Item("Bigger", sign.BiggerCommand),
                    Item("Smaller", sign.SmallerCommand),
                    Item("Remove", sign.RemoveMarkCommand),
                    Item("Done", sign.ToggleCommand),
                ]));
        }
    }

    /// <summary>Adds a notice when it has text.</summary>
    /// <param name="bars">Receives the bar.</param>
    /// <param name="text">The notice, or <see langword="null"/> or empty for none.</param>
    /// <param name="actions">What can be done about it.</param>
    private static void AddNotice(List<ToolBar> bars, string? text, params MenuEntry[] actions)
    {
        if (text is { Length: > 0 })
        {
            bars.Add(new(text, actions));
        }
    }

    /// <summary>Lays one bar out: its text, then a button for each thing that can be done now.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="bar">The bar.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">The top of the bar.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">Receives the command of each button.</param>
    /// <returns>The height the bar takes, which is more than one line when the window is too narrow.</returns>
    private static int AddBar(List<ChromeItem> items, ToolBar bar, ChromeLook look, int top, int width, List<MenuEntry> actions)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, ChromeCommand.None, 0, string.Empty, default, true, false));
        var tight = metrics with { Padding = metrics.Gap + metrics.Gap };
        var row = new ChromeRow(items, look.Runs, tight, tight.Gap, top + tight.Gap, tight.ToolbarHeight - tight.Gap - tight.Gap) { Limit = width - tight.Gap };
        row = row.Label(ChromeText.Fit(bar.Text, ChromeBars.Style(metrics), Math.Max(metrics.PopupWidth, width >> 1), look.Runs));
        row = row.Space();
        foreach (var action in bar.Actions)
        {
            // A button that cannot do anything now is left out, so the bar only offers what works.
            if (!action.IsEnabled)
            {
                continue;
            }

            row = row.Button(action.Header, ChromeCommand.ToolAction, actions.Count, false);
            actions.Add(action);
        }

        var height = row.Lines * metrics.ToolbarHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }

    /// <summary>Makes a button that runs a command, offered only while the command can run.</summary>
    /// <param name="text">The button's text in English, which is looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <returns>The button.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string text, ICommand command) => MenuEntry.Item(UiText.Get(text), null, command, null, command.CanExecute(null));

    /// <summary>Puts a tool's name before what it says.</summary>
    /// <param name="name">The tool's name.</param>
    /// <param name="status">What it says, which may be empty.</param>
    /// <returns>The name alone, or the name and the status.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Label(string name, string status) => status.Length > 0 ? $"{name}: {status}" : name;

    /// <summary>Puts how much of some work is done after its name.</summary>
    /// <param name="name">What the work is.</param>
    /// <param name="share">How much is done, from 0 to 1.</param>
    /// <returns>The name and a percentage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Share(string name, double share) => string.Create(CultureInfo.CurrentCulture, $"{name} {Math.Clamp(share, 0, 1) * Percent:0}%");

    /// <summary>Joins a notice and its details, when the details are shown.</summary>
    /// <param name="notice">The notice, or <see langword="null"/> for none.</param>
    /// <param name="details">The details, or <see langword="null"/> while they are hidden.</param>
    /// <returns>The notice with its details, or <see langword="null"/> for no notice.</returns>
    private static string? Join(string? notice, string? details) => notice is { Length: > 0 } && details is { Length: > 0 } ? $"{notice} {details}" : notice;
}

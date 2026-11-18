// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Text;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Icons;

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
    /// <returns>The height the bars take together.</returns>
    internal static int Add(List<ChromeItem> items, DocumentTabViewModel tab, ChromeLook look, int top, int width)
    {
        var bars = new List<ToolBar>();
        AddNotices(bars, tab);
        AddReadingAids(bars, tab);
        AddPageTools(bars, tab);
        var height = 0;
        foreach (var bar in bars)
        {
            height += AddBar(items, bar, look, top + height, width);
        }

        return height;
    }

    /// <summary>Adds the notices about the document: what it needs, what was repaired, what cannot be shown and a newer copy on disk.</summary>
    /// <param name="bars">Receives the bars.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddNotices(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        // Most documents have no notice, and a layout is built for every change, so the buttons of a notice are made only for one that is shown.
        var notices = tab.Notices;
        if (IsShown(tab.Notice))
        {
            AddNotice(bars, tab.Notice, Item(ChromeIcon.Close, DismissText, tab.DismissNoticeCommand));
        }

        if (IsShown(notices.ContentWarning))
        {
            AddNotice(bars, notices.ContentWarning, Item(ChromeIcon.Close, DismissText, notices.DismissContentWarningCommand));
        }

        if (IsShown(notices.IntegrityNotice))
        {
            AddNotice(bars, notices.IntegrityNotice, Item(ChromeIcon.Close, DismissText, notices.DismissIntegrityNoticeCommand));
        }

        if (IsShown(notices.RepairNotice))
        {
            AddNotice(
                bars,
                Join(notices.RepairNotice, notices.RepairDetails),
                Item(ChromeIcon.More, "Details", notices.ToggleRepairDetailsCommand),
                Item(ChromeIcon.Close, DismissText, notices.DismissRepairNoticeCommand));
        }

        if (IsShown(notices.RequirementNotice))
        {
            AddNotice(
                bars,
                Join(notices.RequirementNotice, notices.RequirementDetails),
                Item(ChromeIcon.More, "Details", notices.ToggleRequirementDetailsCommand),
                Item(ChromeIcon.Close, DismissText, notices.DismissRequirementNoticeCommand));
        }

        if (tab.HasPendingReload)
        {
            bars.Add(new(UiText.Get("This file has changed on disk."), [Item(ChromeIcon.Redo, "Reload", tab.ReloadCommand), Item(ChromeIcon.Done, "Keep this version", tab.DismissReloadCommand)]));
        }
    }

    /// <summary>Adds the state of the reading aids that are running: read aloud, text recognition and focus mode.</summary>
    /// <param name="bars">Receives the bars.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddReadingAids(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        // Read Aloud and Focus Mode have no bar until they have been used, and reading them here would make them for every tab shown.
        if (tab.ReadAloudIfMade is { IsOpen: true } aloud)
        {
            var status = aloud.IsDownloading ? Share(UiText.Get("Getting the voice"), aloud.DownloadProgress) : aloud.StatusText;
            bars.Add(new(
                Label(UiText.Get("Read aloud"), status),
                [
                    Item(ChromeIcon.Play, aloud.PlayPauseText, aloud.PlayPauseCommand),
                    Item(ChromeIcon.PreviousSentence, "Sentence before", aloud.PreviousCommand),
                    Item(ChromeIcon.NextSentence, "Next sentence", aloud.NextCommand),
                    Item(ChromeIcon.Speaker, "Read this page", aloud.ReadThisPageCommand),
                    Item(ChromeIcon.PageDown, "Read to the end", aloud.ReadToEndCommand),
                    Item(ChromeIcon.Add, "Get a better voice", aloud.DownloadVoiceCommand),
                    Item(ChromeIcon.Close, "Close", aloud.CloseCommand),
                ]));
        }

        // Text recognition has nothing to show until it has been used, and reading it here would make it for every tab shown.
        var text = tab.TextRecognitionInUse;
        if (text is { IsBusy: true })
        {
            bars.Add(new(Share(Label(UiText.Get("Recognising text"), text.ProgressText), text.Progress), [Item(ChromeIcon.Stop, "Stop", text.StopCommand)]));
        }

        if (text is { IsLanguageBarOpen: true })
        {
            bars.Add(new(text.LanguageBarText, [Item(ChromeIcon.Done, text.LanguageActionText, text.ConfirmLanguageCommand), Item(ChromeIcon.Close, "Close", text.CloseLanguageBarCommand)]));
        }

        AddInks(bars, tab.Separations);
        if (tab.FocusModeIfMade is { IsOn: true } focus)
        {
            bars.Add(new(
                UiText.Get("Focus mode: one column of plain text."),
                [
                    Item(ChromeIcon.ZoomIn, "Larger", focus.LargerCommand),
                    Item(ChromeIcon.ZoomOut, "Smaller", focus.SmallerCommand),
                    Item(ChromeIcon.TextSize, "Text settings", focus.ShowSettingsCommand),
                    Item(ChromeIcon.Back, "Back to the pages", focus.ToggleCommand),
                ]));
        }
    }

    /// <summary>Adds the bar that says which ink of a colour separated page is shown and offers the others.</summary>
    /// <param name="bars">The bars so far.</param>
    /// <param name="inks">The separations of the document.</param>
    private static void AddInks(List<ToolBar> bars, PageSeparationViewModel inks)
    {
        if (!inks.HasSeparations || inks.Description is not { Length: > 0 } description)
        {
            return;
        }

        var actions = new List<BarAction>(inks.Colorants.Count);
        for (var i = 0; i < inks.Colorants.Count; i++)
        {
            actions.Add(new(ChromeIcon.Layers, MenuEntry.Item(UiText.Format("Show {0}", inks.Colorants[i]), null, inks.ShowInkCommand, i, i != inks.SelectedIndex)));
        }

        bars.Add(new(Label(UiText.Get("Inks"), description), [.. actions]));
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
                [
                    Item(ChromeIcon.Back, "Slower", tools.SlowerAutoScrollCommand),
                    Item(ChromeIcon.Forward, "Faster", tools.FasterAutoScrollCommand),
                    Item(ChromeIcon.Stop, "Stop", tools.StopAutoScrollCommand),
                ]));
        }

        if (tab.MeasureIfMade is { IsOn: true } measure)
        {
            var result = measure.Result.Length > 0 ? measure.Result : UiText.Get("Click the points to measure between.");
            bars.Add(new(
                Label(UiText.Get("Measure"), result),
                [Item(ChromeIcon.Add, "Keep as a mark", measure.KeepCommand), Item(ChromeIcon.Close, "Clear", measure.ClearCommand), Item(ChromeIcon.Done, "Done", measure.ToggleCommand)]));
        }

        AddEditTools(bars, tab);
        AddTextFormat(bars, tab);
        if (tab.FillAndSignIfMade is { IsActive: true } sign)
        {
            bars.Add(new(
                sign.Hint,
                [
                    Item(ChromeIcon.Signature, "Signature", sign.SignatureCommand),
                    Item(ChromeIcon.Text, "Initials", sign.InitialsCommand),
                    Item(ChromeIcon.Stamp, "Put it down", sign.PlaceCommand),
                    Item(ChromeIcon.ZoomIn, "Bigger", sign.BiggerCommand),
                    Item(ChromeIcon.ZoomOut, "Smaller", sign.SmallerCommand),
                    Item(ChromeIcon.Close, "Remove", sign.RemoveMarkCommand),
                    Item(ChromeIcon.Done, "Done", sign.ToggleCommand),
                ]));
        }
    }

    /// <summary>Adds the bar of the text being typed on the page: its weight, slant, underline, alignment and size, its properties window and the button that finishes it.</summary>
    /// <param name="bars">Receives the bar.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddTextFormat(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        if (!tab.IsTextFormatShown)
        {
            return;
        }

        var marks = tab.Annotations;
        bars.Add(new(
            UiText.Get("Text format"),
            [
                Item(ChromeIcon.Bold, "Bold", marks.ToggleBoldCommand),
                Item(ChromeIcon.Italic, "Italic", marks.ToggleItalicCommand),
                Item(ChromeIcon.Underline, "Underline", marks.ToggleUnderlineCommand),
                new(ChromeIcon.AlignLeft, MenuEntry.Item(UiText.Get("Align left"), null, marks.SetAlignmentCommand, TextBoxAlignment.Left, true)),
                new(ChromeIcon.AlignCentre, MenuEntry.Item(UiText.Get("Align centre"), null, marks.SetAlignmentCommand, TextBoxAlignment.Center, true)),
                new(ChromeIcon.AlignRight, MenuEntry.Item(UiText.Get("Align right"), null, marks.SetAlignmentCommand, TextBoxAlignment.Right, true)),
                Item(ChromeIcon.ZoomIn, "Larger text", marks.GrowTextCommand),
                Item(ChromeIcon.ZoomOut, "Smaller text", marks.ShrinkTextCommand),
                Item(ChromeIcon.More, "Text properties…", marks.EditTextPropertiesCommand),
                Item(ChromeIcon.Done, "Finish text", marks.FinishTextCommand),
            ]));
    }

    /// <summary>Adds the bar of editing the page's own content: what is picked can be retyped or removed, the edits undone and done again, and a PDF 2.0 copy saved.</summary>
    /// <param name="bars">Receives the bar.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddEditTools(List<ToolBar> bars, DocumentTabViewModel tab)
    {
        if (tab.ContentEditIfMade is not { IsEditing: true } edit)
        {
            return;
        }

        bars.Add(new(
            UiText.Get("Edit tools"),
            [
                Item(ChromeIcon.Text, "Retype text", edit.EditPickedTextCommand),
                Item(ChromeIcon.Close, "Remove picked", edit.RemovePickedCommand),
                Item(ChromeIcon.Undo, "Undo", tab.Pages.UndoCommand),
                Item(ChromeIcon.Redo, "Redo", tab.Pages.RedoCommand),
                Item(ChromeIcon.Save, "Save PDF 2.0 copy", tab.Copies.Pdf20.RunCommand),
                Item(ChromeIcon.Done, "Done", edit.ToggleEditingCommand),
            ]));
    }

    /// <summary>Tells whether a notice has text to show.</summary>
    /// <param name="text">The notice, or <see langword="null"/> or empty for none.</param>
    /// <returns><see langword="true"/> when there is a notice.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsShown([NotNullWhen(true)] string? text) => text is { Length: > 0 };

    /// <summary>Adds a notice when it has text.</summary>
    /// <param name="bars">Receives the bar.</param>
    /// <param name="text">The notice, or <see langword="null"/> or empty for none.</param>
    /// <param name="actions">What can be done about it.</param>
    private static void AddNotice(List<ToolBar> bars, string? text, params BarAction[] actions)
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
    /// <returns>The height the bar takes, which is more than one line when the window is too narrow.</returns>
    private static int AddBar(List<ChromeItem> items, ToolBar bar, ChromeLook look, int top, int width)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, default, true, false));
        var tight = metrics with { Padding = metrics.Gap + metrics.Gap };
        var row = new ChromeRow(items, look.Runs, tight, tight.Gap, top + tight.Gap, tight.ToolbarHeight - tight.Gap - tight.Gap) { Limit = width - tight.Gap };
        row = row.Label(ChromeText.Fit(bar.Text, ChromeBars.Style(metrics), Math.Max(metrics.PopupWidth, width >> 1), look.Runs));
        row = row.Space();
        foreach (var action in bar.Actions)
        {
            // A button that cannot do anything now is left out, so the bar only offers what works.
            var entry = action.Entry;
            if (!entry.IsEnabled)
            {
                continue;
            }

            // The button runs the action's own command. Its place among the controls tells it from another button of the same command.
            row = row.Button(action.Icon, entry.Header, entry.Command, new GroupChoice(items.Count, entry.Parameter, false));
        }

        var height = row.Lines * metrics.ToolbarHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }

    /// <summary>Makes a button that runs a command, offered only while the command can run.</summary>
    /// <param name="icon">The icon drawn before the text.</param>
    /// <param name="text">The button's text in English, which is looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <returns>The button.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BarAction Item(ChromeIcon icon, string text, ICommand command) => new(icon, MenuEntry.Item(UiText.Get(text), null, command, null, command.CanExecute(null)));

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

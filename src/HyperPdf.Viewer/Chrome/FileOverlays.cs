// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Presentation.Input;
using HyperPdf.Viewer.Text;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The file dialog, drawn inside the window so it looks and works the same on every system. It lists one folder at a time, and a
/// path can be typed instead of picked.
/// </summary>
internal static class FileOverlays
{
    /// <summary>The rows of the list shown at once.</summary>
    private const int VisibleRows = 10;

    /// <summary>The rows of the dialog besides the list: heading, typing box, folder name and buttons.</summary>
    private const int OtherRows = 4;

    /// <summary>The mark after the name of a folder.</summary>
    private const string FolderMark = "/";

    /// <summary>The extension of the files listed.</summary>
    private const string PdfExtension = ".pdf";

    /// <summary>Opens the dialog.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <param name="saveName">The file name to offer when saving, or <see langword="null"/> to ask what to open.</param>
    /// <param name="many">Whether several files can be opened at once.</param>
    /// <param name="picksFolder">Whether the dialog asks for a folder rather than a file.</param>
    /// <returns>The dialog and its answer: the paths chosen, or none when it was cancelled.</returns>
    internal static OpenedOverlay<IReadOnlyList<string>> Open(string folder, string? saveName, bool many, bool picksFolder)
    {
        var saving = saveName is not null;
        var start = ReadFolder(
            new(
                Directory.Exists(folder) ? folder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                saveName ?? string.Empty,
                [],
                0,
                0,
                [],
                false,
                saving,
                many && !saving,
                picksFolder,
                false,
                []));
        var input = new Signal<OverlayInput>();
        var state = OverlayStates.Track(input, start, Reduce);
        var answered = state.Where(static current => current.Answered);
        var overlay = new Overlay(
            OverlayKind.Dialog,
            OverlayTraits.Modal | OverlayTraits.TakesAllKeys,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, state.Value, in place),
            input,
            Signal.Never<RxVoid>(),
            answered.AsSignal(),
            new MultipleDisposable(state, input));
        return new(overlay, answered.Select(static current => (IReadOnlyList<string>)current.Answer).Take(1));
    }

    /// <summary>Makes the state that follows what the user did.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="input">What the user did.</param>
    /// <returns>The state after.</returns>
    private static FileState Reduce(FileState state, OverlayInput input) => state.Answered ? state : input switch
    {
        { Kind: OverlayInputKind.Press, Item: { } item } => Press(state, item),
        { Kind: OverlayInputKind.Key } => PressKey(state, input.Key),
        { Kind: OverlayInputKind.Scroll } => Scroll(state, input.Value),
        { IsTypedCharacter: true } => state with { Entry = string.Concat(state.Entry, input.TypedText) },
        { Kind: OverlayInputKind.Dismiss } => state with { Answered = true },
        _ => state,
    };

    /// <summary>Takes a press on a control.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="item">The control.</param>
    /// <returns>The state after.</returns>
    private static FileState Press(FileState state, ChromeItem item) => item.Action switch
    {
        OverlayAction.ParentFolder => Directory.GetParent(state.Folder) is { } parent ? ShowFolder(state, parent.FullName) : state,
        OverlayAction.PickRow => Pick(state, item.Index),
        OverlayAction.FileKinds => ShowFolder(state with { AllFiles = !state.AllFiles }, state.Folder),
        OverlayAction.Accept => AcceptEntry(state),
        OverlayAction.Cancel => state with { Answered = true },
        _ => state,
    };

    /// <summary>Takes a key: Enter uses what was typed, Escape cancels, Backspace takes the last character, and the arrows move the list.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="key">The key.</param>
    /// <returns>The state after.</returns>
    private static FileState PressKey(FileState state, ViewerKey key) => key switch
    {
        ViewerKey.Enter => AcceptEntry(state),
        ViewerKey.Escape => state with { Answered = true },
        ViewerKey.Back => state with { Entry = ChromeText.WithoutLast(state.Entry) },
        _ => Scroll(state, RowsOf(key)),
    };

    /// <summary>Tells how far a key moves the list.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The rows to move down, negative to move up, or zero for a key that does not move the list.</returns>
    private static int RowsOf(ViewerKey key) => key switch
    {
        ViewerKey.Down => 1,
        ViewerKey.Up => -1,
        ViewerKey.PageDown => VisibleRows,
        ViewerKey.PageUp => -VisibleRows,
        _ => 0,
    };

    /// <summary>Scrolls the list, no further than its ends.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="rows">The rows to move down, negative to move up.</param>
    /// <returns>The state after.</returns>
    private static FileState Scroll(FileState state, int rows) =>
        rows == 0 ? state : state with { First = Math.Clamp(state.First + rows, 0, Math.Max(0, state.Names.Length - VisibleRows)) };

    /// <summary>Handles a press on a row of the list: goes into a folder, takes a file's name to save as, ticks a file, or answers with the file.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="index">The row.</param>
    /// <returns>The state after.</returns>
    private static FileState Pick(FileState state, int index)
    {
        if ((uint)index >= (uint)state.Names.Length)
        {
            return state;
        }

        var path = Path.Combine(state.Folder, state.Names[index]);
        if (index < state.FolderCount)
        {
            return ShowFolder(state, path);
        }

        if (state.Saving)
        {
            return state with { Entry = state.Names[index] };
        }

        // A press ticks the file or takes the tick away; the open button opens every ticked file.
        return state.Many ? state with { Chosen = Toggle(state.Chosen, state.Names[index]) } : state with { Answered = true, Answer = [path] };
    }

    /// <summary>Adds a name to the ticked names, or takes it away when it is there.</summary>
    /// <param name="chosen">The names ticked.</param>
    /// <param name="name">The name.</param>
    /// <returns>The names ticked after.</returns>
    private static string[] Toggle(string[] chosen, string name)
    {
        var at = Array.IndexOf(chosen, name);
        if (at < 0)
        {
            return [.. chosen, name];
        }

        var kept = new List<string>(chosen);
        kept.RemoveAt(at);
        return [.. kept];
    }

    /// <summary>Uses the typed text, or the ticked files when nothing is typed: goes into a folder, or answers with the files.</summary>
    /// <param name="state">The state before.</param>
    /// <returns>The state after.</returns>
    private static FileState AcceptEntry(FileState state)
    {
        if (state.Entry.Length == 0)
        {
            // With nothing typed, the folder shown is the one chosen when a folder is asked for, and the ticked files are otherwise.
            return state.PicksFolder ? state with { Answered = true, Answer = [state.Folder] } : AcceptChosen(state);
        }

        var path = Path.GetFullPath(state.Entry, state.Folder);
        if (Directory.Exists(path))
        {
            return ShowFolder(state with { Entry = string.Empty }, path);
        }

        return state.Saving || File.Exists(path) ? state with { Answered = true, Answer = [path] } : state;
    }

    /// <summary>Answers with the ticked files, in the order they are listed. With no file ticked the dialog stays open.</summary>
    /// <param name="state">The state before.</param>
    /// <returns>The state after.</returns>
    private static FileState AcceptChosen(FileState state)
    {
        if (state.Chosen.Length == 0)
        {
            return state;
        }

        var paths = new List<string>(state.Chosen.Length);
        for (var i = state.FolderCount; i < state.Names.Length; i++)
        {
            if (Array.IndexOf(state.Chosen, state.Names[i]) >= 0)
            {
                paths.Add(Path.Combine(state.Folder, state.Names[i]));
            }
        }

        return state with { Answered = true, Answer = [.. paths] };
    }

    /// <summary>Shows another folder.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="folder">The folder.</param>
    /// <returns>The state after.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FileState ShowFolder(FileState state, string folder) => ReadFolder(state with { Folder = folder, First = 0, Chosen = [] });

    /// <summary>Reads the names in the folder shown. A folder that cannot be read is shown empty.</summary>
    /// <param name="state">The state before.</param>
    /// <returns>The state with the names of the folder.</returns>
    private static FileState ReadFolder(FileState state)
    {
        try
        {
            var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
            var folders = new List<string>();
            foreach (var path in Directory.EnumerateDirectories(state.Folder, "*", options))
            {
                folders.Add(Path.GetFileName(path));
            }

            folders.Sort(StringComparer.CurrentCultureIgnoreCase);
            var files = new List<string>();
            foreach (var path in Directory.EnumerateFiles(state.Folder, "*", options))
            {
                if (state.AllFiles || path.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase))
                {
                    files.Add(Path.GetFileName(path));
                }
            }

            files.Sort(StringComparer.CurrentCultureIgnoreCase);
            return state with { Names = [.. folders, .. files], FolderCount = folders.Count };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return state with { Names = [], FolderCount = 0 };
        }
    }

    /// <summary>Gets the heading of a dialog that asks for a file.</summary>
    /// <param name="state">What the dialog shows.</param>
    /// <returns>The heading.</returns>
    private static string TitleOf(FileState state)
    {
        if (state.PicksFolder)
        {
            return UiText.Get("Choose a folder");
        }

        if (state.Saving)
        {
            return UiText.Get("Save a copy as");
        }

        return UiText.Get(state.Many ? "Open files" : "Open a file");
    }

    /// <summary>Gets the text of the button that agrees.</summary>
    /// <param name="state">What the dialog shows.</param>
    /// <returns>The text.</returns>
    private static string AcceptText(FileState state)
    {
        if (state.PicksFolder)
        {
            return UiText.Get("Choose this folder");
        }

        return state.Saving ? UiText.Get("Save") : UiText.Get("Open");
    }

    /// <summary>Lays the dialog out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="state">What the dialog shows.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, FileState state, in OverlayPlace place)
    {
        var runs = place.Runs;
        var metrics = place.Metrics;
        var size = place.Size;
        var style = ChromeBars.Style(metrics);
        var width = Math.Min(metrics.DialogWidth, size.Width);
        var inner = width - metrics.Padding - metrics.Padding;
        var height = (metrics.RowHeight * (VisibleRows + OtherRows)) + metrics.ToolbarHeight + metrics.Padding + metrics.Padding;
        var left = (size.Width - width) >> 1;
        var top = Math.Max(0, (size.Height - height) >> 1);
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top + metrics.Padding;
        items.Add(new(ChromeItemKind.Heading, null, 0, TitleOf(state), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        var typed = ChromeText.FitEnd(state.Entry, style, inner - metrics.Padding - metrics.Padding, runs);
        items.Add(new(ChromeItemKind.Field, null, 0, typed, new(left + metrics.Padding, y, inner, metrics.RowHeight), true, true));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Label, null, 0, ChromeText.FitEnd(state.Folder, style, inner, runs), new(left, y, width, metrics.RowHeight), false, false));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Row, OverlayAction.ParentFolder, 0, UiText.Get("↑ Up one folder"), new(left, y, width, metrics.RowHeight), Directory.GetParent(state.Folder) is not null, false));
        y += metrics.RowHeight;
        for (var row = 0; row < VisibleRows; row++)
        {
            var index = state.First + row;
            if (index < state.Names.Length)
            {
                var name = index < state.FolderCount ? string.Concat(state.Names[index], FolderMark) : state.Names[index];
                var ticked = index >= state.FolderCount && Array.IndexOf(state.Chosen, state.Names[index]) >= 0;
                items.Add(new(ChromeItemKind.Row, OverlayAction.PickRow, index, ChromeText.Fit(name, style, inner, runs), new(left, y, width, metrics.RowHeight), true, ticked));
            }

            y += metrics.RowHeight;
        }

        var buttons = new ChromeRow(items, runs, metrics, left + metrics.Padding, y + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        buttons = buttons.Button(AcceptText(state), OverlayAction.Accept, state.Entry.Length > 0 || state.Chosen.Length > 0 || state.PicksFolder);
        buttons = buttons.Button(UiText.Get("Cancel"), OverlayAction.Cancel, true);
        buttons = buttons.Space();
        buttons = buttons.Button(UiText.Get(state.AllFiles ? "Showing all files" : "Showing PDF files"), OverlayAction.FileKinds, true);
    }
}

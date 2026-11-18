// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// A menu drawn inside the window. A submenu takes the place of its parent, with a row that leads back, so only one list is on
/// screen at a time and nothing opens or closes as the pointer passes.
/// </summary>
internal static class MenuOverlays
{
    /// <summary>The index of the row that leads back to the parent menu.</summary>
    internal const int BackIndex = -1;

    /// <summary>The rows a separator's height is divided by.</summary>
    private const int SeparatorShare = 3;

    /// <summary>The mark before a ticked entry.</summary>
    private const string Tick = "● ";

    /// <summary>The mark after an entry that opens a submenu.</summary>
    private const string More = "  ›";

    /// <summary>
    /// Opens a menu. A press or a key that leaves the menu open moves within it. One that does not closes it: an entry that was chosen,
    /// going back from the top menu, or a press somewhere else.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="anchor">Where the menu's top left corner is wanted, in device pixels.</param>
    /// <param name="run">Runs the command of the entry that was chosen.</param>
    /// <returns>The menu.</returns>
    internal static Overlay Open(ReadOnlySpan<MenuEntry> entries, ScreenPoint anchor, Action<MenuEntry> run)
    {
        var input = new Signal<OverlayInput>();
        var start = new MenuState([..entries], [], BackIndex - 1, null, false);
        var state = OverlayStates.Track(input, start, Reduce);
        var done = state.Where(static current => current.Done);
        return new(
            OverlayKind.Menu,
            OverlayTraits.TakesAllKeys | OverlayTraits.ClosesOnOutsidePress | OverlayTraits.UsesTab,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, state.Value, anchor, in place),
            input,
            Signal.Never<RxVoid>(),
            done.AsSignal(),
            new MultipleDisposable(input, state, done.Where(static current => current.Chosen is not null).Select(static current => current.Chosen!).Take(1).SubscribeSafe(run, ErrorTrace.Write)));
    }

    /// <summary>Makes the state that follows what the user did.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="input">What the user did.</param>
    /// <returns>The state after.</returns>
    private static MenuState Reduce(MenuState state, OverlayInput input) => state.Done ? state : input switch
    {
        { Kind: OverlayInputKind.Press, Item: { Action: OverlayAction.PickOption } item } => Press(state, item.Index),
        { Kind: OverlayInputKind.Key } => PressKey(state, input.Key),
        { Kind: OverlayInputKind.Scroll } => MoveCursor(state, Math.Sign(input.Value)),
        { Kind: OverlayInputKind.Dismiss } => state with { Done = true },
        _ => state,
    };

    /// <summary>Takes a press on a row.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="index">The entry pressed, or <see cref="BackIndex"/> for the row that leads back.</param>
    /// <returns>The state after.</returns>
    private static MenuState Press(MenuState state, int index) => index == BackIndex ? GoBack(state) : Run(state, state.Shown[index]);

    /// <summary>Takes a key: the arrows move through the entries, Enter runs one and Escape goes back.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="key">The key.</param>
    /// <returns>The state after.</returns>
    private static MenuState PressKey(MenuState state, ViewerKey key) => key switch
    {
        ViewerKey.Down => MoveCursor(state, 1),
        ViewerKey.Up => MoveCursor(state, -1),
        ViewerKey.Enter or ViewerKey.Space or ViewerKey.Right when state.Cursor >= 0 && state.Cursor < state.Shown.Length => Run(state, state.Shown[state.Cursor]),
        ViewerKey.Escape or ViewerKey.Left or ViewerKey.Enter => GoBack(state),
        _ => state,
    };

    /// <summary>Runs an entry, or opens its submenu.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The state after, which is closed when the entry was chosen.</returns>
    private static MenuState Run(MenuState state, MenuEntry entry)
    {
        if (!entry.IsEnabled)
        {
            return state;
        }

        if (entry.Kind == MenuEntryKind.Submenu)
        {
            return state with { Parents = [.. state.Parents, state.Shown], Shown = entry.Children, Cursor = BackIndex - 1 };
        }

        return entry.Command is null ? state : state with { Chosen = entry, Done = true };
    }

    /// <summary>Goes back to the parent menu, or closes the top menu.</summary>
    /// <param name="state">The state before.</param>
    /// <returns>The state after.</returns>
    private static MenuState GoBack(MenuState state) => state.Parents.Length == 0
        ? state with { Done = true }
        : state with { Shown = state.Parents[^1], Parents = state.Parents[..^1], Cursor = BackIndex - 1 };

    /// <summary>Tells whether the keyboard can stop on an entry.</summary>
    /// <param name="state">What the menu shows.</param>
    /// <param name="index">The entry, or <see cref="BackIndex"/>.</param>
    /// <returns><see langword="true"/> when it can be pressed.</returns>
    private static bool CanStopOn(MenuState state, int index) => index == BackIndex || (state.Shown[index].Kind != MenuEntryKind.Separator && state.Shown[index].IsEnabled);

    /// <summary>Moves the keyboard to the next entry that can be pressed.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="step">1 to move down, -1 to move up.</param>
    /// <returns>The state after.</returns>
    private static MenuState MoveCursor(MenuState state, int step)
    {
        var first = state.Parents.Length > 0 ? BackIndex : 0;
        var count = state.Shown.Length - first;
        if (count <= 0)
        {
            return state;
        }

        // With no entry chosen yet, down starts at the first entry and up at the last.
        var at = state.Cursor;
        if (at < first)
        {
            at = step > 0 ? first - 1 : state.Shown.Length;
        }

        for (var tries = 0; tries < count; tries++)
        {
            at = first + ((at - first + step + count) % count);
            if (CanStopOn(state, at))
            {
                return state with { Cursor = at };
            }
        }

        return state;
    }

    /// <summary>Lays the menu out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="state">What the menu shows.</param>
    /// <param name="anchor">Where the menu's top left corner is wanted.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, MenuState state, ScreenPoint anchor, in OverlayPlace place)
    {
        var runs = place.Runs;
        var metrics = place.Metrics;
        var size = place.Size;
        var style = ChromeBars.Style(metrics);
        var width = metrics.PopupWidth;
        var back = state.Parents.Length > 0 ? metrics.RowHeight : 0;
        for (var i = 0; i < state.Shown.Length; i++)
        {
            var entry = state.Shown[i];
            var gesture = entry.Gesture is { } keys ? runs.Measure(keys, style) + metrics.Padding : 0;
            width = Math.Max(width, runs.Measure(Header(entry), style) + gesture + (metrics.Padding * SeparatorShare));
        }

        // A menu taller than the window shows the rows from the first that lets the keyboard's row be seen, as many as fit.
        var first = FirstShown(state, metrics, size.Height - back);
        var count = 0;
        var height = back;
        while (first + count < state.Shown.Length && height + RowHeight(state.Shown[first + count], metrics) <= size.Height)
        {
            height += RowHeight(state.Shown[first + count], metrics);
            count++;
        }

        var left = Math.Max(0, Math.Min(anchor.X, size.Width - width));
        var top = Math.Max(0, Math.Min(anchor.Y, size.Height - height));
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top;
        if (state.Parents.Length > 0)
        {
            items.Add(new(ChromeItemKind.Row, OverlayAction.PickOption, BackIndex, UiText.Get("‹ Back"), new(left, y, width, metrics.RowHeight), true, state.Cursor == BackIndex));
            y += metrics.RowHeight;
        }

        for (var i = first; i < first + count; i++)
        {
            var bounds = new LayoutRect(left, y, width, RowHeight(state.Shown[i], metrics));
            y += (int)bounds.Height;
            AddEntry(items, state, runs, metrics, i, bounds);
        }
    }

    /// <summary>Finds the first entry a menu shows when it is taller than the room: the first, until the keyboard's entry would fall below the room.</summary>
    /// <param name="state">What the menu shows.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="room">The height left for the entries.</param>
    /// <returns>The index of the first entry shown.</returns>
    private static int FirstShown(MenuState state, in ChromeMetrics metrics, int room)
    {
        var first = 0;
        if (state.Cursor < 0)
        {
            return first;
        }

        var used = 0;
        for (var i = 0; i <= state.Cursor && i < state.Shown.Length; i++)
        {
            used += RowHeight(state.Shown[i], metrics);
        }

        // Entries are dropped from the top until the one the keyboard is on fits.
        while (used > room && first < state.Cursor)
        {
            used -= RowHeight(state.Shown[first], metrics);
            first++;
        }

        return first;
    }

    /// <summary>Gets the text of an entry as it is drawn.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The header with its marks.</returns>
    private static string Header(MenuEntry entry)
    {
        var header = ChromeText.WithoutAccessMark(entry.Header);
        if (entry.IsChecked)
        {
            header = string.Concat(Tick, header);
        }

        return entry.Kind == MenuEntryKind.Submenu ? string.Concat(header, More) : header;
    }

    /// <summary>Tells what an entry is beyond a row: ticked, or one that opens another menu.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The trait.</returns>
    private static ChromeItemTrait TraitOf(MenuEntry entry)
    {
        if (entry.Kind == MenuEntryKind.Submenu)
        {
            return ChromeItemTrait.OpensMore;
        }

        return entry.IsChecked ? ChromeItemTrait.Ticked : ChromeItemTrait.None;
    }

    /// <summary>Gets the height of an entry's row.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The height in device pixels.</returns>
    private static int RowHeight(MenuEntry entry, in ChromeMetrics metrics) =>
        entry.Kind == MenuEntryKind.Separator ? metrics.RowHeight / SeparatorShare : metrics.RowHeight;

    /// <summary>Adds the row of one entry, and its shortcut on the right when it has one.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="state">What the menu shows.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="index">The entry.</param>
    /// <param name="bounds">Where the row goes.</param>
    private static void AddEntry(List<ChromeItem> items, MenuState state, TextRuns runs, in ChromeMetrics metrics, int index, in LayoutRect bounds)
    {
        var entry = state.Shown[index];
        if (entry.Kind == MenuEntryKind.Separator)
        {
            items.Add(new(ChromeItemKind.Separator, null, index, string.Empty, bounds, true, false));
            return;
        }

        var pressable = entry.IsEnabled && (entry.Command is not null || entry.Kind == MenuEntryKind.Submenu);
        items.Add(new(ChromeItemKind.Row, pressable ? OverlayAction.PickOption : OverlayAction.None, index, Header(entry), bounds, entry.IsEnabled, index == state.Cursor) { Trait = TraitOf(entry) });
        if (entry.Gesture is not { } keys)
        {
            return;
        }

        var keysWidth = runs.Measure(keys, ChromeBars.Style(metrics)) + metrics.Padding + metrics.Padding;
        items.Add(new(ChromeItemKind.Label, null, index, keys, new(bounds.Right - keysWidth, bounds.Y, keysWidth, bounds.Height), false, false));
    }
}

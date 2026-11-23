// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Icons;
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

    /// <summary>The fewest rows of room below a toolbar anchor that keeps the menu under its button instead of shifting over it.</summary>
    private const int MinBelowRows = 4;

    /// <summary>The paddings around a menu row's icon and text.</summary>
    private const int RowPaddings = 3;

    /// <summary>The opaque alpha bits for a colour swatch.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The RGB bits of a colour swatch.</summary>
    private const uint ColorMask = 0x00FFFFFF;

    /// <summary>The sides of a margin (top and bottom, or left and right).</summary>
    private const int MarginSides = 2;

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
        var start = new MenuState([..entries], [], BackIndex - 1, 0, true, null, false);
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
        { Kind: OverlayInputKind.Scroll } => ScrollMenu(state, input.Value),
        { Kind: OverlayInputKind.Dismiss } => state with { Done = true },
        _ => state,
    };

    /// <summary>Scrolls the visible window of menu entries with the mouse wheel.</summary>
    /// <param name="state">The state before.</param>
    /// <param name="rows">The rows to scroll down, negative to scroll up.</param>
    /// <returns>The state after.</returns>
    private static MenuState ScrollMenu(MenuState state, int rows) =>
        rows == 0 ? state : state with { First = Math.Clamp(state.First + rows, 0, Math.Max(0, state.Shown.Length - 1)), FollowCursor = false };

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
            return state with { Parents = [.. state.Parents, state.Shown], Shown = entry.Children, Cursor = BackIndex - 1, First = 0, FollowCursor = true };
        }

        return entry.Command is null ? state : state with { Chosen = entry, Done = true };
    }

    /// <summary>Goes back to the parent menu, or closes the top menu.</summary>
    /// <param name="state">The state before.</param>
    /// <returns>The state after.</returns>
    private static MenuState GoBack(MenuState state) => state.Parents.Length == 0
        ? state with { Done = true }
        : state with { Shown = state.Parents[^1], Parents = state.Parents[..^1], Cursor = BackIndex - 1, First = 0, FollowCursor = true };

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
                return state with { Cursor = at, FollowCursor = true };
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
        var width = MeasureWidth(state, runs, style, in metrics, size.Width);
        var back = state.Parents.Length > 0 ? metrics.RowHeight : 0;
        var keepBelow = anchor.Y > 0 && size.Height - anchor.Y >= metrics.RowHeight * MinBelowRows;
        var maxHeight = keepBelow
            ? size.Height - anchor.Y - metrics.Gap
            : Math.Max(metrics.RowHeight, size.Height - (metrics.Gap * MarginSides));
        var room = Math.Max(metrics.RowHeight, maxHeight - back);
        var first = FirstShown(state, in metrics, room);
        var count = 0;
        var height = back;
        while (first + count < state.Shown.Length && height + RowHeight(state.Shown[first + count], in metrics) <= maxHeight)
        {
            height += RowHeight(state.Shown[first + count], in metrics);
            count++;
        }

        var left = Math.Max(0, Math.Min(anchor.X, size.Width - width));
        var top = keepBelow ? anchor.Y : Math.Max(0, Math.Min(anchor.Y, size.Height - height));
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top;
        if (state.Parents.Length > 0)
        {
            items.Add(new(ChromeItemKind.Row, OverlayAction.PickOption, BackIndex, UiText.Get("Back"), new(left, y, width, metrics.RowHeight), true, state.Cursor == BackIndex)
            {
                Icon = ChromeIcon.Back,
            });
            y += metrics.RowHeight;
        }

        var barWidth = count < state.Shown.Length ? metrics.Gap + metrics.Line : 0;
        for (var i = first; i < first + count; i++)
        {
            var bounds = new LayoutRect(left, y, width - barWidth, RowHeight(state.Shown[i], in metrics));
            y += (int)bounds.Height;
            AddEntry(items, state, runs, in metrics, i, in bounds);
        }

        FormLayout.AddScrollBar(
            items,
            new(left + width - metrics.Gap - metrics.Line, top + back, metrics.Gap, height - back),
            first,
            count,
            state.Shown.Length,
            place.Theme);
    }

    /// <summary>Measures the width the menu needs, clamped to the window width.</summary>
    /// <param name="state">What the menu shows.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">The text style.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="windowWidth">The window width.</param>
    /// <returns>The menu width in device pixels.</returns>
    private static int MeasureWidth(MenuState state, TextRuns runs, TextStyle style, in ChromeMetrics metrics, int windowWidth)
    {
        var width = metrics.PopupWidth;
        var iconRoom = metrics.IconSize + ChromePainter.IconGap(metrics);
        for (var i = 0; i < state.Shown.Length; i++)
        {
            var entry = state.Shown[i];
            var rightRoom = RightDecorationWidth(entry, runs, style, in metrics);
            var needed = runs.Measure(Header(entry), style) + iconRoom + rightRoom + (metrics.Padding * RowPaddings);
            width = Math.Max(width, needed);
        }

        return Math.Min(width, Math.Max(1, windowWidth));
    }

    /// <summary>Gets the width taken on the right side of an entry by its shortcut, submenu arrow or colour swatch.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">The text style.</param>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The width in device pixels.</returns>
    private static int RightDecorationWidth(MenuEntry entry, TextRuns runs, TextStyle style, in ChromeMetrics metrics)
    {
        if (entry.Gesture is { } keys)
        {
            return runs.Measure(keys, style) + metrics.Padding;
        }

        return entry.Kind == MenuEntryKind.Submenu || entry.Swatch is not null ? metrics.IconSize + metrics.Padding : 0;
    }

    /// <summary>Finds the first entry a menu shows when it is taller than the room.</summary>
    /// <param name="state">What the menu shows.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="room">The height left for the entries.</param>
    /// <returns>The index of the first entry shown.</returns>
    private static int FirstShown(MenuState state, in ChromeMetrics metrics, int room)
    {
        var maxFirst = MaxFirst(state.Shown, in metrics, room);
        var first = Math.Clamp(state.First, 0, maxFirst);
        if (!state.FollowCursor || state.Cursor < 0 || state.Cursor >= state.Shown.Length)
        {
            return first;
        }

        if (state.Cursor < first)
        {
            return state.Cursor;
        }

        var used = 0;
        for (var i = first; i <= state.Cursor; i++)
        {
            used += RowHeight(state.Shown[i], in metrics);
        }

        while (used > room && first < state.Cursor)
        {
            used -= RowHeight(state.Shown[first], in metrics);
            first++;
        }

        return first;
    }

    /// <summary>Finds the latest starting entry from which the remaining entries fill the available height.</summary>
    /// <param name="entries">All entries.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="room">The height left for the entries.</param>
    /// <returns>The highest useful start index.</returns>
    private static int MaxFirst(MenuEntry[] entries, in ChromeMetrics metrics, int room)
    {
        var used = 0;
        var first = entries.Length;
        while (first > 0)
        {
            var row = RowHeight(entries[first - 1], in metrics);
            if (used + row > room)
            {
                break;
            }

            used += row;
            first--;
        }

        return Math.Min(first, Math.Max(0, entries.Length - 1));
    }

    /// <summary>Gets the clean text of an entry without its access-key mark.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The header text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Header(MenuEntry entry) => ChromeText.WithoutAccessMark(entry.Header);

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

    /// <summary>Adds the row of one entry, with its icon, colour swatch, submenu mark or shortcut.</summary>
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

        var style = ChromeBars.Style(metrics);
        var rightRoom = RightDecorationWidth(entry, runs, style, in metrics);
        var icon = MenuIcons.Of(entry);
        var iconRoom = icon == ChromeIcon.None ? 0 : metrics.IconSize + ChromePainter.IconGap(metrics);
        var textRoom = Math.Max(1, (int)bounds.Width - (metrics.Padding * MarginSides) - iconRoom - rightRoom);
        var pressable = entry.IsEnabled && (entry.Command is not null || entry.Kind == MenuEntryKind.Submenu);
        var fitted = ChromeText.Fit(Header(entry), style, textRoom, runs);
        var action = pressable ? OverlayAction.PickOption : OverlayAction.None;
        items.Add(new(ChromeItemKind.Row, action, index, fitted, bounds, entry.IsEnabled, index == state.Cursor) { Trait = TraitOf(entry), Icon = icon });
        AddRightDecoration(items, entry, runs, style, in metrics, index, in bounds);
    }

    /// <summary>Adds the shortcut keys, submenu arrow or colour swatch at the right edge of a menu row.</summary>
    /// <param name="items">Receives the control.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="style">The text style.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="index">The entry index.</param>
    /// <param name="bounds">The row bounds.</param>
    private static void AddRightDecoration(List<ChromeItem> items, MenuEntry entry, TextRuns runs, TextStyle style, in ChromeMetrics metrics, int index, in LayoutRect bounds)
    {
        if (entry.Swatch is { } swatch)
        {
            var side = metrics.IconSize;
            var top = bounds.Y + ((bounds.Height - side) / MarginSides);
            var box = new LayoutRect(bounds.Right - side - metrics.Padding, top, side, side);
            items.Add(new(ChromeItemKind.Panel, null, index, string.Empty, box, false, false) { Ground = Opaque | (swatch & ColorMask), Framed = true });
            return;
        }

        if (entry.Kind == MenuEntryKind.Submenu)
        {
            var arrowWidth = metrics.IconSize + (metrics.Padding * MarginSides);
            var arrowBounds = new LayoutRect(bounds.Right - arrowWidth, bounds.Y, arrowWidth, bounds.Height);
            items.Add(new(ChromeItemKind.Picture, null, index, string.Empty, arrowBounds, entry.IsEnabled, false) { Icon = ChromeIcon.Forward });
            return;
        }

        if (entry.Gesture is not { } keys)
        {
            return;
        }

        var keysWidth = runs.Measure(keys, style) + (metrics.Padding * MarginSides);
        items.Add(new(ChromeItemKind.Label, null, index, keys, new(bounds.Right - keysWidth, bounds.Y, keysWidth, bounds.Height), false, false));
    }
}

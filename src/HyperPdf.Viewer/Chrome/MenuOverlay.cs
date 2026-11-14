// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// A menu drawn inside the window. A submenu takes the place of its parent, with a row that leads back, so only one list is on
/// screen at a time and nothing opens or closes as the pointer passes.
/// </summary>
/// <param name="entries">The entries.</param>
/// <param name="anchor">Where the menu's top left corner is wanted, in device pixels.</param>
[DebuggerDisplay("MenuOverlay: {_shown.Count} entries")]
internal sealed class MenuOverlay(IReadOnlyList<MenuEntry> entries, ScreenPoint anchor) : IChromeOverlay
{
    /// <summary>The index of the row that leads back to the parent menu.</summary>
    private const int BackIndex = -1;

    /// <summary>The rows a separator's height is divided by.</summary>
    private const int SeparatorShare = 3;

    /// <summary>The mark before a ticked entry.</summary>
    private const string Tick = "● ";

    /// <summary>The mark after an entry that opens a submenu.</summary>
    private const string More = "  ›";

    /// <summary>The menus above the one shown.</summary>
    private readonly Stack<IReadOnlyList<MenuEntry>> _parents = [];

    /// <summary>The entries shown.</summary>
    private IReadOnlyList<MenuEntry> _shown = entries;

    /// <summary>The entry the keyboard is on, or less than <see cref="BackIndex"/> for none.</summary>
    private int _cursor = BackIndex - 1;

    /// <inheritdoc/>
    public bool IsModal => false;

    /// <inheritdoc/>
    public bool TakesAllKeys => true;

    /// <inheritdoc/>
    public bool ClosesOnOutsidePress => true;

    /// <inheritdoc/>
    public int Version { get; private set; }

    /// <inheritdoc/>
    public void Build(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, SurfaceSize size)
    {
        var style = ChromeBars.Style(metrics);
        var width = metrics.PopupWidth;
        var height = _parents.Count > 0 ? metrics.RowHeight : 0;
        for (var i = 0; i < _shown.Count; i++)
        {
            var entry = _shown[i];
            height += RowHeight(entry, metrics);
            var gesture = entry.Gesture is { } keys ? runs.Measure(keys, style) + metrics.Padding : 0;
            width = Math.Max(width, runs.Measure(Header(entry), style) + gesture + (metrics.Padding * SeparatorShare));
        }

        var left = Math.Max(0, Math.Min(anchor.X, size.Width - width));
        var top = Math.Max(0, Math.Min(anchor.Y, size.Height - height));
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, new(left, top, width, height), true, false));
        var y = top;
        if (_parents.Count > 0)
        {
            items.Add(new(ChromeItemKind.Row, ChromeCommand.MenuEntry, BackIndex, UiText.Get("‹ Back"), new(left, y, width, metrics.RowHeight), true, _cursor == BackIndex));
            y += metrics.RowHeight;
        }

        for (var i = 0; i < _shown.Count; i++)
        {
            var bounds = new LayoutRect(left, y, width, RowHeight(_shown[i], metrics));
            y += (int)bounds.Height;
            AddEntry(items, runs, metrics, i, bounds);
        }
    }

    /// <inheritdoc/>
    public bool Press(ChromeItem item)
    {
        if (item.Command != ChromeCommand.MenuEntry)
        {
            return true;
        }

        return item.Index == BackIndex ? GoBack() : Run(_shown[item.Index]);
    }

    /// <inheritdoc/>
    public bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.Down:
                {
                    MoveCursor(1);
                    return true;
                }

            case ViewerKey.Up:
                {
                    MoveCursor(-1);
                    return true;
                }

            case ViewerKey.Enter or ViewerKey.Space or ViewerKey.Right when _cursor >= 0 && _cursor < _shown.Count:
                {
                    return Run(_shown[_cursor]);
                }

            case ViewerKey.Escape or ViewerKey.Left or ViewerKey.Enter:
                {
                    return GoBack();
                }

            default:
                {
                    return true;
                }
        }
    }

    /// <inheritdoc/>
    public void Type(int scalar)
    {
        // A menu has nothing to type into.
    }

    /// <inheritdoc/>
    public void Scroll(int rows)
    {
        // A menu shows all of its entries, so there is nothing to scroll.
    }

    /// <inheritdoc/>
    public void Dismiss()
    {
        // A menu that closes without a choice does nothing.
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

    /// <summary>Gets the height of an entry's row.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The height in device pixels.</returns>
    private static int RowHeight(MenuEntry entry, in ChromeMetrics metrics) =>
        entry.Kind == MenuEntryKind.Separator ? metrics.RowHeight / SeparatorShare : metrics.RowHeight;

    /// <summary>Adds the row of one entry, and its shortcut on the right when it has one.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="index">The entry.</param>
    /// <param name="bounds">Where the row goes.</param>
    private void AddEntry(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, int index, in LayoutRect bounds)
    {
        var entry = _shown[index];
        if (entry.Kind == MenuEntryKind.Separator)
        {
            items.Add(new(ChromeItemKind.Separator, ChromeCommand.None, index, string.Empty, bounds, true, false));
            return;
        }

        var pressable = entry.IsEnabled && (entry.Command is not null || entry.Kind == MenuEntryKind.Submenu);
        items.Add(new(ChromeItemKind.Row, pressable ? ChromeCommand.MenuEntry : ChromeCommand.None, index, Header(entry), bounds, entry.IsEnabled, index == _cursor));
        if (entry.Gesture is not { } keys)
        {
            return;
        }

        var keysWidth = runs.Measure(keys, ChromeBars.Style(metrics)) + metrics.Padding + metrics.Padding;
        items.Add(new(ChromeItemKind.Label, ChromeCommand.None, index, keys, new(bounds.Right - keysWidth, bounds.Y, keysWidth, bounds.Height), false, false));
    }

    /// <summary>Tells whether the keyboard can stop on an entry.</summary>
    /// <param name="index">The entry, or <see cref="BackIndex"/>.</param>
    /// <returns><see langword="true"/> when it can be pressed.</returns>
    private bool CanStopOn(int index) => index == BackIndex || (_shown[index].Kind != MenuEntryKind.Separator && _shown[index].IsEnabled);

    /// <summary>Runs an entry, or opens its submenu.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> when the menu stays open.</returns>
    private bool Run(MenuEntry entry)
    {
        if (!entry.IsEnabled)
        {
            return true;
        }

        if (entry.Kind == MenuEntryKind.Submenu)
        {
            _parents.Push(_shown);
            _shown = entry.Children;
            _cursor = BackIndex - 1;
            Version++;
            return true;
        }

        if (entry.Command is not { } command || !command.CanExecute(entry.Parameter))
        {
            return true;
        }

        command.Execute(entry.Parameter);
        return false;
    }

    /// <summary>Goes back to the parent menu, or closes the top menu.</summary>
    /// <returns><see langword="true"/> when a parent menu is now shown.</returns>
    private bool GoBack()
    {
        if (_parents.Count == 0)
        {
            return false;
        }

        _shown = _parents.Pop();
        _cursor = BackIndex - 1;
        Version++;
        return true;
    }

    /// <summary>Moves the keyboard to the next entry that can be pressed.</summary>
    /// <param name="step">1 to move down, -1 to move up.</param>
    private void MoveCursor(int step)
    {
        var first = _parents.Count > 0 ? BackIndex : 0;
        var count = _shown.Count - first;
        if (count <= 0)
        {
            return;
        }

        // With no entry chosen yet, down starts at the first entry and up at the last.
        var at = _cursor;
        if (at < first)
        {
            at = step > 0 ? first - 1 : _shown.Count;
        }

        for (var tries = 0; tries < count; tries++)
        {
            at = first + ((at - first + step + count) % count);
            if (!CanStopOn(at))
            {
                continue;
            }

            _cursor = at;
            Version++;
            return;
        }
    }
}

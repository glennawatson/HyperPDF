// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Windows.Input;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Platform;
using HyperPdf.ViewModels;
using HyperPdf.Viewer.Icons;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// What the window shows while no document is open: a button to open one and the documents opened lately, so the first thing to do
/// is in front of the user and not behind a menu.
/// </summary>
internal static class StartOverlays
{
    /// <summary>The most recent documents listed.</summary>
    private const int RecentLimit = 12;

    /// <summary>The paddings in the width of the button that forgets a document: one on each side and one between its icon and its text.</summary>
    private const int RemovePaddings = 3;

    /// <summary>The lines of text a document opened lately takes: its name and its folder.</summary>
    private const int RowLines = 2;

    /// <summary>
    /// Opens the start view. Each control runs a command of the main view model: the button opens a file, a document opened lately
    /// opens again, and its own button forgets it. The view never closes by itself: the window takes it away when a document opens.
    /// </summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The start view.</returns>
    internal static Overlay Open(MainViewModel main)
    {
        var input = new Signal<OverlayInput>();
        var remove = ReactiveCommand.Create<RecentDocument>(main.RemoveRecent);
        var actions = new StartActions(main.OpenCommand, main.OpenRecentCommand, remove, main.ClearRecentCommand);
        return new(
            OverlayKind.Start,
            OverlayTraits.TakesAllKeys | OverlayTraits.StaysUnder,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, main, actions, in place),
            input,
            ((IList)main.RecentDocuments).CollectionChanges.AsSignal(),
            Signal.Never<RxVoid>(),
            new MultipleDisposable(input, remove));
    }

    /// <summary>Lays the start view out over the part of the window left for the pages.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="main">The main view model, which knows the documents opened lately.</param>
    /// <param name="actions">The commands the controls run.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, MainViewModel main, StartActions actions, in OverlayPlace place)
    {
        var metrics = place.Metrics;
        var area = place.Area;
        var width = (int)Math.Min(area.Width, metrics.DialogWidth);
        var left = (int)area.X + (((int)area.Width - width) >> 1);
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, area, true, false));
        var y = AddWelcome(items, in place, actions.Open, new(left, (int)area.Y + metrics.RowHeight, width, 0));
        var count = Math.Min(main.RecentDocuments.Count, RecentLimit);
        if (count == 0)
        {
            return;
        }

        // The heading and the button that clears the list share a line, with a rule under them that sets the list apart.
        var style = ChromeBars.Style(metrics);
        var clear = UiText.Get("Clear list");
        var clearWidth = Math.Min(width >> 1, place.Runs.Measure(clear, style) + metrics.IconSize + (metrics.Padding * RemovePaddings));
        items.Add(new(ChromeItemKind.Heading, null, 0, UiText.Get("Opened lately"), new(left, y, width - clearWidth, metrics.RowHeight), true, false));

        // It has the same mark as the button that removes one file, so the two read as the same kind of thing.
        items.Add(new(ChromeItemKind.Button, actions.Clear, 0, clear, new(left + width - clearWidth, y, clearWidth, metrics.RowHeight), true, false) { Icon = ChromeIcon.Close });
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(left, y, width, metrics.Line), true, false));
        y += metrics.Line;
        var rowHeight = metrics.RowHeight + (metrics.RowHeight >> 1);
        for (var i = 0; i < count && y + rowHeight <= area.Bottom; i++)
        {
            AddRecent(items, in place, actions, main.RecentDocuments[i], i, new(left, y, width, rowHeight));
            y += rowHeight;
            items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(left, y, width, metrics.Line), true, false));
            y += metrics.Line;
        }
    }

    /// <summary>Adds the top of the start view: a large document icon, what to do, the button that opens a file and a hint, each in the middle.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="place">What to lay them out with.</param>
    /// <param name="open">The command that asks for a file and opens it.</param>
    /// <param name="column">The left edge, the top and the width of the column the controls are in.</param>
    /// <returns>The top of what comes after.</returns>
    private static int AddWelcome(List<ChromeItem> items, in OverlayPlace place, ICommand open, in LayoutRect column)
    {
        var runs = place.Runs;
        var metrics = place.Metrics;
        var style = ChromeBars.Style(metrics);
        var left = (int)column.X;
        var width = (int)column.Width;
        var y = (int)column.Y;
        var picture = metrics.RowHeight + metrics.RowHeight;
        items.Add(new(ChromeItemKind.Picture, null, 0, string.Empty, new(left, y, width, picture), false, false) { Icon = ChromeIcon.Document, IconSize = picture });
        y += picture + metrics.Gap;
        var title = ChromeText.Fit(UiText.Get("No document is open"), style with { Bold = true }, width - metrics.Padding - metrics.Padding, runs);
        items.Add(new(ChromeItemKind.Heading, null, 0, title, Centred(runs.Measure(title, style with { Bold = true }), left, y, width, metrics), true, false));
        y += metrics.RowHeight;
        var hint = ChromeText.Fit(UiText.Get("Drop PDF files here, or choose them from a folder."), style, width - metrics.Padding - metrics.Padding, runs);
        items.Add(new(ChromeItemKind.Label, null, 0, hint, Centred(runs.Measure(hint, style), left, y, width, metrics), true, false));
        y += metrics.RowHeight + metrics.Gap;

        // The button has a box of its own, so the one thing to do first stands out from the text round it.
        var text = UiText.Get("Open a document…");
        var buttonHeight = metrics.RowHeight + metrics.Gap + metrics.Gap;
        var box = Centred(runs.Measure(text, style) + metrics.IconSize + metrics.Padding + metrics.Padding, left, y, width, metrics) with { Height = buttonHeight };
        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, box, true, false));
        items.Add(new(ChromeItemKind.Button, open, 0, text, box, true, false) { Icon = ChromeIcon.Open });
        return y + buttonHeight + metrics.RowHeight;
    }

    /// <summary>Adds one document opened lately: its icon, its name over the folder it is in, and the button that forgets it.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="place">What to lay them out with.</param>
    /// <param name="actions">The commands the controls run.</param>
    /// <param name="recent">The document.</param>
    /// <param name="index">Its place in the list.</param>
    /// <param name="bounds">Where the row goes.</param>
    private static void AddRecent(List<ChromeItem> items, in OverlayPlace place, StartActions actions, RecentDocument recent, int index, in LayoutRect bounds)
    {
        var path = recent.FilePath;
        var runs = place.Runs;
        var metrics = place.Metrics;
        var style = ChromeBars.Style(metrics);
        var remove = UiText.Get("Remove");
        var removeWidth = Math.Min((int)bounds.Width >> 1, runs.Measure(remove, style) + metrics.IconSize + (metrics.Padding * RemovePaddings));
        var rowWidth = (int)bounds.Width - removeWidth;
        var textLeft = metrics.Padding + metrics.IconSize;
        var textRoom = rowWidth - textLeft - metrics.Padding - metrics.Padding;
        var half = Math.Floor(bounds.Height / RowLines);

        // The row itself carries the name for a screen reader and draws the icon; the name and the folder are drawn over it on two lines.
        var name = Path.GetFileName(path);
        var row = new LayoutRect(bounds.X, bounds.Y, rowWidth, bounds.Height);
        items.Add(new(ChromeItemKind.Row, actions.OpenRecent, index, name, row, true, false) { Parameter = recent, Icon = ChromeIcon.Document, HidesText = true });
        var nameBounds = new LayoutRect(bounds.X + textLeft, bounds.Y + metrics.Line, textRoom + metrics.Padding + metrics.Padding, half);
        items.Add(new(ChromeItemKind.Picture, null, index, ChromeText.Fit(name, style, textRoom, runs), nameBounds, true, false));
        var small = style with { Size = metrics.TextSize - metrics.Line - metrics.Line };
        var folder = ChromeText.Fit(Path.GetDirectoryName(path) ?? string.Empty, small, textRoom, runs);
        var folderBounds = new LayoutRect(nameBounds.X, bounds.Y + half - metrics.Line, nameBounds.Width, half);
        items.Add(new(ChromeItemKind.Label, null, index, folder, folderBounds, false, false) { TextSize = small.Size });
        var forget = new LayoutRect(bounds.X + rowWidth, bounds.Y, removeWidth, bounds.Height);
        items.Add(new(ChromeItemKind.Button, actions.Remove, index, remove, forget, true, false) { Parameter = recent, Icon = ChromeIcon.Close });
    }

    /// <summary>Finds the place of a control one row high whose text sits in the middle of the column.</summary>
    /// <param name="textWidth">The width of the text.</param>
    /// <param name="left">The left edge of the column.</param>
    /// <param name="top">The top of the control.</param>
    /// <param name="width">The width of the column.</param>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The place.</returns>
    private static LayoutRect Centred(int textWidth, int left, int top, int width, in ChromeMetrics metrics)
    {
        var wide = Math.Min(width, textWidth + metrics.Padding + metrics.Padding);
        return new(left + ((width - wide) >> 1), top, wide, metrics.RowHeight);
    }
}

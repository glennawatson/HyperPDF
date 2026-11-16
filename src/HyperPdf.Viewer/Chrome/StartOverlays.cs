// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using HyperPdf.Core.Layout;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Text;
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

    /// <summary>The index of the row that opens the file dialog.</summary>
    private const int OpenIndex = -1;

    /// <summary>The index of the row that clears the list of documents opened lately.</summary>
    private const int ClearIndex = -2;

    /// <summary>The index of the button that forgets the first document opened lately; the buttons of the ones after it have indexes below it.</summary>
    private const int RemoveIndex = -3;

    /// <summary>How many rows wide the button that forgets a document is.</summary>
    private const int RemoveRows = 4;

    /// <summary>
    /// Opens the start view. A press on the open row runs the open command, and a press on a document opened lately opens it again.
    /// The view never closes by itself: the window takes it away when a document opens.
    /// </summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The start view.</returns>
    internal static Overlay Open(MainViewModel main)
    {
        var input = new Signal<OverlayInput>();
        var rows = input.SelectPressedItems().Where(static item => item.Command == ChromeCommand.PickRow).Select(static item => item.Index);
        return new(
            OverlayKind.Start,
            OverlayTraits.TakesAllKeys | OverlayTraits.StaysUnder,
            (List<ChromeItem> items, in OverlayPlace place) => Build(items, main, in place),
            input,
            ((IList)main.RecentDocuments).CollectionChanges.AsSignal(),
            Signal.Never<RxVoid>(),
            new MultipleDisposable(
                input,
                rows.Where(static index => index == OpenIndex).AsSignal().InvokeCommand(main.OpenCommand),
                rows.Where(static index => index == ClearIndex).AsSignal().InvokeCommand(main.ClearRecentCommand),
                rows.Where(index => (uint)index < (uint)main.RecentDocuments.Count).Select(index => main.RecentDocuments[index]).InvokeCommand(main.OpenRecentCommand),
                rows.Where(static index => index <= RemoveIndex)
                    .Select(static index => RemoveIndex - index)
                    .Where(index => (uint)index < (uint)main.RecentDocuments.Count)
                    .SubscribeSafe(index => main.RemoveRecent(main.RecentDocuments[index]), ErrorTrace.Write)));
    }

    /// <summary>Lays the start view out over the part of the window left for the pages.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="main">The main view model, which knows the documents opened lately.</param>
    /// <param name="place">What to lay them out with.</param>
    private static void Build(List<ChromeItem> items, MainViewModel main, in OverlayPlace place)
    {
        var runs = place.Runs;
        var metrics = place.Metrics;
        var area = place.Area;
        var style = ChromeBars.Style(metrics);
        var width = (int)Math.Min(area.Width, metrics.DialogWidth);
        var left = (int)area.X + (((int)area.Width - width) >> 1);
        var y = (int)area.Y + metrics.RowHeight;
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, area, true, false));
        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, ChromeText.Fit(UiText.Get("No document is open"), style, width, runs), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        items.Add(new(ChromeItemKind.Button, ChromeCommand.PickRow, OpenIndex, UiText.Get("Open a document…"), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        var hint = ChromeText.Fit(UiText.Get("Drop PDF files here, or choose them from a folder."), style, width, runs);
        items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, hint, new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight + metrics.RowHeight;
        var count = Math.Min(main.RecentDocuments.Count, RecentLimit);
        if (count == 0)
        {
            return;
        }

        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, UiText.Get("Opened lately"), new(left, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        var removeWidth = Math.Min(width >> 1, metrics.RowHeight * RemoveRows);
        var rowWidth = width - removeWidth;
        var remove = UiText.Get("Remove");
        for (var i = 0; i < count && y + metrics.RowHeight <= area.Bottom; i++)
        {
            var name = ChromeText.Fit(Path.GetFileName(main.RecentDocuments[i].FilePath), style, rowWidth - metrics.Padding - metrics.Padding, runs);
            items.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, i, name, new(left, y, rowWidth, metrics.RowHeight), true, false));
            var forget = new LayoutRect(left + rowWidth, y, removeWidth, metrics.RowHeight);
            items.Add(new(ChromeItemKind.Button, ChromeCommand.PickRow, RemoveIndex - i, ChromeText.Fit(remove, style, removeWidth, runs), forget, true, false));
            y += metrics.RowHeight;
        }

        if (y + metrics.RowHeight <= area.Bottom)
        {
            items.Add(new(ChromeItemKind.Button, ChromeCommand.PickRow, ClearIndex, UiText.Get("Clear list"), new(left, y, width, metrics.RowHeight), true, false));
        }
    }
}

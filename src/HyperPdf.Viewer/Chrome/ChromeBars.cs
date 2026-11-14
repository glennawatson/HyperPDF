// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the bars that are always in the window: the tab strip, the toolbar and the status bar.</summary>
internal static class ChromeBars
{
    /// <summary>The mark after the name of a tab with changes that are not saved.</summary>
    private const string UnsavedMark = " •";

    /// <summary>The text of the button that closes a tab.</summary>
    private const string CloseText = "×";

    /// <summary>The text of the button that opens a file in a new tab.</summary>
    private const string NewTabText = "+";

    /// <summary>One hundred, to show a zoom as a percentage.</summary>
    private const double Percent = 100;

    /// <summary>Makes a number that changes whenever what the bars show changes, so they are only laid out again when needed.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="size">The window size in device pixels.</param>
    /// <param name="scale">The display scale.</param>
    /// <param name="sidebarVersion">The version of the side panel's scroll position and open bookmarks.</param>
    /// <returns>The number.</returns>
    internal static int Stamp(MainViewModel main, SurfaceSize size, double scale, int sidebarVersion)
    {
        var hash = default(HashCode);
        hash.Add(size.Width);
        hash.Add(size.Height);
        hash.Add(scale);
        hash.Add(sidebarVersion);
        var tabs = main.Tabs;
        hash.Add(tabs.Count);
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            hash.Add(RuntimeHelpers.GetHashCode(tab));
            hash.Add(RuntimeHelpers.GetHashCode(tab.Title));
            hash.Add(tab.HasUnsavedChanges);
        }

        if (main.SelectedTab is { } selected)
        {
            hash.Add(RuntimeHelpers.GetHashCode(selected));
            hash.Add(selected.CurrentPageIndex);
            hash.Add(selected.PageCount);
            hash.Add(selected.Zoom);
            hash.Add(selected.IsLoaded);
            hash.Add(selected.SidebarVisible);
            hash.Add(RuntimeHelpers.GetHashCode(selected.Outline));
            hash.Add(selected.SelectedOutlineItem is { } bookmark ? RuntimeHelpers.GetHashCode(bookmark) : 0);
        }

        if (main.StatusMessage is { } status)
        {
            hash.Add(RuntimeHelpers.GetHashCode(status));
        }

        return hash.ToHashCode();
    }

    /// <summary>Lays the bars out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="sidebar">The side panel's rows and scroll position.</param>
    /// <param name="size">The window size in device pixels.</param>
    /// <returns>The part of the window left for the pages, in device pixels.</returns>
    internal static LayoutRect Build(List<ChromeItem> items, MainViewModel main, ChromeLook look, SidebarState sidebar, SurfaceSize size)
    {
        var runs = look.Runs;
        var metrics = look.Metrics;
        items.Clear();
        var top = metrics.TabHeight + metrics.ToolbarHeight;
        var statusTop = Math.Max(top, size.Height - metrics.StatusHeight);
        AddTabs(items, main, runs, metrics, size.Width);
        AddToolbar(items, main, runs, metrics, size.Width);
        items.Add(new(ChromeItemKind.Panel, ChromeCommand.None, 0, string.Empty, new(0, statusTop, size.Width, metrics.StatusHeight), true, false));
        var status = main.StatusMessage ?? main.SelectedTab?.ErrorMessage ?? string.Empty;
        if (status.Length > 0)
        {
            var room = size.Width - metrics.Padding - metrics.Padding;
            var fitted = ChromeText.Fit(status, Style(metrics), room, runs);
            items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, fitted, new(metrics.Padding, statusTop, room, metrics.StatusHeight), true, false));
        }

        var between = new LayoutRect(0, top, size.Width, Math.Max(0, statusTop - top));
        sidebar.Bounds = default;

        // A document with no bookmarks has nothing to list, so the panel would only take room from the pages.
        if (main.SelectedTab is not { SidebarVisible: true, IsLoaded: true, Outline.Count: > 0 } tab)
        {
            return between;
        }

        var panel = ChromeSidebar.Build(items, tab, sidebar, look, between);
        return new(panel, top, size.Width - panel, between.Height);
    }

    /// <summary>Gets the style bar text is measured with. The colour is chosen when the text is drawn.</summary>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextStyle Style(in ChromeMetrics metrics) => new(metrics.TextSize, false, 0);

    /// <summary>Adds the tab strip.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="width">The window width.</param>
    private static void AddTabs(List<ChromeItem> items, MainViewModel main, TextRuns runs, in ChromeMetrics metrics, int width)
    {
        items.Add(new(ChromeItemKind.Panel, ChromeCommand.None, 0, string.Empty, new(0, 0, width, metrics.TabHeight), true, false));
        var tabs = main.Tabs;
        var closeWidth = metrics.TabHeight - metrics.Gap - metrics.Gap;
        var newTabWidth = metrics.TabHeight;
        var tabWidth = tabs.Count == 0 ? 0 : Math.Min(metrics.TabWidth, Math.Max(closeWidth + metrics.Padding, (width - newTabWidth) / tabs.Count));
        var style = Style(metrics);
        var x = 0;
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            var name = tab.HasUnsavedChanges ? string.Concat(tab.Title, UnsavedMark) : tab.Title;
            var room = tabWidth - metrics.Padding - closeWidth - metrics.Gap;
            var selected = ReferenceEquals(tab, main.SelectedTab);
            items.Add(new(ChromeItemKind.Tab, ChromeCommand.SelectTab, i, ChromeText.Fit(name, style, room, runs), new(x, 0, tabWidth, metrics.TabHeight), true, selected));
            var close = new LayoutRect(x + tabWidth - closeWidth - metrics.Gap, metrics.Gap, closeWidth, closeWidth);
            items.Add(new(ChromeItemKind.Button, ChromeCommand.CloseTab, i, CloseText, close, true, false));
            x += tabWidth;
        }

        items.Add(new(ChromeItemKind.Button, ChromeCommand.Open, 0, NewTabText, new(x, 0, newTabWidth, metrics.TabHeight), true, false));
    }

    /// <summary>Adds the toolbar.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="width">The window width.</param>
    private static void AddToolbar(List<ChromeItem> items, MainViewModel main, TextRuns runs, in ChromeMetrics metrics, int width)
    {
        items.Add(new(ChromeItemKind.Panel, ChromeCommand.None, 0, string.Empty, new(0, metrics.TabHeight, width, metrics.ToolbarHeight), true, false));
        var row = new ChromeRow(items, runs, metrics, metrics.Gap, metrics.TabHeight + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap);
        var tab = main.SelectedTab;
        var loaded = tab is { IsLoaded: true };
        row.Button(UiText.Get("Menu"), ChromeCommand.Menu, true);
        row.Button(UiText.Get("Open"), ChromeCommand.Open, true);
        row.Button(UiText.Get("Save"), ChromeCommand.Save, tab is { HasUnsavedChanges: true });
        row.Space();
        row.Button(UiText.Get("Previous"), ChromeCommand.PreviousPage, loaded && tab!.CurrentPageIndex > 0);
        row.Label(PageText(tab));
        row.Button(UiText.Get("Next"), ChromeCommand.NextPage, loaded && tab!.CurrentPageIndex < tab.PageCount - 1);
        row.Space();
        row.Button(UiText.Get("Zoom out"), ChromeCommand.ZoomOut, loaded);
        row.Label(ZoomText(tab));
        row.Button(UiText.Get("Zoom in"), ChromeCommand.ZoomIn, loaded);
        row.Button(UiText.Get("Fit width"), ChromeCommand.FitWidth, loaded);
        row.Button(UiText.Get("Fit page"), ChromeCommand.FitPage, loaded);
        row.Space();
        row.Button(UiText.Get("Find"), ChromeCommand.Find, loaded);
        row.Button(UiText.Get("Bookmarks"), ChromeCommand.Sidebar, loaded && tab!.Outline.Count > 0);
    }

    /// <summary>Says which page is shown.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/>.</param>
    /// <returns>The text, such as "Page 3 of 40".</returns>
    private static string PageText(DocumentTabViewModel? tab) =>
        tab is { PageCount: > 0 } ? UiText.Format("Page {0} of {1}", tab.CurrentPageIndex + 1, tab.PageCount) : UiText.Get("No pages");

    /// <summary>Says how large the pages are shown.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/>.</param>
    /// <returns>The text, such as "125%".</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string ZoomText(DocumentTabViewModel? tab) =>
        string.Create(CultureInfo.CurrentCulture, $"{Math.Round((tab?.Zoom ?? 1) * Percent)}%");
}

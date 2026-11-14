// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the side panel, which lists the document's bookmarks.</summary>
internal static class ChromeSidebar
{
    /// <summary>The space that sets a nested bookmark in from its parent.</summary>
    private const string Indent = "    ";

    /// <summary>The mark before a bookmark whose children are hidden.</summary>
    private const string ClosedMark = "+ ";

    /// <summary>The mark before a bookmark whose children are shown.</summary>
    private const string OpenMark = "– ";

    /// <summary>The space before a bookmark with no children, as wide as a mark.</summary>
    private const string NoMark = "   ";

    /// <summary>Lays the panel out down the left of the page area.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="state">The panel's rows and scroll position, which are brought up to date.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="area">The part of the window between the toolbar and the status bar.</param>
    /// <returns>The width the panel takes, in device pixels.</returns>
    internal static int Build(List<ChromeItem> items, DocumentTabViewModel tab, SidebarState state, ChromeLook look, in LayoutRect area)
    {
        var metrics = look.Metrics;
        var width = (int)Math.Min(metrics.SidebarWidth, area.Width);
        state.Rows.Clear();
        Flatten(tab.Outline, 0, state.Rows);
        state.Bounds = new(area.X, area.Y, width, area.Height);
        state.VisibleRows = Math.Max(0, ((int)area.Height / metrics.RowHeight) - 1);
        state.First = Math.Clamp(state.First, 0, Math.Max(0, state.Rows.Count - state.VisibleRows));
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, state.Bounds, true, false));
        var y = (int)area.Y;
        items.Add(new(ChromeItemKind.Heading, ChromeCommand.None, 0, UiText.Get("Bookmarks"), new(area.X, y, width, metrics.RowHeight), true, false));
        y += metrics.RowHeight;
        var last = Math.Min(state.Rows.Count, state.First + state.VisibleRows);
        for (var i = state.First; i < last; i++)
        {
            var row = state.Rows[i];
            var text = ChromeText.Fit(Describe(row), ChromeBars.Style(metrics), width - metrics.Padding - metrics.Padding, look.Runs);
            items.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, i, text, new(area.X, y, width, metrics.RowHeight), true, ReferenceEquals(row.Item, tab.SelectedOutlineItem)));
            y += metrics.RowHeight;
        }

        return width;
    }

    /// <summary>Lists bookmarks in order, each open one followed by its children.</summary>
    /// <param name="bookmarks">The bookmarks of one level.</param>
    /// <param name="depth">How deep the level is nested.</param>
    /// <param name="rows">Receives the rows.</param>
    private static void Flatten(IReadOnlyList<OutlineItemViewModel> bookmarks, int depth, List<SidebarRow> rows)
    {
        for (var i = 0; i < bookmarks.Count; i++)
        {
            var bookmark = bookmarks[i];
            rows.Add(new(bookmark, depth));
            if (bookmark.IsExpanded)
            {
                Flatten(bookmark.Children, depth + 1, rows);
            }
        }
    }

    /// <summary>Makes the text of a row: set in by its depth, with a mark that says whether it has children and whether they are shown.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The text.</returns>
    private static string Describe(SidebarRow row)
    {
        var mark = NoMark;
        if (row.Item.Children.Count > 0)
        {
            mark = row.Item.IsExpanded ? OpenMark : ClosedMark;
        }

        return row.Depth == 0 ? string.Concat(mark, row.Item.Title) : string.Concat(string.Concat(Enumerable.Repeat(Indent, row.Depth)), mark, row.Item.Title);
    }
}

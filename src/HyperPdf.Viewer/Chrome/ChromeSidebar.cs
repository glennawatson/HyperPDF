// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the side panel: a row of tabs for pages, bookmarks, notes, files and layers, and the list of the tab it is on.</summary>
internal static class ChromeSidebar
{
    /// <summary>The share of the panel's width a page picture takes.</summary>
    private const double PictureShare = 0.5;

    /// <summary>Half, for centring.</summary>
    private const double Half = 0.5;

    /// <summary>Lays the panel out down the left of the page area.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="view">The panel's scroll position, which the rows are brought up to date under.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="area">The part of the window between the toolbar and the status bar.</param>
    /// <returns>The width the panel takes, and what it lists now.</returns>
    internal static SidebarLayout Build(List<ChromeItem> items, DocumentTabViewModel tab, SidebarView view, ChromeLook look, in LayoutRect area)
    {
        var metrics = look.Metrics;
        var width = (int)Math.Min(metrics.SidebarWidth, area.Width);
        var listed = new List<SidebarRow>();
        SidebarRows.Fill(listed, tab);
        var bounds = new LayoutRect(area.X, area.Y, width, area.Height);
        var first = Math.Clamp(view.First, 0, Math.Max(0, listed.Count - 1));
        items.Add(new(ChromeItemKind.Popup, ChromeCommand.None, 0, string.Empty, bounds, true, false));
        var top = (int)area.Y + AddTabs(items, tab, look, new(area.X, area.Y, width, area.Height));
        var bottom = (int)area.Bottom;
        if (listed.Count == 0)
        {
            var none = ChromeText.Fit(SidebarRows.EmptyText(tab.SidebarMode), ChromeBars.Style(metrics), width - metrics.Padding - metrics.Padding, look.Runs);
            items.Add(new(ChromeItemKind.Label, ChromeCommand.None, 0, none, new(area.X, top, width, metrics.RowHeight), false, false));
            return new(width, new([], first, bounds, 0));
        }

        var shown = 0;
        for (var i = first; i < listed.Count; i++)
        {
            var height = AddRow(items, tab, listed[i], i, look, new(area.X, top, width, bottom - top));
            if (height == 0)
            {
                break;
            }

            top += height;
            shown++;
        }

        return new(width, new([.. listed], first, bounds, shown));
    }

    /// <summary>Adds the row of tabs that choose what the panel lists.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="area">The panel's area.</param>
    /// <returns>The height the tabs take, which is more than one line when the names do not fit across the panel.</returns>
    private static int AddTabs(List<ChromeItem> items, DocumentTabViewModel tab, ChromeLook look, in LayoutRect area)
    {
        // The five names share the panel's width, so they stand closer together than toolbar buttons.
        var tight = look.Metrics with { Padding = look.Metrics.Gap + look.Metrics.Gap };
        var height = tight.ToolbarHeight - tight.Gap - tight.Gap;
        var row = new ChromeRow(items, look.Runs, tight, (int)area.X + tight.Gap, (int)area.Y + tight.Gap, height) { Limit = (int)area.Right - tight.Gap };
        foreach (var mode in SidebarRows.Modes)
        {
            row = row.Button(SidebarRows.NameOf(mode), ChromeCommand.SidebarMode, (int)mode, mode == tab.SidebarMode);
        }

        return row.Lines * tight.ToolbarHeight;
    }

    /// <summary>Adds one row of the list when it fits: a page with its picture and number, or a line of text.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's place in the list.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="room">The part of the panel still free.</param>
    /// <returns>The height the row took, or 0 when it does not fit.</returns>
    private static int AddRow(List<ChromeItem> items, DocumentTabViewModel tab, SidebarRow row, int index, ChromeLook look, in LayoutRect room)
    {
        var metrics = look.Metrics;
        if (row.Page < 0)
        {
            if (room.Height < metrics.RowHeight)
            {
                return 0;
            }

            var text = ChromeText.Fit(row.Text, ChromeBars.Style(metrics), (int)room.Width - metrics.Padding - metrics.Padding, look.Runs);
            items.Add(new(ChromeItemKind.Row, ChromeCommand.PickRow, index, text, new(room.X, room.Y, room.Width, metrics.RowHeight), row.Enabled, row.Selected));
            return metrics.RowHeight;
        }

        var size = tab.Source.PageSizes[row.Page];
        var pictureWidth = Math.Floor(room.Width * PictureShare);
        var pictureHeight = Math.Floor(pictureWidth * size.Height / Math.Max(1, size.Width));
        var height = (int)pictureHeight + metrics.RowHeight + metrics.Gap;
        if (room.Height < height)
        {
            return 0;
        }

        var left = room.X + Math.Floor((room.Width - pictureWidth) * Half);
        items.Add(new(ChromeItemKind.Thumbnail, ChromeCommand.PickRow, index, string.Empty, new(left, room.Y + metrics.Gap, pictureWidth, pictureHeight), true, row.Selected));
        items.Add(new(ChromeItemKind.Button, ChromeCommand.PickRow, index, row.Text, new(room.X, room.Y + metrics.Gap + pictureHeight, room.Width, metrics.RowHeight), true, row.Selected));
        return height;
    }
}

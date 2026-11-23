// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Icons;

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
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <param name="follows">Whether the list of pages follows the page being read.</param>
    /// <returns>The width the panel takes, and what it lists now.</returns>
    internal static SidebarLayout Build(List<ChromeItem> items, DocumentTabViewModel tab, SidebarView view, ChromeLook look, in LayoutRect area, ChromeActions actions, bool follows)
    {
        var metrics = look.Metrics;
        var width = (int)Math.Min(metrics.SidebarWidth, area.Width);
        var pages = SidebarRows.PagesOf(tab);
        var listed = SidebarRows.List(tab, view);
        var bounds = new LayoutRect(area.X, area.Y, width, area.Height);
        var first = Math.Clamp(view.First, 0, Math.Max(0, listed.Length - 1));
        var followedTab = view.FollowedTab;
        var followedPage = view.FollowedPage;

        // The list moves only when the page being read changes, so a list the reader scrolled by hand is left where it is.
        if (follows && tab.SidebarMode == SidebarMode.Thumbnails && (!ReferenceEquals(followedTab, tab) || followedPage != tab.CurrentPageIndex))
        {
            var last = Math.Max(0, listed.Length - Math.Max(1, view.VisibleRows));
            first = Math.Clamp(SidebarFollow.First(first, view.VisibleRows, listed.Length, tab.CurrentPageIndex), 0, last);
            followedTab = tab;
            followedPage = tab.CurrentPageIndex;
        }

        items.Add(new(ChromeItemKind.Popup, null, 0, string.Empty, bounds, true, false));
        var top = (int)area.Y + AddTabs(items, tab, new(look, actions.ShowSidebarList), new(area.X, area.Y, width, area.Height), actions, follows);
        var bottom = (int)area.Bottom;
        if (listed.Length == 0)
        {
            var none = ChromeText.Fit(SidebarRows.EmptyText(tab.SidebarMode), ChromeBars.Style(metrics), width - metrics.Padding - metrics.Padding, look.Runs);
            items.Add(new(ChromeItemKind.Label, null, 0, none, new(area.X, top, width, metrics.RowHeight), false, false));
            return new(width, view with { Rows = listed, First = first, Bounds = bounds, VisibleRows = 0, FollowedTab = followedTab, FollowedPage = followedPage, ListedPages = pages });
        }

        var shown = 0;
        var rowLook = new SidebarLook(look, actions.PickSidebarRow);

        // Keep the neighbouring page visible when a labelled toolbar wraps or a page is unusually tall.
        var available = bottom - top;
        var captionHeight = metrics.RowHeight + metrics.Gap;
        var halfHeight = available >> 1;
        var maximumPictureHeight = Math.Max(1, (halfHeight > captionHeight ? halfHeight : available) - captionHeight);
        for (var i = first; i < listed.Length; i++)
        {
            var height = AddRow(items, tab, listed[i], i, rowLook, new(area.X, top, width, bottom - top), maximumPictureHeight);
            if (height == 0)
            {
                break;
            }

            top += height;
            shown++;
        }

        return new(width, view with { Rows = listed, First = first, Bounds = bounds, VisibleRows = shown, FollowedTab = followedTab, FollowedPage = followedPage, ListedPages = pages });
    }

    /// <summary>Adds the row of tabs that choose what the panel lists.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="chosen">The text measurer, the sizes and the command a tab runs with the list it stands for.</param>
    /// <param name="area">The panel's area.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <param name="follows">Whether the list of pages follows the page being read.</param>
    /// <returns>The height the tabs and the name under them take.</returns>
    private static int AddTabs(List<ChromeItem> items, DocumentTabViewModel tab, SidebarLook chosen, in LayoutRect area, ChromeActions actions, bool follows)
    {
        var look = chosen.Look;
        var tight = look.Metrics with { Padding = look.Metrics.Gap + look.Metrics.Gap };
        var height = tight.ToolbarHeight - tight.Gap - tight.Gap;
        var row = new ChromeRow(items, look.Runs, tight, (int)area.X + tight.Gap, (int)area.Y + tight.Gap, height)
        {
            Limit = (int)area.Right - tight.Gap,
            ShowLabels = false,
            Describe = ButtonHelp.Sidebar,
        };
        foreach (var mode in SidebarRows.Modes)
        {
            row = row.Button(IconOf(mode), SidebarRows.NameOf(mode), chosen.Pick, new GroupChoice((int)mode, SidebarRows.ValueOf(mode), mode == tab.SidebarMode));
        }

        // The tabs show icons alone so all nine fit on one line; the name of the list shown stands under them, over a rule.
        var top = (int)area.Y + (row.Lines * tight.ToolbarHeight);
        var headerRight = AddHeaderButton(items, tab, look.Metrics, in area, top, actions, follows);
        var titleRoom = Math.Max(1, headerRight - (int)area.X - look.Metrics.Padding - look.Metrics.Padding);
        var name = ChromeText.Fit(SidebarRows.NameOf(tab.SidebarMode), ChromeBars.Style(look.Metrics) with { Bold = true }, titleRoom, look.Runs);
        items.Add(new(ChromeItemKind.Heading, null, 0, name, new(area.X, top, headerRight - area.X, look.Metrics.RowHeight), true, false));
        top += look.Metrics.RowHeight;
        top += AddModeActions(items, tab, look, in area, top, actions);
        items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(area.X, top, area.Width, look.Metrics.Line), true, false));
        return top + look.Metrics.Line - (int)area.Y;
    }

    /// <summary>Adds a single icon action at the right of the side-panel heading when the list shown has one.</summary>
    /// <param name="items">Receives the button.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="area">The panel bounds.</param>
    /// <param name="top">The heading top.</param>
    /// <param name="actions">The window commands.</param>
    /// <param name="follows">Whether thumbnails follow the page being read.</param>
    /// <returns>The right edge left for the heading text.</returns>
    private static int AddHeaderButton(List<ChromeItem> items, DocumentTabViewModel tab, in ChromeMetrics metrics, in LayoutRect area, int top, ChromeActions actions, bool follows)
    {
        var side = metrics.RowHeight;
        var place = new LayoutRect(area.Right - side - metrics.Gap, top, side, side);
        switch (tab.SidebarMode)
        {
            case SidebarMode.Thumbnails:
                {
                    items.Add(new(ChromeItemKind.Button, actions.ToggleFollowPage, 0, UiText.Get("Follow the page being read"), place, true, follows)
                    {
                        Icon = ChromeIcon.Follow,
                        HidesText = true,
                        Trait = ChromeItemTrait.Switch,
                    });
                    return (int)place.X;
                }

            case SidebarMode.Search:
                {
                    items.Add(new(ChromeItemKind.Button, actions.ShowFind, 0, UiText.Get("Find…"), place, true, false) { Icon = ChromeIcon.Search, HidesText = true });
                    return (int)place.X;
                }

            case SidebarMode.Annotations:
                {
                    items.Add(new(ChromeItemKind.Button, tab.Annotations.ShowFilterCommand, 0, UiText.Get("Filter and sort the notes"), place, true, false)
                    {
                        Icon = ChromeIcon.Filter,
                        HidesText = true,
                    });
                    return (int)place.X;
                }

            case SidebarMode.Layers:
                {
                    items.Add(new(ChromeItemKind.Button, tab.Layers.ResetCommand, 0, UiText.Get("Reset layers"), place, true, false) { Icon = ChromeIcon.Undo, HidesText = true });
                    return (int)place.X;
                }

            case SidebarMode.Attachments:
                {
                    items.Add(new(ChromeItemKind.Button, tab.Attachments.AttachFilesCommand, 0, UiText.Get("Attach a file"), place, true, false) { Icon = ChromeIcon.Add, HidesText = true });
                    return (int)place.X;
                }

            default:
                {
                    return (int)area.Right;
                }
        }
    }

    /// <summary>Adds the compact in-place action strip for bookmarks or accessibility tags right inside the side panel.</summary>
    /// <param name="items">Receives the buttons.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="look">The text measurer and sizes.</param>
    /// <param name="area">The panel bounds.</param>
    /// <param name="top">The top of the strip.</param>
    /// <param name="actions">The window commands.</param>
    /// <returns>The height taken by the strip, or 0 when the current tab has none.</returns>
    private static int AddModeActions(List<ChromeItem> items, DocumentTabViewModel tab, ChromeLook look, in LayoutRect area, int top, ChromeActions actions)
    {
        if (tab.SidebarMode is not (SidebarMode.Outline or SidebarMode.Tags))
        {
            return 0;
        }

        var tight = look.Metrics with { Padding = look.Metrics.Gap + look.Metrics.Gap };
        var row = new ChromeRow(items, look.Runs, tight, (int)area.X + tight.Gap, top + tight.Gap, tight.RowHeight - tight.Gap) { Limit = (int)area.Right - tight.Gap, ShowLabels = false };
        if (tab.SidebarMode == SidebarMode.Outline)
        {
            var hasBookmark = tab.SelectedOutlineItem is not null;
            row = row.IconButton(ChromeIcon.Add, UiText.Get("Add bookmark"), actions.AddBookmark, true);
            row = row.IconButton(ChromeIcon.Text, UiText.Get("Rename bookmark"), actions.RenameBookmark, hasBookmark);
            row = row.IconButton(ChromeIcon.PageUp, UiText.Get("Move bookmark up"), actions.MoveBookmarkUp, hasBookmark);
            row = row.IconButton(ChromeIcon.PageDown, UiText.Get("Move bookmark down"), actions.MoveBookmarkDown, hasBookmark);
            row = row.IconButton(ChromeIcon.Indent, UiText.Get("Move bookmark in"), actions.IndentBookmark, hasBookmark);
            row = row.IconButton(ChromeIcon.Outdent, UiText.Get("Move bookmark out"), actions.OutdentBookmark, hasBookmark);
            row = row.IconButton(ChromeIcon.Close, UiText.Get("Remove bookmark"), actions.RemoveBookmark, hasBookmark);
        }
        else
        {
            var tags = tab.Tags;
            var hasTag = tags.SelectedItem is not null;
            row = row.IconButton(ChromeIcon.Tags, UiText.Get("Check accessibility tags"), tags.CheckCommand, true);
            row = row.IconButton(ChromeIcon.Draw, UiText.Get("Edit selected tag"), tags.EditCommand, hasTag);
            row = row.IconButton(ChromeIcon.PageUp, UiText.Get("Read tag earlier"), tags.MoveUpCommand, hasTag);
            row = row.IconButton(ChromeIcon.PageDown, UiText.Get("Read tag later"), tags.MoveDownCommand, hasTag);
            row = row.IconButton(ChromeIcon.Indent, UiText.Get("Put inside tag above"), tags.NestCommand, hasTag);
            row = row.IconButton(ChromeIcon.Outdent, UiText.Get("Take out of parent"), tags.UnnestCommand, hasTag);
            row = row.IconButton(ChromeIcon.Save, UiText.Get("Save tag changes"), tags.ApplyCommand, ((System.Windows.Input.ICommand)tags.ApplyCommand).CanExecute(null));
        }

        return (row.Lines * tight.RowHeight) + tight.Gap;
    }

    /// <summary>Gets the icon of a tab of the side panel.</summary>
    /// <param name="mode">The tab.</param>
    /// <returns>The icon.</returns>
    private static ChromeIcon IconOf(SidebarMode mode) => mode switch
    {
        SidebarMode.Thumbnails => ChromeIcon.Thumbnails,
        SidebarMode.Outline => ChromeIcon.Outline,
        SidebarMode.Search => ChromeIcon.Search,
        SidebarMode.Annotations => ChromeIcon.Annotations,
        SidebarMode.Attachments => ChromeIcon.Attachment,
        SidebarMode.Layers => ChromeIcon.Layers,
        SidebarMode.Tags => ChromeIcon.Tags,
        SidebarMode.Parts => ChromeIcon.Parts,
        _ => ChromeIcon.Media,
    };

    /// <summary>Adds one row of the list when it fits: a page with its picture and number, or a line of text.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's place in the list.</param>
    /// <param name="look">The text measurer, the sizes and the command a row runs with what it stands for.</param>
    /// <param name="room">The part of the panel still free.</param>
    /// <param name="maximumPictureHeight">The available picture height per page.</param>
    /// <returns>The height the row took, or 0 when it does not fit.</returns>
    private static int AddRow(List<ChromeItem> items, DocumentTabViewModel tab, SidebarRow row, int index, SidebarLook look, in LayoutRect room, int maximumPictureHeight)
    {
        var metrics = look.Look.Metrics;
        var pick = look.Pick;
        if (row.Page < 0)
        {
            if (room.Height < metrics.RowHeight + metrics.Line)
            {
                return 0;
            }

            // Each row ends in a rule, so one entry of the list is told from the next without reading both.
            var iconRoom = row.Icon == ChromeIcon.None ? 0 : metrics.IconSize + ChromePainter.IconGap(metrics);
            var text = ChromeText.Fit(row.Text, ChromeBars.Style(metrics), Math.Max(1, (int)room.Width - metrics.Padding - metrics.Padding - iconRoom), look.Look.Runs);
            items.Add(new(ChromeItemKind.Row, pick, index, text, new(room.X, room.Y, room.Width, metrics.RowHeight), row.Enabled, row.Selected) { Parameter = row.Target, Icon = row.Icon });
            items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(room.X, room.Y + metrics.RowHeight, room.Width, metrics.Line), true, false));
            return metrics.RowHeight + metrics.Line;
        }

        var size = tab.Source.PageSizes[row.Page];
        var scale = Math.Min(room.Width * PictureShare / Math.Max(1, size.Width), maximumPictureHeight / Math.Max(1.0, size.Height));
        var pictureWidth = Math.Max(1, Math.Floor(size.Width * scale));
        var pictureHeight = Math.Max(1, Math.Floor(size.Height * scale));
        var height = (int)pictureHeight + metrics.RowHeight + metrics.Gap;
        if (room.Height < height)
        {
            return 0;
        }

        // The rows of the pages are kept from one layout to the next, so which page is being read is asked of the tab here.
        var reading = row.Page == tab.CurrentPageIndex;
        if (reading)
        {
            items.Add(new(ChromeItemKind.Panel, null, index, string.Empty, new(room.X, room.Y, room.Width, height), true, false) { Ground = look.Look.Theme.Hover });
        }

        var left = room.X + Math.Floor((room.Width - pictureWidth) * Half);
        items.Add(new(ChromeItemKind.Thumbnail, pick, index, string.Empty, new(left, room.Y + metrics.Gap, pictureWidth, pictureHeight), true, reading) { Parameter = row.Target });
        var caption = new LayoutRect(room.X, room.Y + metrics.Gap + pictureHeight, room.Width, metrics.RowHeight);
        if (tab.PagesInUse is { IsOrganising: true } pages)
        {
            // While the pages are organised the words under a page are a box that is ticked when the page tools change that
            // page. Pressing it adds the page or takes it out, so several pages are chosen without holding a key.
            var chosen = Array.BinarySearch(pages.SelectedPages, row.Page) >= 0;
            items.Add(new(ChromeItemKind.Button, pages.TogglePageCommand, index, row.Text, caption, true, chosen) { Parameter = row.Target, Icon = chosen ? ChromeIcon.CheckOn : ChromeIcon.CheckOff });
            return height;
        }

        items.Add(new(ChromeItemKind.Button, pick, index, row.Text, caption, true, reading) { Parameter = row.Target, Icon = reading ? ChromeIcon.Done : ChromeIcon.None });
        return height;
    }
}

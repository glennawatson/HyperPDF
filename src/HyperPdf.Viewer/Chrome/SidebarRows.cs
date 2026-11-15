// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lists what the side panel shows for each of its tabs: pages, bookmarks, notes, attached files and layers.</summary>
internal static class SidebarRows
{
    /// <summary>The space that sets a nested row in from its parent.</summary>
    private const string Indent = "    ";

    /// <summary>The mark before a row whose children are hidden.</summary>
    private const string ClosedMark = "+ ";

    /// <summary>The mark before a row whose children are shown.</summary>
    private const string OpenMark = "– ";

    /// <summary>The space before a row with no children, as wide as a mark.</summary>
    private const string NoMark = "   ";

    /// <summary>The names of the tabs of the side panel, in English.</summary>
    private static readonly Dictionary<SidebarMode, string> Names = new()
    {
        [SidebarMode.Thumbnails] = "Pages",
        [SidebarMode.Outline] = "Bookmarks",
        [SidebarMode.Search] = "Found",
        [SidebarMode.Annotations] = "Notes",
        [SidebarMode.Attachments] = "Files",
        [SidebarMode.Layers] = "Layers",
        [SidebarMode.Parts] = "Parts",
        [SidebarMode.Media] = "Sound and film",
        [SidebarMode.Tags] = "Tags",
    };

    /// <summary>What each tab of the side panel says when it has nothing to list, in English.</summary>
    private static readonly Dictionary<SidebarMode, string> EmptyTexts = new()
    {
        [SidebarMode.Thumbnails] = "This document has no pages.",
        [SidebarMode.Outline] = "This document has no bookmarks.",
        [SidebarMode.Search] = "Nothing is found yet. Press Control and F to search.",
        [SidebarMode.Annotations] = "This document has no notes or marks.",
        [SidebarMode.Attachments] = "This document has no attached files.",
        [SidebarMode.Layers] = "This document has no layers.",
        [SidebarMode.Parts] = "This document names no parts.",
        [SidebarMode.Media] = "This document has no sound or film.",
        [SidebarMode.Tags] = "This document has no accessibility tags.",
    };

    /// <summary>Gets the tabs of the side panel, in the order they are shown.</summary>
    internal static ReadOnlySpan<SidebarMode> Modes =>
    [
        SidebarMode.Thumbnails, SidebarMode.Outline, SidebarMode.Search, SidebarMode.Annotations, SidebarMode.Attachments, SidebarMode.Layers, SidebarMode.Parts,
        SidebarMode.Media, SidebarMode.Tags,
    ];

    /// <summary>Gets the name of a tab of the side panel.</summary>
    /// <param name="mode">The tab.</param>
    /// <returns>The name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string NameOf(SidebarMode mode) => UiText.Get(Names.GetValueOrDefault(mode, string.Empty));

    /// <summary>Says what a tab of the side panel shows when it has nothing to list.</summary>
    /// <param name="mode">The tab.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string EmptyText(SidebarMode mode) => UiText.Get(EmptyTexts.GetValueOrDefault(mode, string.Empty));

    /// <summary>Lists the rows of the tab the side panel is on.</summary>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="tab">The document tab.</param>
    internal static void Fill(List<SidebarRow> rows, DocumentTabViewModel tab)
    {
        rows.Clear();
        switch (tab.SidebarMode)
        {
            case SidebarMode.Thumbnails:
                {
                    foreach (var page in tab.Thumbnails)
                    {
                        rows.Add(new(page.Caption, page, page.PageIndex == tab.CurrentPageIndex, true, page.PageIndex));
                    }

                    break;
                }

            case SidebarMode.Outline:
                {
                    AddBookmarks(rows, tab, tab.Outline, string.Empty);
                    break;
                }

            case SidebarMode.Annotations:
                {
                    _ = tab.Annotations.ShowItems();
                    foreach (var note in tab.Annotations.VisibleItems)
                    {
                        rows.Add(new($"{note.Heading}: {note.Summary}", note, ReferenceEquals(note.Annotation, tab.Annotations.Selected), true, -1));
                    }

                    break;
                }

            case SidebarMode.Attachments:
                {
                    foreach (var file in tab.Attachments.Items)
                    {
                        rows.Add(new($"{file.Name} ({AttachmentsViewModel.FormatSize(file.Size)})", file, ReferenceEquals(file, tab.Attachments.Selected), true, -1));
                    }

                    break;
                }

            default:
                {
                    FillFurther(rows, tab);
                    break;
                }
        }
    }

    /// <summary>Lists the rows of the tabs that show what was found, the layers, the parts, the sounds and films, and the tags.</summary>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="tab">The document tab.</param>
    private static void FillFurther(List<SidebarRow> rows, DocumentTabViewModel tab)
    {
        switch (tab.SidebarMode)
        {
            case SidebarMode.Search:
                {
                    foreach (var found in tab.Search.Results)
                    {
                        rows.Add(new(UiText.Format("Page {0}: {1}", found.PageNumber, found.Context), found, ReferenceEquals(found, tab.Search.SelectedResult), true, -1));
                    }

                    break;
                }

            case SidebarMode.Parts:
                {
                    AddParts(rows, tab, tab.Parts.Parts, string.Empty);
                    foreach (var thread in tab.Parts.Threads)
                    {
                        rows.Add(new(thread.SpokenName, thread, ReferenceEquals(thread, tab.Parts.SelectedThread), true, -1));
                    }

                    break;
                }

            case SidebarMode.Media:
                {
                    foreach (var media in tab.Media.Items)
                    {
                        rows.Add(new(UiText.Format("{0}, page {1}", media.Name, media.PageIndex + 1), media, ReferenceEquals(media, tab.Media.Selected), true, -1));
                    }

                    break;
                }

            case SidebarMode.Tags:
                {
                    foreach (var tag in tab.Tags.Items)
                    {
                        rows.Add(new(tag.Row, tag, ReferenceEquals(tag, tab.Tags.SelectedItem), true, -1));
                    }

                    break;
                }

            default:
                {
                    AddLayers(rows, tab.Layers.Items, string.Empty);
                    break;
                }
        }
    }

    /// <summary>Lists the parts of a document in order, each followed by the parts inside it.</summary>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="parts">The parts of one level.</param>
    /// <param name="indent">The space that sets the level in.</param>
    private static void AddParts(List<SidebarRow> rows, DocumentTabViewModel tab, IReadOnlyList<PartItemViewModel> parts, string indent)
    {
        foreach (var part in parts)
        {
            rows.Add(new(part.PagesText.Length > 0 ? $"{indent}{part.Name} ({part.PagesText})" : $"{indent}{part.Name}", part, ReferenceEquals(part, tab.Parts.SelectedPart), true, -1));
            AddParts(rows, tab, part.Children, $"{indent}{Indent}");
        }
    }

    /// <summary>Lists bookmarks in order, each open one followed by its children.</summary>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="tab">The document tab.</param>
    /// <param name="bookmarks">The bookmarks of one level.</param>
    /// <param name="indent">The space that sets the level in.</param>
    private static void AddBookmarks(List<SidebarRow> rows, DocumentTabViewModel tab, IReadOnlyList<OutlineItemViewModel> bookmarks, string indent)
    {
        foreach (var bookmark in bookmarks)
        {
            var mark = NoMark;
            if (bookmark.Children.Count > 0)
            {
                mark = bookmark.IsExpanded ? OpenMark : ClosedMark;
            }

            rows.Add(new($"{indent}{mark}{bookmark.Title}", bookmark, ReferenceEquals(bookmark, tab.SelectedOutlineItem), true, -1));
            if (bookmark.IsExpanded)
            {
                AddBookmarks(rows, tab, bookmark.Children, $"{indent}{Indent}");
            }
        }
    }

    /// <summary>Lists layers in order with whether each is shown, and the layers under each.</summary>
    /// <param name="rows">Receives the rows.</param>
    /// <param name="layers">The layers of one level.</param>
    /// <param name="indent">The space that sets the level in.</param>
    private static void AddLayers(List<SidebarRow> rows, IReadOnlyList<LayerItemViewModel> layers, string indent)
    {
        foreach (var layer in layers)
        {
            var state = layer.IsVisible ? UiText.Get("Shown") : UiText.Get("Hidden");
            rows.Add(new(layer.IsLabel ? $"{indent}{layer.Name}" : $"{indent}{layer.Name}: {state}", layer, layer.IsVisible && !layer.IsLabel, layer.CanToggle, -1));
            AddLayers(rows, layer.Children, $"{indent}{Indent}");
        }
    }
}

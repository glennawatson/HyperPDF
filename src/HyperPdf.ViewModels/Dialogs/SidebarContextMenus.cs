// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Sidebar;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>Builds the menu shown by a right-click on a row of the side panel: a page, a bookmark, a comment, an attached file, a film or sound, a layer or a tag.</summary>
public static class SidebarContextMenus
{
    /// <summary>The words of the entry that moves a bookmark or a tag up.</summary>
    private const string MoveUp = "Move _Up";

    /// <summary>The words of the entry that moves a bookmark or a tag down.</summary>
    private const string MoveDown = "Move _Down";

    /// <summary>The words of the entry that moves a bookmark or a tag under the one above it.</summary>
    private const string MoveIn = "Move _In";

    /// <summary>The words of the entry that moves a bookmark or a tag out a level.</summary>
    private const string MoveOut = "Move _Out";

    /// <summary>The words of the entry that saves an attached file or a film.</summary>
    private const string SaveCopy = "_Save a Copy…";

    /// <summary>The name of the icon of an entry that goes to a place.</summary>
    private const string Goes = "Follow";

    /// <summary>The name of the icon of an entry that removes something.</summary>
    private const string Removes = "Close";

    /// <summary>
    /// Selects the row under the pointer and lists what can be done to it. The row is selected first because the tools
    /// act on the selected row, so the menu acts on the row it was opened on.
    /// </summary>
    /// <param name="tab">The tab the side panel belongs to.</param>
    /// <param name="row">What the row stands for.</param>
    /// <returns>The menu items, or <see langword="null"/> when the row has no menu.</returns>
    public static List<MenuEntry>? ForRow(DocumentTabViewModel tab, object? row)
    {
        ArgumentNullException.ThrowIfNull(tab);
        return row switch
        {
            ThumbnailItemViewModel page => ForThumbnail(tab, page),
            OutlineItemViewModel bookmark => ForBookmark(tab, bookmark),
            AnnotationItemViewModel comment => ForComment(tab.Annotations, comment),
            DocumentAttachment file => ForAttachment(tab.Attachments, file),
            DocumentMedia media => ForMedia(tab.Media, media),
            LayerItemViewModel layer => ForLayer(tab.Layers, layer),
            TagItemViewModel tag => ForTag(tab.Tags, tag),
            _ => null,
        };
    }

    /// <summary>Lists what can be done to the selected pages. A page that is not selected is selected alone first.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForThumbnail(DocumentTabViewModel tab, ThumbnailItemViewModel page)
    {
        if (Array.BinarySearch(tab.Pages.SelectedPages, page.PageIndex) < 0)
        {
            tab.Pages.SelectPages([page.PageIndex]);
        }

        return PageContextMenus.ForPage(tab, page);
    }

    /// <summary>Lists what can be done to a bookmark.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="bookmark">The bookmark under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForBookmark(DocumentTabViewModel tab, OutlineItemViewModel bookmark)
    {
        tab.SelectedOutlineItem = bookmark;
        var edit = tab.OutlineEditing;
        return
        [
            MenuEntry.Item(UiText.Get("_Go to This Bookmark"), null, tab.FollowLinkCommand, bookmark.Target, true).WithIcon(Goes),
            MenuEntry.Separator(),
            Item("_Add a Bookmark Here…", edit.AddCommand, "Add"),
            Item("_Rename the Bookmark…", edit.RenameCommand, "Text"),
            MenuEntry.Separator(),
            Item(MoveUp, edit.MoveUpCommand, "PageUp"),
            Item(MoveDown, edit.MoveDownCommand, "PageDown"),
            Item(MoveIn, edit.IndentCommand, "Indent"),
            Item(MoveOut, edit.OutdentCommand, "Outdent"),
            MenuEntry.Separator(),
            Item("Re_move the Bookmark", edit.RemoveCommand, Removes),
        ];
    }

    /// <summary>Lists what can be done to a comment: going to it, then what a right-click on it on the page offers.</summary>
    /// <param name="annotations">The comments of the tab.</param>
    /// <param name="comment">The comment under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForComment(AnnotationsViewModel annotations, AnnotationItemViewModel comment)
    {
        annotations.Select(comment.Annotation);
        var items = ContextMenus.ForAnnotation(annotations, comment.Annotation);
        items.Insert(1, MenuEntry.Item(UiText.Get("_Go to This Comment"), null, annotations.GoToCommand, comment, true).WithIcon(Goes));
        return items;
    }

    /// <summary>Lists what can be done to a file attached to the document.</summary>
    /// <param name="attachments">The attached files of the tab.</param>
    /// <param name="file">The file under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForAttachment(AttachmentsViewModel attachments, DocumentAttachment file)
    {
        attachments.Selected = file;
        return
        [
            Item("_Open the Attached File", attachments.OpenSelectedCommand, "Open"),
            Item(SaveCopy, attachments.SaveCommand, "Save"),
        ];
    }

    /// <summary>Lists what can be done to a film or a sound in the document.</summary>
    /// <param name="films">The films and sounds of the tab.</param>
    /// <param name="media">The one under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForMedia(DocumentMediaViewModel films, DocumentMedia media)
    {
        films.Selected = media;
        List<MenuEntry> items = [];
        if (films.Player.IsAvailable)
        {
            items.Add(Item("_Play Here", films.Player.PlayCommand, "Play"));
        }

        items.Add(Item("_Open in Another App", films.OpenSelectedCommand, "Open"));
        items.Add(Item(SaveCopy, films.SaveCommand, "Save"));
        return items;
    }

    /// <summary>Lists what can be done to a layer.</summary>
    /// <param name="layers">The layers of the tab.</param>
    /// <param name="layer">The layer under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForLayer(LayersViewModel layers, LayerItemViewModel layer) =>
    [
        MenuEntry.Item(layer.IsVisible ? UiText.Get("_Hide This Layer") : UiText.Get("_Show This Layer"), null, layers.ToggleLayerCommand, layer, layer.CanToggle)
            .WithIcon(layer.IsVisible ? "CheckOn" : "CheckOff"),
        MenuEntry.Item(UiText.Get("Show _Only This Layer"), null, layers.ShowOnlyCommand, layer, layer.CanToggle).WithIcon("Layers"),
        MenuEntry.Separator(),
        Item("Show Every _Layer as the Document Sets It", layers.ResetCommand, "Undo"),
    ];

    /// <summary>Lists what can be done to an accessibility tag.</summary>
    /// <param name="tags">The tags of the tab.</param>
    /// <param name="tag">The tag under the pointer.</param>
    /// <returns>The menu items.</returns>
    private static List<MenuEntry> ForTag(TagsViewModel tags, TagItemViewModel tag)
    {
        tags.SelectedItem = tag;
        return
        [
            Item("_Edit This Tag…", tags.EditCommand, "Text"),
            MenuEntry.Separator(),
            Item(MoveUp, tags.MoveUpCommand, "PageUp"),
            Item(MoveDown, tags.MoveDownCommand, "PageDown"),
            Item(MoveIn, tags.NestCommand, "Indent"),
            Item(MoveOut, tags.UnnestCommand, "Outdent"),
            MenuEntry.Separator(),
            Item("Re_move This Tag", tags.RemoveCommand, Removes),
        ];
    }

    /// <summary>Makes an entry that runs a command with no value, greyed out while the command cannot run.</summary>
    /// <param name="header">The entry's words in English, which are looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <param name="icon">The name of the entry's icon.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string header, ICommand command, string icon) =>
        MenuEntry.Item(UiText.Get(header), null, command, null, command.CanExecute(null)).WithIcon(icon);
}

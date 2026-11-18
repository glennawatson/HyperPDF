// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Finding;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// Joins the window's controls to the tab shown, for what a control's own command cannot say: what a row of the side panel does,
/// which depends on what the row stands for, and the changes of the tab that open the search bar or lay the bars out again.
/// </summary>
internal static class ChromeBindings
{
    /// <summary>
    /// Joins the controls to the tab shown. The search bar opens whenever the search is opened, by its button, Ctrl+F or the menu, and
    /// not only by its button. The bars are laid out again as the media player changes what its bar shows.
    /// </summary>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="chrome">The window's controls, which ask where to save an attached file.</param>
    /// <returns>What ends the joining, which is done when another tab is shown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IDisposable Tab(ChromeActions actions, DocumentTabViewModel tab, ViewerChrome chrome) => new MultipleDisposable(
        tab.Search.WhenChanged(static search => search.IsOpen).Where(static open => open).SubscribeSafe(_ => chrome.ShowFindBar(), ErrorTrace.Write),
        tab.Media.WhenChanged(static media => media.HasPlayer)
            .SwitchMap(made => made ? PlayerChanges(tab.Media.Player) : Signal.Never<RxVoid>())
            .SubscribeSafe(_ => chrome.InvalidateBars(), ErrorTrace.Write),
        Panel(actions.PickSidebarRow, tab, chrome));

    /// <summary>Follows what the media bar shows. The picture is left out, because a new one arrives for each frame of a film.</summary>
    /// <param name="player">The player.</param>
    /// <returns>A signal each time something the bar shows changes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> PlayerChanges(MediaPlayerViewModel player) => Signal.Merge(
        player.WhenChanged(static p => p.State).AsSignal(),
        player.WhenChanged(static p => p.Clock).AsSignal(),
        player.WhenChanged(static p => p.Caption).AsSignal(),
        player.WhenChanged(static p => p.CanPlay).AsSignal(),
        player.WhenChanged(static p => p.CanPause).AsSignal(),
        player.WhenChanged(static p => p.CanMove).AsSignal(),
        player.WhenChanged(static p => p.HasPicture).AsSignal());

    /// <summary>Joins the rows of the side panel to the tab: what a row of each list does.</summary>
    /// <param name="rows">What each pressed row stands for: a page, a bookmark, a note, an attached file or a layer. Each kind is its own stream.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>What ends the joining.</returns>
    private static MultipleDisposable Panel(IObservable<object> rows, DocumentTabViewModel tab, ViewerChrome chrome) =>
    [
        rows.OfType<ThumbnailItemViewModel>().SubscribeSafe(
            page =>
            {
                tab.GoToPage(page.PageIndex);

                // The page picked is the page the page tools change.
                tab.Pages.SelectPages([page.PageIndex]);
            },
            ErrorTrace.Write),
        rows.OfType<OutlineItemViewModel>().SubscribeSafe(bookmark => PickBookmark(bookmark, tab), ErrorTrace.Write),
        rows.OfType<AnnotationItemViewModel>().InvokeCommand(tab.Annotations.GoToCommand),
        rows.OfType<LayerItemViewModel>().SubscribeSafe(static layer => layer.IsVisible = !layer.IsVisible, ErrorTrace.Write),
        rows.OfType<SearchResultItemViewModel>().SubscribeSafe(found => tab.Search.SelectedResult = found, ErrorTrace.Write),
        rows.OfType<PartItemViewModel>().SubscribeSafe(part => tab.Parts.SelectedPart = part, ErrorTrace.Write),
        rows.OfType<ThreadItemViewModel>().SubscribeSafe(thread => tab.Parts.SelectedThread = thread, ErrorTrace.Write),
        rows.OfType<TagItemViewModel>().SubscribeSafe(tag => tab.Tags.SelectedItem = tag, ErrorTrace.Write),

        // A film or a sound plays in the bar under the toolbar when the desktop can play it, and goes to the system player when it cannot.
        // The player is made when it is first needed, so its command is read when a row is pressed and not when the tab is joined.
        rows.OfType<DocumentMedia>()
            .Where(_ => tab.Media.Player.IsAvailable)
            .Do(media => tab.Media.Selected = media)
            .SubscribeSafe(_ => ChromeActions.Run(tab.Media.Player.PlayCommand), ErrorTrace.Write),
        rows.OfType<DocumentMedia>().Where(_ => !tab.Media.Player.IsAvailable).Do(media => tab.Media.Selected = media).AsSignal().InvokeCommand(tab.Media.OpenSelectedCommand),
        rows.OfType<DocumentAttachment>()
            .SwitchMap(file => chrome.PickFile(FolderOf(tab), Path.GetFileName(file.Name))
                .KeepNotNull()
                .SwitchMap(path => tab.Attachments.SaveTo(file, path)))
            .SubscribeSafe(static _ => { }, ErrorTrace.Write),

        // Opening or closing a bookmark changes the rows without changing a view model property the panel follows.
        rows.SubscribeSafe(_ => chrome.InvalidateBars(), ErrorTrace.Write),
    ];

    /// <summary>Goes to a bookmark of the side panel, and shows or hides the bookmarks under it.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <param name="tab">The tab shown.</param>
    private static void PickBookmark(OutlineItemViewModel bookmark, DocumentTabViewModel tab)
    {
        if (bookmark.Children.Count > 0)
        {
            bookmark.IsExpanded = !bookmark.IsExpanded;
        }

        if (ReferenceEquals(tab.SelectedOutlineItem, bookmark))
        {
            // Choosing the chosen bookmark again does not change the selection, so the tab is told to go there directly.
            tab.Navigate(bookmark.Target);
            return;
        }

        tab.SelectedOutlineItem = bookmark;
    }

    /// <summary>Picks the folder a save dialog starts in: the folder of the tab's document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The folder.</returns>
    private static string FolderOf(DocumentTabViewModel tab) =>
        Path.GetDirectoryName(Path.GetFullPath(tab.FilePath)) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}

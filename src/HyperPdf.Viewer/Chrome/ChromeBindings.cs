// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels;
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
/// Joins the presses of the window's controls to the view models. Each kind of control is one stream of presses, and each stream runs
/// a view model command or sets a view model property.
/// </summary>
internal static class ChromeBindings
{
    /// <summary>The colours a new mark can have, in the order the colour button steps through them.</summary>
    private static readonly uint[] MarkColors =
    [
        AnnotationColors.Sand, AnnotationColors.Sage, AnnotationColors.Slate, AnnotationColors.Clay, AnnotationColors.Heather, AnnotationColors.Peach,
        AnnotationColors.Stone, AnnotationColors.Ink,
    ];

    /// <summary>Joins the controls that act on the whole window: the tabs and the open button.</summary>
    /// <param name="presses">The presses of the window's controls.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>What ends the joining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IDisposable Window(IObservable<ChromePress> presses, MainViewModel main) => new MultipleDisposable(
        presses.WhereCommand(ChromeCommand.SelectTab).SelectWith(main, TabAt).KeepNotNull().SubscribeSafe(tab => main.SelectedTab = tab, ErrorTrace.Write),
        presses.WhereCommand(ChromeCommand.CloseTab).SelectWith(main, TabAt).KeepNotNull().InvokeCommand(main.CloseTabCommand),
        presses.InvokeCommandOn(ChromeCommand.Open, main.OpenCommand),
        presses.InvokeCommandOn(ChromeCommand.DismissStatus, main.DismissStatusCommand),
        presses.InvokeCommandOn(ChromeCommand.NextTab, main.NextTabCommand),
        presses.InvokeCommandOn(ChromeCommand.PreviousTab, main.PreviousTabCommand));

    /// <summary>Joins the controls that act on the tab shown: the toolbar, the marking bar and the side panel.</summary>
    /// <param name="presses">The presses of the window's controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="chrome">The window's controls, which ask where to save an attached file and know what the side panel lists.</param>
    /// <returns>What ends the joining, which is done when another tab is shown.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IDisposable Tab(IObservable<ChromePress> presses, DocumentTabViewModel tab, ViewerChrome chrome) => new MultipleDisposable(
        presses.InvokeCommandOn(ChromeCommand.Save, tab.SaveCommand),
        presses.InvokeCommandOn(ChromeCommand.PreviousPage, tab.PreviousPageCommand),
        presses.InvokeCommandOn(ChromeCommand.NextPage, tab.NextPageCommand),
        presses.InvokeCommandOn(ChromeCommand.ZoomOut, tab.ZoomOutCommand),
        presses.InvokeCommandOn(ChromeCommand.ZoomIn, tab.ZoomInCommand),
        presses.InvokeCommandOn(ChromeCommand.FitWidth, tab.FitWidthCommand),
        presses.InvokeCommandOn(ChromeCommand.FitPage, tab.FitPageCommand),
        presses.InvokeCommandOn(ChromeCommand.Sidebar, tab.ToggleSidebarCommand),
        Marks(presses, tab.Annotations),
        presses.WhereCommand(ChromeCommand.GoToPage).SubscribeSafe(_ => AskForPage(tab, chrome), ErrorTrace.Write),
        Media(presses, tab, chrome),
        Panel(presses, tab, chrome));

    /// <summary>
    /// Joins the media bar to the player, and makes the bars lay out again as the player changes what the bar shows. The player is made
    /// when it is first needed, so each button reaches it when it is pressed.
    /// </summary>
    /// <param name="presses">The presses of the window's controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>What ends the joining.</returns>
    private static MultipleDisposable Media(IObservable<ChromePress> presses, DocumentTabViewModel tab, ViewerChrome chrome) =>
    [
        presses.WhereCommand(ChromeCommand.MediaPlay).SubscribeSafe(_ => Run(tab.Media.Player.PlayCommand), ErrorTrace.Write),
        presses.WhereCommand(ChromeCommand.MediaPause).SubscribeSafe(_ => Run(tab.Media.Player.PauseCommand), ErrorTrace.Write),
        presses.WhereCommand(ChromeCommand.MediaStop).SubscribeSafe(_ => Run(tab.Media.Player.StopCommand), ErrorTrace.Write),
        presses.WhereCommand(ChromeCommand.MediaBack).SubscribeSafe(_ => Run(tab.Media.Player.SkipBackCommand), ErrorTrace.Write),
        presses.WhereCommand(ChromeCommand.MediaForward).SubscribeSafe(_ => Run(tab.Media.Player.SkipForwardCommand), ErrorTrace.Write),
        presses.WhereCommand(ChromeCommand.MediaPicture).SubscribeSafe(_ => Run(tab.Media.ShowPlayerCommand), ErrorTrace.Write),
        tab.Media.WhenChanged(static media => media.HasPlayer)
            .SwitchMap(made => made ? PlayerChanges(tab.Media.Player) : Signal.Never<RxVoid>())
            .SubscribeSafe(_ => chrome.InvalidateBars(), ErrorTrace.Write),
    ];

    /// <summary>Asks which page to go to, and goes there.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <param name="chrome">The window's controls, which ask.</param>
    private static void AskForPage(DocumentTabViewModel tab, ViewerChrome chrome) =>
        _ = chrome.Prompt(new(UiText.Get("Go to a page"), UiText.Get("Page number"), tab.PageEntry, UiText.Get("Go"), false)).SubscribeSafe(
            answer =>
            {
                if (answer is null)
                {
                    return;
                }

                tab.PageEntry = answer;
                Run(tab.GoToPageEntryCommand);
            },
            ErrorTrace.Write);

    /// <summary>Runs a command when it can run.</summary>
    /// <param name="command">The command.</param>
    private static void Run(ICommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

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

    /// <summary>Joins the marking bar to the marking tools.</summary>
    /// <param name="presses">The presses of the window's controls.</param>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <returns>What ends the joining.</returns>
    private static MultipleDisposable Marks(IObservable<ChromePress> presses, AnnotationsViewModel marks)
    {
        // The button reads whether the bar is shown once for each press. Both commands follow that one reading, so the first to run
        // cannot change what the second one sees.
        var shown = presses.WhereCommand(ChromeCommand.Annotate).SelectWith(marks, static (tools, _) => tools.IsAnnotating).Publish();
        MultipleDisposable joined =
        [
            shown.Where(static on => on).AsSignal().InvokeCommand(marks.DoneCommand),
            shown.Where(static on => !on).AsSignal().InvokeCommand(marks.StartCommand),
            presses.WhereCommand(ChromeCommand.MarkTool).Select(static press => (AnnotationTool)press.Index).InvokeCommand(marks.SetToolCommand),
            presses.WhereCommand(ChromeCommand.MarkColor).SelectWith(marks, NextColor).InvokeCommand(marks.SetColorCommand),
            presses.WhereCommand(ChromeCommand.MarkWidth).SelectWith(marks, NextWidth).InvokeCommand(marks.SetLineWidthCommand),
            presses.InvokeCommandOn(ChromeCommand.Undo, marks.UndoCommand),
            presses.InvokeCommandOn(ChromeCommand.Redo, marks.RedoCommand),
            presses.InvokeCommandOn(ChromeCommand.DeleteMark, marks.DeleteCommand),
        ];

        // Both commands are joined before the presses start to flow, so each press reaches both with the same reading.
        joined.Add(shown.Connect());
        return joined;
    }

    /// <summary>Joins the side panel to the tab: its five lists, and what a row of each list does.</summary>
    /// <param name="presses">The presses of the window's controls.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>What ends the joining.</returns>
    private static MultipleDisposable Panel(IObservable<ChromePress> presses, DocumentTabViewModel tab, ViewerChrome chrome)
    {
        // A row stands for a page, a bookmark, a note, an attached file or a layer. Each kind is its own stream.
        var rows = presses.WhereCommand(ChromeCommand.PickRow).SelectWith(chrome, static (owner, press) => owner.SidebarTarget(in press)).KeepNotNull();
        return
        [
            presses.WhereCommand(ChromeCommand.SidebarMode).SubscribeSafe(
                press =>
                {
                    tab.SidebarMode = (SidebarMode)press.Index;
                    chrome.ResetSidebarScroll();
                },
                ErrorTrace.Write),
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
            rows.OfType<DocumentMedia>().Where(_ => tab.Media.Player.IsAvailable).Do(media => tab.Media.Selected = media).SubscribeSafe(_ => Run(tab.Media.Player.PlayCommand), ErrorTrace.Write),
            rows.OfType<DocumentMedia>().Where(_ => !tab.Media.Player.IsAvailable).Do(media => tab.Media.Selected = media).AsSignal().InvokeCommand(tab.Media.OpenSelectedCommand),
            rows.OfType<DocumentAttachment>()
                .SwitchMap(file => chrome.PickFile(FolderOf(tab), Path.GetFileName(file.Name))
                    .KeepNotNull()
                    .SwitchMap(path => Signal.FromAsync(() => tab.Attachments.SaveToAsync(file, path))))
                .SubscribeSafe(static _ => { }, ErrorTrace.Write),

            // Opening or closing a bookmark changes the rows without changing a view model property the panel follows.
            presses.WhereCommand(ChromeCommand.PickRow).SubscribeSafe(_ => chrome.InvalidateBars(), ErrorTrace.Write),
        ];
    }

    /// <summary>Finds the tab a tab strip control stands for.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="press">The press.</param>
    /// <returns>The tab, or <see langword="null"/> when it has closed since the controls were laid out.</returns>
    private static DocumentTabViewModel? TabAt(MainViewModel main, ChromePress press) =>
        (uint)press.Index < (uint)main.Tabs.Count ? main.Tabs[press.Index] : null;

    /// <summary>Picks the colour after the one in use, by its name, which also recolours the mark that is picked.</summary>
    /// <param name="marks">The marking tools.</param>
    /// <param name="press">The press.</param>
    /// <returns>The name of the next colour.</returns>
    private static string NextColor(AnnotationsViewModel marks, ChromePress press)
    {
        var colors = MarkColors.AsSpan();
        return AnnotationNames.GetColor(colors[(colors.IndexOf(marks.Color) + 1) % colors.Length]);
    }

    /// <summary>Picks the line width after the one in use, by its name.</summary>
    /// <param name="marks">The marking tools.</param>
    /// <param name="press">The press.</param>
    /// <returns>The name of the next line width.</returns>
    private static string NextWidth(AnnotationsViewModel marks, ChromePress press)
    {
        var widths = AnnotationsViewModel.LineWidths;
        var at = 0;
        while (at < widths.Count && widths[at].Value < marks.LineWidth)
        {
            at++;
        }

        return widths[(at + 1) % widths.Count].NameKey;
    }

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

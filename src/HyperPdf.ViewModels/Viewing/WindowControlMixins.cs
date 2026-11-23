// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Signing;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// The signals a window follows to keep its controls in step with the view models: one value each time something the tab strip, the
/// toolbar, the marking bar, the side panel or the status bar shows has changed. A head subscribes once and lays its controls out again.
/// </summary>
public static class WindowControlMixins
{
    /// <summary>Names the signal of a tab's controls among the signals the tab keeps.</summary>
    private static readonly object ControlChangesKey = new();

    /// <summary>Adds the signal of a tab's marking tools.</summary>
    /// <param name="marks">The marking tools.</param>
    extension(AnnotationsViewModel marks)
    {
        /// <summary>Gets a signal for each change to what the marking bar shows.</summary>
        public IObservable<RxVoid> ControlChanges => Signal.Merge(
            marks.WhenChanged(static a => a.IsAnnotating).AsSignal(),
            marks.WhenChanged(static a => a.Tool).AsSignal(),
            marks.WhenChanged(static a => a.Color).AsSignal(),
            marks.WhenChanged(static a => a.LineWidth).AsSignal(),
            marks.WhenChanged(static a => a.CanUndo).AsSignal(),
            marks.WhenChanged(static a => a.CanRedo).AsSignal(),
            marks.WhenChanged(static a => a.Selected).AsSignal());
    }

    /// <summary>Adds the signal of a document tab.</summary>
    /// <param name="tab">The tab.</param>
    extension(DocumentTabViewModel tab)
    {
        /// <summary>
        /// Gets a signal for each change to what the toolbar, the marking bar and the side panel show of the tab, starting with one
        /// for the tab as it is now. The tab keeps the signal behind it connected, so showing the tab again follows that one signal
        /// and does not subscribe to every property again.
        /// </summary>
        public IObservable<RxVoid> ControlChanges =>
            tab.KeepSignal(ControlChangesKey, static shown => shown.NewControlChanges.Publish()).StartWith(RxVoid.Default);

        /// <summary>Gets a new signal for each change to what the toolbar, the marking bar and the side panel show of the tab.</summary>
        private IObservable<RxVoid> NewControlChanges => Signal.Merge(
            tab.WhenChanged(static t => t.CurrentPageIndex).AsSignal(),
            tab.WhenChanged(static t => t.PageCount).AsSignal(),
            tab.WhenChanged(static t => t.Zoom).AsSignal(),
            tab.WhenChanged(static t => t.IsLoaded).AsSignal(),
            tab.WhenChanged(static t => t.ErrorMessage).AsSignal(),
            tab.WhenChanged(static t => t.SidebarVisible).AsSignal(),
            tab.WhenChanged(static t => t.Thumbnails).AsSignal(),
            tab.WhenChanged(static t => t.Outline).AsSignal(),
            tab.WhenChanged(static t => t.SelectedOutlineItem).AsSignal(),
            tab.WhenChanged(static t => t.CanGoBack).AsSignal(),
            tab.WhenChanged(static t => t.CanGoForward).AsSignal(),
            tab.PanelListChanges,
            tab.PartChanges<AnnotationsViewModel>(static marks => marks.ControlChanges),
            tab.ToolChanges);

        /// <summary>
        /// Gets a signal for each change to what the notice and tool bars show: the notices about the document and the state of read
        /// aloud, text recognition, focus mode, slow scrolling, measuring, filling and signing, and editing the page's own content.
        /// </summary>
        private IObservable<RxVoid> ToolChanges => Signal.Merge(
            tab.WhenChanged(static t => t.Notice).AsSignal(),
            tab.WhenChanged(static t => t.HasPendingReload).AsSignal(),
            tab.WhenChanged(static t => t.SignatureCount).AsSignal(),
            tab.Notices.GetChangedObservable().AsSignal(),
            tab.PartChanges<SignaturesViewModel>(static signatures => Signal.Merge(
                signatures.WhenChanged(static s => s.IsChecking).AsSignal(),
                signatures.WhenChanged(static s => s.StatusSummary).AsSignal(),
                ((IList)signatures.Signatures).CollectionChanges.AsSignal())),
            tab.PartChanges<ReadAloudViewModel>(static aloud => Signal.Merge(
                aloud.WhenChanged(static a => a.IsOpen).AsSignal(),
                aloud.WhenChanged(static a => a.IsPlaying).AsSignal(),
                aloud.WhenChanged(static a => a.StatusText).AsSignal(),
                aloud.WhenChanged(static a => a.IsDownloading).AsSignal(),
                aloud.WhenChanged(static a => a.DownloadProgress).AsSignal(),
                aloud.WhenChanged(static a => a.VoiceIndex).AsSignal(),
                aloud.WhenChanged(static a => a.SpeedIndex).AsSignal(),
                aloud.WhenChanged(static a => a.DimRest).AsSignal(),
                aloud.WhenChanged(static a => a.CanDownloadVoice).AsSignal())),
            tab.WhenChanged(static t => t.TextRecognitionInUse).SwitchMap(static text => text is null ? Signal.Return(RxVoid.Default) : text.GetChangedObservable().AsSignal()),
            tab.PartChanges<FocusModeViewModel>(static focus => focus.WhenChanged(static f => f.IsOn).AsSignal()),
            tab.PageTools.WhenChanged(static p => p.IsAutoScrolling).AsSignal(),
            tab.PageTools.WhenChanged(static p => p.AutoScrollSpeed).AsSignal(),
            tab.PartChanges<MeasureViewModel>(static measure => measure.GetChangedObservable().AsSignal()),
            tab.PartChanges<FillAndSignViewModel>(static filling => filling.GetChangedObservable().AsSignal()),
            tab.WhenChanged(static t => t.IsTextFormatShown).AsSignal(),
            tab.PartChanges<PageContentEditViewModel>(static edit => Signal.Merge(
                edit.WhenChanged(static e => e.IsEditing).AsSignal(),
                edit.WhenChanged(static e => e.Selected).AsSignal(),
                edit.WhenChanged(static e => e.TextEdit).AsSignal())),
            tab.PartChanges<PageManagementViewModel>(static pages => Signal.Merge(
                pages.WhenChanged(static p => p.CanUndo).AsSignal(),
                pages.WhenChanged(static p => p.CanRedo).AsSignal())));

        /// <summary>Gets a signal for each change to the list the side panel shows. The view model of a list is only made once the panel shows it.</summary>
        private IObservable<RxVoid> PanelListChanges =>
            tab.WhenChanged(static t => t.SidebarMode).SelectWith(tab, PanelList).SwitchMap(static list => list);
    }

    /// <summary>Adds the signal of the main view model.</summary>
    /// <param name="main">The main view model.</param>
    extension(MainViewModel main)
    {
        /// <summary>Gets a signal for each change to what the window's controls show: the tabs, the tab shown and the status message.</summary>
        public IObservable<RxVoid> ControlChanges => Signal.Merge(
            main.WhenChanged(static m => m.StatusMessage).AsSignal(),
            Signal.CreateWithState<RxVoid, IList>(main.Tabs, TabNameWatch.Start),
            main.WhenChanged(static m => m.SelectedTab).SwitchMap(static tab => tab is null ? Signal.Return(RxVoid.Default) : tab.ControlChanges));
    }

    /// <summary>Follows one list of the side panel.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="mode">The list the panel shows.</param>
    /// <returns>A signal now, and again each time the list changes.</returns>
    private static IObservable<RxVoid> PanelList(DocumentTabViewModel tab, SidebarMode mode) => mode switch
    {
        SidebarMode.Annotations => ((IList)tab.Annotations.VisibleItems).CollectionChanges.AsSignal(),
        SidebarMode.Attachments => ((IList)tab.Attachments.Items).CollectionChanges.AsSignal(),
        SidebarMode.Layers => ((IList)tab.Layers.Items).CollectionChanges.AsSignal(),
        SidebarMode.Search => Signal.Merge(
            ((IList)tab.Search.Results).CollectionChanges.AsSignal(),
            tab.Search.WhenChanged(static s => s.SelectedResult).AsSignal()),
        SidebarMode.Parts => Signal.Merge(
            ((IList)tab.Parts.Parts).CollectionChanges.AsSignal(),
            tab.Parts.WhenChanged(static p => p.SelectedPart).AsSignal()),
        SidebarMode.Media => Signal.Merge(
            ((IList)tab.Media.Items).CollectionChanges.AsSignal(),
            tab.Media.WhenChanged(static m => m.Selected).AsSignal()),
        SidebarMode.Tags => Signal.Merge(
            tab.Tags.WhenChanged(static t => t.Items).AsSignal(),
            tab.Tags.WhenChanged(static t => t.SelectedItem).AsSignal()),
        _ => Signal.Return(RxVoid.Default),
    };
}

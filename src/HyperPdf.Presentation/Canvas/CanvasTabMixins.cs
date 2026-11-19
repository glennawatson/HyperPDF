// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Presentation.Canvas;

/// <summary>The state of a tab the page canvas follows, as streams, so the canvas of any head reacts to the tab instead of being told.</summary>
public static class CanvasTabMixins
{
    /// <summary>Names the signal of the changes that move the marks, among the signals a tab keeps.</summary>
    private static readonly object MarkRedrawsKey = new();

    /// <summary>Names the signal of the changes that need the pages drawn again.</summary>
    private static readonly object CanvasRedrawsKey = new();

    /// <summary>Names the signal of the changes to how the pages are laid out.</summary>
    private static readonly object LayoutSettingsKey = new();

    /// <summary>Adds the changes of a tab the canvas follows.</summary>
    /// <param name="tab">The tab.</param>
    extension(DocumentTabViewModel tab)
    {
        /// <summary>
        /// Gets the changes that move the marks over the pages: search hits, the reading, the focus band and the layout. The tab
        /// keeps the signal connected, so showing the tab again follows it and does not subscribe to each property again.
        /// </summary>
        public IObservable<RxVoid> MarkRedraws => tab.KeepSignal(MarkRedrawsKey, static shown => shown.NewMarkRedraws.Publish());

        /// <summary>
        /// Gets the changes that only need the pages drawn again: tone, content, caret, tiles, picks and placing. The tab keeps
        /// the signal connected.
        /// </summary>
        public IObservable<RxVoid> CanvasRedraws => tab.KeepSignal(CanvasRedrawsKey, static shown => shown.NewCanvasRedraws.Publish());

        /// <summary>
        /// Gets the changes to how the pages are laid out: zoom, arrangement, rotation and page by page. The zoom counts only in
        /// Free mode. The tab keeps the signal connected.
        /// </summary>
        public IObservable<RxVoid> LayoutSettingsChanges => tab.KeepSignal(LayoutSettingsKey, static shown => shown.NewLayoutSettingsChanges.Publish());

        /// <summary>Gets the places the reader asks to go after the tab is shown.</summary>
        public IObservable<NavigationRequest> Destinations => tab.WhenChanged(static t => t.Destination).Skip(1).Where(static request => request.IsRequest);

        /// <summary>Gets the moments the document's pages were read again, after it loads, reloads or its pages change.</summary>
        public IObservable<RxVoid> PageReads => tab.WhenChanged(static t => t.LayoutVersion).Skip(1).AsSignal();

        /// <summary>Gets the moments a menu command used the text selection, which the canvas then clears.</summary>
        public IObservable<RxVoid> SelectionUses => Signal.Merge(
            tab.PartChanges<AnnotationsViewModel>(static marks => marks.MarkSelectionCommand.Where(static marked => marked).AsSignal()),
            tab.PartChanges<ReadAloudViewModel>(static reading => reading.ReadFromCharacterCommand.AsSignal()));

        /// <summary>Gets the tab each time caret mode is turned on.</summary>
        public IObservable<DocumentTabViewModel> CaretModeStarts => tab.WhenChanged(static t => t.IsCaretMode)
            .Skip(1)
            .Where(static on => on)
            .SelectWith(tab, static (shown, _) => shown);

        /// <summary>Gets the moments the page tool changes.</summary>
        public IObservable<RxVoid> PageToolChanges => tab.WhenChanged(static t => t.PageTools.PageTool).Skip(1).AsSignal();

        /// <summary>Gets whether the pages scroll by themselves, now and as it changes.</summary>
        public IObservable<bool> AutoScrolling => tab.WhenChanged(static t => t.PageTools.IsAutoScrolling);

        /// <summary>Gets a new signal of the changes that move the marks over the pages.</summary>
        private IObservable<RxVoid> NewMarkRedraws => Signal.Merge(
            tab.PartChanges<SearchViewModel>(static search => search.WhenChanged(static s => s.HighlightVersion).Skip(1).AsSignal()),
            tab.PartChanges<ReadAloudViewModel>(static reading => Signal.Merge(
                reading.WhenChanged(static r => r.SpokenBounds).Skip(1).AsSignal(),
                reading.WhenChanged(static r => r.MarksVersion).Skip(1).AsSignal())),
            tab.PartChanges<FocusModeViewModel>(static focus => focus.WhenChanged(static f => f.FocusBand).Skip(1).AsSignal()),
            tab.PartChanges<PageContentEditViewModel>(static edit => edit.WhenChanged(static e => e.MarksVersion).Skip(1).AsSignal()),
            tab.WhenChanged(static t => t.LayoutVersion).Skip(1).AsSignal(),
            tab.LayoutSettingsChanges);

        /// <summary>Gets a new signal of the changes that only need the pages drawn again.</summary>
        private IObservable<RxVoid> NewCanvasRedraws => Signal.Merge(
            tab.WhenChanged(static t => t.PageTone).Skip(1).AsSignal(),
            tab.WhenChanged(static t => t.ContentVersion).Skip(1).AsSignal(),
            tab.WhenChanged(static t => t.IsCaretMode).Skip(1).AsSignal(),
            tab.RenderHub.TilesArrived,
            tab.PartChanges<AnnotationsViewModel>(static marks => Signal.Merge(
                marks.WhenChanged(static annotations => annotations.Selected).Skip(1).AsSignal(),
                marks.WhenChanged(static annotations => annotations.RegionsVersion, static annotations => annotations.Tool, static (version, _) => version).Skip(1).AsSignal())),
            tab.PartChanges<FillAndSignViewModel>(static filling => filling.WhenChanged(static f => f.Placement).Skip(1).AsSignal()),
            tab.Forms.WhenChanged(static forms => forms.Focused).Skip(1).AsSignal());

        /// <summary>Gets a new signal of the changes to how the pages are laid out.</summary>
        private IObservable<RxVoid> NewLayoutSettingsChanges => tab.WhenChanged(
                static t => t.ZoomMode,
                static t => t.LayoutMode,
                static t => t.Rotation,
                static t => t.IsPageByPage,
                static t => t.Zoom,
                static t => t.RightToLeft,
                static t => t.ScrollMode,
                static (mode, layout, rotation, pageByPage, zoom, rightToLeft, scroll) =>
                    new CanvasLayoutKey(mode, layout, rotation, pageByPage, mode == ZoomMode.Free ? zoom : 0, rightToLeft, scroll))
            .DistinctUntilChanged()
            .Skip(1)
            .AsSignal();
    }

    /// <summary>What the canvas layout depends on.</summary>
    /// <param name="Mode">How the zoom is chosen.</param>
    /// <param name="Layout">The page arrangement.</param>
    /// <param name="Rotation">The page rotation.</param>
    /// <param name="PageByPage">Whether pages are shown one at a time.</param>
    /// <param name="Zoom">The zoom in Free mode, otherwise 0.</param>
    /// <param name="RightToLeft">Whether two page spreads read right to left.</param>
    /// <param name="Scroll">The direction pages are laid out in.</param>
    private readonly record struct CanvasLayoutKey(ZoomMode Mode, PageLayoutMode Layout, PageRotation Rotation, bool PageByPage, double Zoom, bool RightToLeft, ScrollMode Scroll);
}

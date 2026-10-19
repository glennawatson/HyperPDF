// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>The state of a tab the page canvas follows, as streams, so the canvas reacts to the tab instead of being told.</summary>
internal static class CanvasTabMixins
{
    /// <summary>Adds the changes of a tab the canvas follows.</summary>
    /// <param name="tab">The tab.</param>
    extension(DocumentTabViewModel tab)
    {
        /// <summary>Gets the changes that move the marks over the pages: search hits, the reading, the focus band and the layout.</summary>
        internal IObservable<RxVoid> MarkRedraws => Signal.Merge(
            tab.Search.WhenChanged(static search => search.HighlightVersion).Skip(1).AsVoid,
            tab.ReadAloud.WhenChanged(static reading => reading.SpokenBounds).Skip(1).AsVoid,
            tab.ReadAloud.WhenChanged(static reading => reading.MarksVersion).Skip(1).AsVoid,
            tab.FocusMode.WhenChanged(static focus => focus.FocusBand).Skip(1).AsVoid,
            tab.WhenChanged(static t => t.LayoutVersion).Skip(1).AsVoid,
            tab.LayoutSettingsChanges);

        /// <summary>Gets the changes that only need the pages drawn again: tone, content, caret, tiles, picks and placing.</summary>
        internal IObservable<RxVoid> CanvasRedraws => Signal.Merge(
            tab.WhenChanged(static t => t.PageTone).Skip(1).AsVoid,
            tab.WhenChanged(static t => t.ContentVersion).Skip(1).AsVoid,
            tab.WhenChanged(static t => t.IsCaretMode).Skip(1).AsVoid,
            tab.RenderHub.TilesArrived,
            tab.Annotations.WhenChanged(static annotations => annotations.Selected).Skip(1).AsVoid,
            tab.Annotations.WhenChanged(static annotations => annotations.RegionsVersion, static annotations => annotations.Tool, static (version, _) => version).Skip(1).AsVoid,
            tab.FillAndSign.WhenChanged(static filling => filling.Placement).Skip(1).AsVoid,
            tab.Forms.WhenChanged(static forms => forms.Focused).Skip(1).AsVoid);

        /// <summary>Gets the changes to how the pages are laid out: zoom, arrangement, rotation and page by page. The zoom counts only in Free mode.</summary>
        internal IObservable<RxVoid> LayoutSettingsChanges => tab.WhenChanged(
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
            .AsVoid;

        /// <summary>Gets the places the reader asks to go after the tab is shown.</summary>
        internal IObservable<NavigationRequest> Destinations => tab.WhenChanged(static t => t.Destination).Skip(1).Where(static request => request.IsRequest);

        /// <summary>Gets the moments the document's pages were read again, after it loads, reloads or its pages change.</summary>
        internal IObservable<RxVoid> PageReads => tab.WhenChanged(static t => t.LayoutVersion).Skip(1).AsVoid;

        /// <summary>Gets the moments a menu command used the text selection, which the canvas then clears.</summary>
        internal IObservable<RxVoid> SelectionUses => Signal.Merge(
            tab.Annotations.MarkSelectionCommand.Where(static marked => marked).AsVoid,
            tab.ReadAloud.ReadFromCharacterCommand.AsVoid);

        /// <summary>Gets the tab each time caret mode is turned on.</summary>
        internal IObservable<DocumentTabViewModel> CaretModeStarts => tab.WhenChanged(static t => t.IsCaretMode)
            .Skip(1)
            .Where(static on => on)
            .SelectWith(tab, static (shown, _) => shown);

        /// <summary>Gets the moments the page tool changes.</summary>
        internal IObservable<RxVoid> PageToolChanges => tab.WhenChanged(static t => t.PageTools.PageTool).Skip(1).AsVoid;

        /// <summary>Gets whether the pages scroll by themselves, now and as it changes.</summary>
        internal IObservable<bool> AutoScrolling => tab.WhenChanged(static t => t.PageTools.IsAutoScrolling);
    }

    /// <summary>Adds the streams of the tab shown, whichever tab that is.</summary>
    /// <param name="tabs">The tab shown, or <see langword="null"/> while none is.</param>
    extension(IObservable<DocumentTabViewModel?> tabs)
    {
        /// <summary>Follows a stream of the tab shown now; a newly shown tab replaces the old tab's stream, and no tab gives nothing.</summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="select">Picks the stream from the tab.</param>
        /// <returns>The values of the shown tab's stream.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<T> Follow<T>(Func<DocumentTabViewModel, IObservable<T>> select) =>
            tabs.SwitchMap(tab => tab is null ? Signal.Never<T>() : select(tab));
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

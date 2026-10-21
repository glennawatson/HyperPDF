// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>
/// The keyboard and drag streams of the page thumbnail list. A drag is a stream of its own: it starts with a press that
/// then moves far enough, lasts as long as the desktop drag, and ends the streams that listen for drops over the list.
/// </summary>
internal static class ThumbnailPageMixins
{
    /// <summary>The distance a pointer moves before starting a page drag.</summary>
    private const double DragThreshold = 8;

    /// <summary>The fraction of an item's height separating its before and after drop positions.</summary>
    private const double Half = 0.5;

    /// <summary>Adds the thumbnail list streams.</summary>
    /// <param name="list">The thumbnail list.</param>
    extension(ThumbnailListBox list)
    {
        /// <summary>Gets the key presses that run a page command, marked handled: Delete, and Ctrl+Alt with Up or Down to reorder.</summary>
        /// <param name="tabs">The tab whose page commands the keys run.</param>
        /// <returns>The shortcuts that can run now.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PageShortcut> PageShortcuts(IObservable<DocumentTabViewModel?> tabs) =>
            list.KeyDowns
                .Inject(tabs)
                .Pick(static press =>
                {
                    var (args, tab) = press;
                    var reorder = args.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt);
                    ICommand? command = args.Key switch
                    {
                        Key.Up when reorder => tab.Pages.MoveEarlierCommand,
                        Key.Down when reorder => tab.Pages.MoveLaterCommand,
                        Key.Delete when args.KeyModifiers == KeyModifiers.None => tab.Pages.DeleteCommand,
                        _ => null,
                    };
                    return command is { } found && found.CanExecute(null) ? new PageShortcut(args, found) : default(PageShortcut?);
                })
                .Do(static shortcut => shortcut.Press.Handled = true);

        /// <summary>
        /// Gets the moments a drag of pages starts: a left press on a thumbnail, then a move past the drag threshold before
        /// the button is released. A newer press replaces the one before.
        /// </summary>
        /// <param name="tabs">The tab whose pages can be edited.</param>
        /// <returns>The drags.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ThumbnailDrag> DragStarts(IObservable<DocumentTabViewModel?> tabs) =>
            list.ItemPresses
                .Inject(tabs)
                .SwitchMap(list.MovesAfter);

        /// <summary>
        /// Gets the desktop drag in progress: the drag when it starts, then <see langword="null"/> when it ends. The
        /// drag runs once for each subscriber, so a view subscribes to it once.
        /// </summary>
        /// <param name="tabs">The tab whose pages can be edited.</param>
        /// <returns>The drag sessions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ThumbnailDrag?> DragSessions(IObservable<DocumentTabViewModel?> tabs) =>
            list.DragStarts(tabs)
                .SwitchMap(static drag => Signal.FromAsync(() => DragAsync(drag)).StartWith(drag));

        /// <summary>
        /// Gets the effects a drag over the list is offered and the drops on it, for as long as a drag from this tab's
        /// document is in progress: only a drag from the same document is accepted, and a drop moves the selected pages.
        /// </summary>
        /// <param name="tabs">The tab whose pages can be edited.</param>
        /// <returns>One value for each drag over or drop handled.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<RxVoid> DragMoves(IObservable<DocumentTabViewModel?> tabs) =>
            list.DragSessions(tabs)
                .SwitchMap(session => session is not { } drag
                    ? Signal.Empty<RxVoid>()
                    : Signal.Merge(
                        list.DragOvers
                            .Inject(tabs)
                            .Where(over => drag.DocumentId == over.State.Source.Id)
                            .Do(list.AcceptDragOver)
                            .AsVoid,
                        list.Drops
                            .Inject(tabs)
                            .Where(drop => drag.DocumentId == drop.State.Source.Id && list.InsertionAt(drop.Value.GetPosition(list)) >= 0)
                            .SelectMany(drop => Signal.FromAsync(() => list.MoveSelectionToDropAsync(drop)))));

        /// <summary>Makes the tab's page selection and selected thumbnail follow the list's selection, including changes to its selected items.</summary>
        /// <param name="tab">The tab whose thumbnails are listed.</param>
        internal void SyncSelectionTo(DocumentTabViewModel tab)
        {
            var selected = new List<int>(list.SelectedItems?.Count ?? 0);
            foreach (var item in list.SelectedItems ?? Array.Empty<object>())
            {
                if (item is ThumbnailItemViewModel thumbnail)
                {
                    selected.Add(thumbnail.PageIndex);
                }
            }

            tab.Pages.SelectPages(selected);
            tab.SelectedThumbnail = list.SelectedItem as ThumbnailItemViewModel;
        }

        /// <summary>Gets the thumbnail under a point in the list.</summary>
        /// <param name="point">The list-relative point.</param>
        /// <returns>The thumbnail, or null outside an item.</returns>
        internal ThumbnailItemViewModel? ThumbnailAt(Point point)
        {
            foreach (var visual in list.GetVisualsAt(point))
            {
                if (visual is Control { DataContext: ThumbnailItemViewModel item })
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Gets the boundary before or after the thumbnail under the pointer.</summary>
        /// <param name="point">The list-relative point.</param>
        /// <returns>The insertion boundary, or -1 outside an item.</returns>
        internal int InsertionAt(Point point)
        {
            if (list.ThumbnailAt(point) is not { } item || list.ContainerFromIndex(item.PageIndex) is not { } container
                || container.TranslatePoint(default, list) is not { } top)
            {
                return -1;
            }

            return item.PageIndex + (point.Y - top.Y >= container.Bounds.Height * Half ? 1 : 0);
        }

        /// <summary>
        /// Gets the first move past the drag threshold after a press that can start a drag, with the left button still held.
        /// A press that cannot start a drag, or a release before the threshold, gives nothing. A press on a page of a
        /// group selection keeps the selection, so the whole group is dragged.
        /// </summary>
        /// <param name="press">The press and the tab whose pages can be edited.</param>
        /// <returns>The drag, once.</returns>
        internal IObservable<ThumbnailDrag> MovesAfter(Injected<PointerPressedEventArgs, DocumentTabViewModel> press)
        {
            var (args, tab) = press;
            if (!tab.Pages.CanEdit || args.KeyModifiers != KeyModifiers.None || !args.GetCurrentPoint(list).Properties.IsLeftButtonPressed
                || list.ThumbnailAt(args.GetPosition(list)) is not { } item)
            {
                return Signal.Empty<ThumbnailDrag>();
            }

            if (tab.Pages.SelectedPages.Count > 1 && IsSelected(tab.Pages.SelectedPages, item.PageIndex))
            {
                args.Handled = true;
                _ = list.Focus();
            }

            var drag = new ThumbnailDrag(args, args.GetPosition(list), tab.Source.Id);
            return list.PointerMoves
                .TakeUntil(list.PointerReleases)
                .Where(move => move.GetCurrentPoint(list).Properties.IsLeftButtonPressed && PassedDragThreshold(move.GetPosition(list), drag.Point))
                .Take(1)
                .Select(_ => drag);
        }

        /// <summary>Accepts a drag over a position that is between thumbnails, when the tab's pages can be edited.</summary>
        /// <param name="over">The drag event and the current tab.</param>
        internal void AcceptDragOver(Injected<DragEventArgs, DocumentTabViewModel> over)
        {
            var (args, tab) = over;
            args.Handled = true;
            args.DragEffects = tab.Pages.CanEdit && list.InsertionAt(args.GetPosition(list)) >= 0
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        /// <summary>Moves the selected pages to the indicated before or after position, and reports failures.</summary>
        /// <param name="drop">The drop event and the current tab.</param>
        /// <returns>A task completing after the edit.</returns>
        internal async Task<RxVoid> MoveSelectionToDropAsync(Injected<DragEventArgs, DocumentTabViewModel> drop)
        {
            var (args, tab) = drop;
            args.Handled = true;
            var insertion = list.InsertionAt(args.GetPosition(list));
            var removedBefore = 0;
            foreach (var page in tab.Pages.SelectedPages)
            {
                if (page < insertion)
                {
                    removedBefore++;
                }
            }

            try
            {
                await tab.Pages.MoveAsync(insertion - removedBefore).ConfigureAwait(true);
            }
            catch (Exception error)
            {
                ErrorTrace.Write(error);
            }

            return RxVoid.Default;
        }
    }

    /// <summary>Runs the desktop drag while retaining the originating document identity.</summary>
    /// <param name="drag">The originating press.</param>
    /// <returns>A task completing when the drag ends, with <see langword="null"/> as the session that follows.</returns>
    private static async Task<ThumbnailDrag?> DragAsync(ThumbnailDrag drag)
    {
        try
        {
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(drag.DocumentId.ToString(CultureInfo.InvariantCulture)));
            _ = await DragDrop.DoDragDropAsync(drag.Press, data, DragDropEffects.Move).ConfigureAwait(true);
        }
        catch (Exception error)
        {
            ErrorTrace.Write(error);
        }

        return null;
    }

    /// <summary>Determines whether a page is one of the selected pages.</summary>
    /// <param name="selected">The selected pages.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when it is selected.</returns>
    private static bool IsSelected(IReadOnlyList<int> selected, int page)
    {
        for (var i = 0; i < selected.Count; i++)
        {
            if (selected[i] == page)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks whether the pointer moved far enough to start dragging.</summary>
    /// <param name="point">The current pointer position.</param>
    /// <param name="origin">The position of the press.</param>
    /// <returns>Whether either axis passed the drag threshold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PassedDragThreshold(Point point, Point origin) =>
        Math.Abs(point.X - origin.X) >= DragThreshold || Math.Abs(point.Y - origin.Y) >= DragThreshold;

    /// <summary>The press that may start a page drag.</summary>
    /// <param name="Press">The pointer event required by the desktop drag API.</param>
    /// <param name="Point">The starting position.</param>
    /// <param name="DocumentId">The document identity at the start of the press.</param>
    [DebuggerDisplay("ThumbnailDrag: document {DocumentId}")]
    internal readonly record struct ThumbnailDrag(PointerPressedEventArgs Press, Point Point, int DocumentId);

    /// <summary>A key press that runs a page command.</summary>
    /// <param name="Press">The key press, marked handled when the command runs.</param>
    /// <param name="Command">The command.</param>
    internal readonly record struct PageShortcut(KeyEventArgs Press, ICommand Command);
}

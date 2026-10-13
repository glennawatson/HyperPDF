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
using HyperPdf.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Views;

/// <summary>Owns thumbnail keyboard and drag subscriptions while the document view is active.</summary>
[DebuggerDisplay("ThumbnailPageInteraction: {_dragging}")]
internal sealed class ThumbnailPageInteraction : IDisposable
{
    /// <summary>The distance a pointer moves before starting a page drag.</summary>
    private const double DragThreshold = 8;

    /// <summary>The fraction of an item's height separating its before and after drop positions.</summary>
    private const double Half = 0.5;

    /// <summary>The thumbnail list.</summary>
    private readonly ThumbnailListBox _list;

    /// <summary>Owns routed event subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>The pending pointer press, including its document identity.</summary>
    private ThumbnailDrag? _drag;

    /// <summary>Whether a desktop drag is in progress.</summary>
    private bool _dragging;

    /// <summary>Whether this activation ended.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="ThumbnailPageInteraction"/> class.</summary>
    /// <param name="tabs">The tab whose page actions the thumbnails run.</param>
    /// <param name="list">The thumbnail list.</param>
    internal ThumbnailPageInteraction(IObservable<DocumentTabViewModel?> tabs, ThumbnailListBox list)
    {
        _list = list;
        DragDrop.SetAllowDrop(list, true);

        // The lambdas that read _drag and _dragging capture this object, which owns the drag in progress.
        _subscriptions =
        [
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
                .Do(static shortcut => shortcut.Press.Handled = true)
                .SubscribeSafe(static shortcut => shortcut.Command.Execute(null), OnError),
            list.SelectionChanges.InjectLatest(tabs).SubscribeSafe(SyncSelection, OnError),
            list.ItemPresses
                .Do(_ => _drag = null)
                .Inject(tabs)
                .Where(static press => press.State.Pages.CanEdit && press.Value.KeyModifiers == KeyModifiers.None)
                .Where(press => press.Value.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
                .SubscribeSafe(RecordPress, OnError),
            list.PointerMoves
                .Where(move => !_dragging && move.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
                .Pick(move => _drag is { } drag && PassedDragThreshold(move.GetPosition(list), drag.Point) ? drag : default(ThumbnailDrag?))
                .SubscribeSafe(drag => _ = DragAsync(drag), OnError),
            list.PointerReleases.Where(_ => !_dragging).SubscribeSafe(_ => _drag = null, OnError),
            list.DragOvers
                .Inject(tabs)
                .Where(over => _dragging && _drag is { } drag && drag.DocumentId == over.State.Source.Id)
                .SubscribeSafe(AcceptDragOver, OnError),
            list.Drops
                .Inject(tabs)
                .Where(drop => !_disposed && _dragging && _drag is { } drag && drag.DocumentId == drop.State.Source.Id && InsertionAt(drop.Value.GetPosition(list)) >= 0)
                .SubscribeSafe(MoveSelectionToDrop, OnError),
        ];
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        _drag = null;
        _subscriptions.Dispose();
        DragDrop.SetAllowDrop(_list, false);
    }

    /// <summary>Reports an unexpected subscription failure.</summary>
    /// <param name="error">The failure.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Observes completion of a drop's page edit.</summary>
    /// <param name="pages">The originating selection.</param>
    /// <param name="destination">The destination after removing selected pages.</param>
    /// <returns>A task completing after the edit.</returns>
    private static async Task MoveAsync(PageManagementViewModel pages, int destination)
    {
        try
        {
            await pages.MoveAsync(destination).ConfigureAwait(true);
        }
        catch (Exception error)
        {
            OnError(error);
        }
    }

    /// <summary>Checks whether the pointer moved far enough to start dragging.</summary>
    /// <param name="point">The current pointer position.</param>
    /// <param name="origin">The position of the press.</param>
    /// <returns>Whether either axis passed the drag threshold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PassedDragThreshold(Point point, Point origin) =>
        Math.Abs(point.X - origin.X) >= DragThreshold || Math.Abs(point.Y - origin.Y) >= DragThreshold;

    /// <summary>Normalises user selection changes, including mutations of the list's selected-items collection.</summary>
    /// <param name="tab">The tab whose thumbnails are listed.</param>
    private void SyncSelection(DocumentTabViewModel tab)
    {
        var selected = new List<int>(_list.SelectedItems?.Count ?? 0);
        foreach (var item in _list.SelectedItems ?? Array.Empty<object>())
        {
            if (item is ThumbnailItemViewModel thumbnail)
            {
                selected.Add(thumbnail.PageIndex);
            }
        }

        tab.Pages.SelectPages(selected);
        tab.SelectedThumbnail = _list.SelectedItem as ThumbnailItemViewModel;
    }

    /// <summary>Records a left press and preserves a group selection when dragging one of its pages.</summary>
    /// <param name="press">The pointer press and the tab whose pages can be edited.</param>
    private void RecordPress(Injected<PointerPressedEventArgs, DocumentTabViewModel> press)
    {
        var (args, tab) = press;
        if (ThumbnailAt(args.GetPosition(_list)) is not { } item)
        {
            return;
        }

        var selected = false;
        foreach (var page in tab.Pages.SelectedPages)
        {
            selected |= page == item.PageIndex;
        }

        if (tab.Pages.SelectedPages.Count > 1 && selected)
        {
            args.Handled = true;
            _ = _list.Focus();
        }

        _drag = new(args, args.GetPosition(_list), tab.Source.Id);
    }

    /// <summary>Runs the desktop drag while retaining the originating document identity.</summary>
    /// <param name="drag">The originating press.</param>
    /// <returns>A task completing when the drag ends.</returns>
    private async Task DragAsync(ThumbnailDrag drag)
    {
        _dragging = true;
        try
        {
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(drag.DocumentId.ToString(CultureInfo.InvariantCulture)));
            _ = await DragDrop.DoDragDropAsync(drag.Press, data, DragDropEffects.Move).ConfigureAwait(true);
        }
        catch (Exception error)
        {
            OnError(error);
        }
        finally
        {
            _dragging = false;
            _drag = null;
        }
    }

    /// <summary>Accepts only a drag from this activation's current document.</summary>
    /// <param name="over">The drag event and the current tab.</param>
    private void AcceptDragOver(Injected<DragEventArgs, DocumentTabViewModel> over)
    {
        var (args, tab) = over;
        args.Handled = true;
        args.DragEffects = tab.Pages.CanEdit && InsertionAt(args.GetPosition(_list)) >= 0
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    /// <summary>Moves the selection to the indicated before or after position.</summary>
    /// <param name="drop">The drop event and the current tab.</param>
    private void MoveSelectionToDrop(Injected<DragEventArgs, DocumentTabViewModel> drop)
    {
        var (args, tab) = drop;
        args.Handled = true;
        var insertion = InsertionAt(args.GetPosition(_list));
        var removedBefore = 0;
        foreach (var page in tab.Pages.SelectedPages)
        {
            if (page < insertion)
            {
                removedBefore++;
            }
        }

        // MoveAsync reports expected edit failures; this observer also reports unexpected failures.
        _ = MoveAsync(tab.Pages, insertion - removedBefore);
    }

    /// <summary>Gets the thumbnail under a point in the list.</summary>
    /// <param name="point">The list-relative point.</param>
    /// <returns>The thumbnail, or null outside an item.</returns>
    private ThumbnailItemViewModel? ThumbnailAt(Point point)
    {
        foreach (var visual in _list.GetVisualsAt(point))
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
    private int InsertionAt(Point point)
    {
        if (ThumbnailAt(point) is not { } item || _list.ContainerFromIndex(item.PageIndex) is not { } container
            || container.TranslatePoint(default, _list) is not { } top)
        {
            return -1;
        }

        return item.PageIndex + (point.Y - top.Y >= container.Bounds.Height * Half ? 1 : 0);
    }

    /// <summary>The press that may start a page drag.</summary>
    /// <param name="Press">The pointer event required by the desktop drag API.</param>
    /// <param name="Point">The starting position.</param>
    /// <param name="DocumentId">The document identity at the start of the press.</param>
    [DebuggerDisplay("ThumbnailDrag: document {DocumentId}")]
    private readonly record struct ThumbnailDrag(PointerPressedEventArgs Press, Point Point, int DocumentId);

    /// <summary>A key press that runs a page command.</summary>
    /// <param name="Press">The key press, marked handled when the command runs.</param>
    /// <param name="Command">The command.</param>
    private readonly record struct PageShortcut(KeyEventArgs Press, ICommand Command);
}

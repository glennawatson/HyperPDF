// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    /// <summary>The view whose current tab supplies page actions.</summary>
    private readonly DocumentView _view;

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
    /// <param name="view">The active view.</param>
    /// <param name="list">The thumbnail list.</param>
    internal ThumbnailPageInteraction(DocumentView view, ThumbnailListBox list)
    {
        _view = view;
        _list = list;
        DragDrop.SetAllowDrop(list, true);
        _subscriptions =
        [
            list.ObserveRouted(InputElement.KeyDownEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnKey, OnError),
            list.ObserveRouted(SelectingItemsControl.SelectionChangedEvent).SubscribeSafe(_ => OnSelectionChanged(), OnError),
            list.ObserveRouted(InputElement.PointerPressedEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnPressed, OnError),
            list.ObserveRouted(InputElement.PointerMovedEvent).SubscribeSafe(OnMoved, OnError),
            list.ObserveRouted(InputElement.PointerReleasedEvent).SubscribeSafe(_ => ResetPress(), OnError),
            list.ObserveRouted(DragDrop.DragOverEvent).SubscribeSafe(OnDragOver, OnError),
            list.ObserveRouted(DragDrop.DropEvent).SubscribeSafe(OnDrop, OnError),
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
    private void OnSelectionChanged()
    {
        if (_view.ViewModel is not { } tab || _list.SelectedItems is not { } items)
        {
            return;
        }

        var selected = new List<int>(items.Count);
        foreach (var item in items)
        {
            if (item is ThumbnailItemViewModel thumbnail)
            {
                selected.Add(thumbnail.PageIndex);
            }
        }

        tab.Pages.SelectPages(selected);
        tab.SelectedThumbnail = _list.SelectedItem as ThumbnailItemViewModel;
    }

    /// <summary>Runs a labelled page command when its shortcut is available.</summary>
    /// <param name="args">The key event.</param>
    private void OnKey(KeyEventArgs args)
    {
        if (_view.ViewModel?.Pages is not { } pages)
        {
            return;
        }

        ICommand? command = null;
        if (args.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt))
        {
            command = args.Key switch
            {
                Key.Up => pages.MoveEarlierCommand,
                Key.Down => pages.MoveLaterCommand,
                _ => null,
            };
        }
        else if (args.Key == Key.Delete && args.KeyModifiers == KeyModifiers.None)
        {
            command = pages.DeleteCommand;
        }

        if (command is null || !command.CanExecute(null))
        {
            return;
        }

        args.Handled = true;
        command.Execute(null);
    }

    /// <summary>Records a left press and preserves a group selection when dragging one of its pages.</summary>
    /// <param name="args">The pointer press.</param>
    private void OnPressed(PointerPressedEventArgs args)
    {
        _drag = null;
        if (_view.ViewModel is not { Pages.CanEdit: true } tab || args.KeyModifiers != KeyModifiers.None
            || !args.GetCurrentPoint(_list).Properties.IsLeftButtonPressed
            || ThumbnailAt(args.GetPosition(_list)) is not { } item)
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

    /// <summary>Starts a desktop drag once the press moves far enough.</summary>
    /// <param name="args">The pointer movement.</param>
    private void OnMoved(PointerEventArgs args)
    {
        if (_dragging || _drag is not { } drag || !args.GetCurrentPoint(_list).Properties.IsLeftButtonPressed
            || !PassedDragThreshold(args.GetPosition(_list), drag.Point))
        {
            return;
        }

        // DragAsync observes and reports its own failures, since routed events cannot await the desktop drag.
        _ = DragAsync(drag);
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
    /// <param name="args">The drag event.</param>
    private void OnDragOver(DragEventArgs args)
    {
        if (!_dragging || _drag is not { } drag || _view.ViewModel is not { } tab || drag.DocumentId != tab.Source.Id)
        {
            return;
        }

        args.Handled = true;
        args.DragEffects = tab.Pages.CanEdit && InsertionAt(args.GetPosition(_list)) >= 0
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    /// <summary>Moves the selection to the indicated before or after position.</summary>
    /// <param name="args">The drop event.</param>
    private void OnDrop(DragEventArgs args)
    {
        var insertion = InsertionAt(args.GetPosition(_list));
        if (_disposed || !_dragging || _drag is not { } drag || _view.ViewModel is not { } tab || drag.DocumentId != tab.Source.Id || insertion < 0)
        {
            return;
        }

        args.Handled = true;
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

    /// <summary>Forgets a press that ended without starting a drag.</summary>
    private void ResetPress()
    {
        if (!_dragging)
        {
            _drag = null;
        }
    }

    /// <summary>The press that may start a page drag.</summary>
    /// <param name="Press">The pointer event required by the desktop drag API.</param>
    /// <param name="Point">The starting position.</param>
    /// <param name="DocumentId">The document identity at the start of the press.</param>
    [DebuggerDisplay("ThumbnailDrag: document {DocumentId}")]
    private sealed record ThumbnailDrag(PointerPressedEventArgs Press, Point Point, int DocumentId);
}

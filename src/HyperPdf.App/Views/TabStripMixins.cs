// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>The streams of the tab strip: dragging tabs, middle clicks, hover previews and sideways scrolling.</summary>
internal static class TabStripMixins
{
    /// <summary>How far the pointer must move before a tab drag starts.</summary>
    private const double DragThreshold = 6;

    /// <summary>Pixels the tab strip scrolls per wheel notch.</summary>
    private const double WheelStep = 60;

    /// <summary>Adds the tab strip streams.</summary>
    /// <param name="strip">The tab strip.</param>
    extension(ListBox strip)
    {
        /// <summary>Gets the tab being dragged: a left press on a tab starts one and any release ends it.</summary>
        internal IObservable<TabDrag?> TabDrags => Signal.Merge(
            strip.ItemPresses
                .WhereWith(strip, static (list, press) => press.GetCurrentPoint(list).Properties.IsLeftButtonPressed)
                .SelectWith(strip, static (list, press) => TabAt(list, press.GetPosition(list)) is { } tab ? new TabDrag(tab, press.GetPosition(list)) : null),
            strip.PointerReleases.Select(static _ => (TabDrag?)null));

        /// <summary>Gets the moves of a dragged tab onto the place of the tab under the pointer.</summary>
        internal IObservable<TabReorder> TabReorders => strip.PointerMoves
            .WithLatestFrom(strip.TabDrags, static (move, drag) => new TabMove(move, drag))
            .SelectWith(
                strip,
                static (list, move) =>
                {
                    var position = move.Move.GetPosition(list);
                    return move.Drag is { } drag && Math.Abs(position.X - drag.Start.X) >= DragThreshold && TabAt(list, position) is { } target && target != drag.Tab
                        ? new TabReorder(drag, target, position)
                        : default(TabReorder?);
                })
            .KeepValues
            .Do(static reorder => reorder.Drag.Start = reorder.Position);

        /// <summary>Gets the tabs closed by a middle click, which the strip marks handled.</summary>
        internal IObservable<DocumentTabViewModel> TabMiddleClicks => strip.ItemPresses
            .WhereWith(strip, static (list, press) => press.GetCurrentPoint(list).Properties.IsMiddleButtonPressed)
            .MarkHandled()
            .SourceItems<DocumentTabViewModel>();

        /// <summary>Gets the sideways scrolls the wheel makes, which the strip marks handled.</summary>
        internal IObservable<TabScroll> TabScrolls => strip.PointerWheels
            .MarkHandled()
            .SelectWith(
                strip,
                static (list, wheel) => list.FindDescendantOfType<ScrollViewer>() is { } scroller
                    ? new TabScroll(scroller, new(Math.Max(0, scroller.Offset.X - ((wheel.Delta.Y + wheel.Delta.X) * WheelStep)), scroller.Offset.Y))
                    : default(TabScroll?))
            .KeepValues;

        /// <summary>Gets the tabs whose hover preview is about to open.</summary>
        internal IObservable<DocumentTabViewModel> TabPreviewOpenings => strip.ToolTipOpenings.SourceItems<DocumentTabViewModel>();
    }

    /// <summary>Gets the tab under a point in the strip, read from the data context of the control there.</summary>
    /// <param name="strip">The tab strip.</param>
    /// <param name="point">The point in tab strip coordinates.</param>
    /// <returns>The tab, if any.</returns>
    private static DocumentTabViewModel? TabAt(ListBox strip, Point point)
    {
        foreach (var visual in strip.GetVisualsAt(point))
        {
            if (visual is Control { DataContext: DocumentTabViewModel tab })
            {
                return tab;
            }
        }

        return null;
    }
}

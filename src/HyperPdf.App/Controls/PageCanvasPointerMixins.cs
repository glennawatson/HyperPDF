// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Input;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Presentation.Input;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>
/// The pointer chains of <see cref="PageCanvas"/>. Each turns an Avalonia event into the neutral one, lets the presentation layer
/// decide what it does, and then does what that asks of Avalonia: focus, capture, the pointer shape, a menu or a picture.
/// </summary>
internal static class PageCanvasPointerMixins
{
    /// <summary>Adds the pointer chains and the carrying out of their effects to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the pointer presses, paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The presses to route.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PointerPressRoute> PointerPressRoutes(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerPresses
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new PointerPressRoute(c, item.Value));

        /// <summary>Gets the pointer movements, paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The movements to route.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PointerMoveRoute> PointerMoveRoutes(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerMoves
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new PointerMoveRoute(c, item.Value));

        /// <summary>Gets the pointer releases, paired with the active tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The releases to route.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<PointerReleaseRoute> PointerReleaseRoutes(IObservable<DocumentTabViewModel?> tabs) =>
            canvas.PointerReleases
                .Inject(tabs)
                .SelectWith(canvas, static (c, item) => new PointerReleaseRoute(c, item.Value));

        /// <summary>Does what the presentation layer asked for after a pointer event.</summary>
        /// <param name="effects">What was asked for.</param>
        /// <param name="pointer">The pointer of the event.</param>
        /// <param name="position">Where the pointer is on the canvas.</param>
        internal void ApplyInputEffects(InputEffects effects, IPointer pointer, Point position)
        {
            if (effects.Focus)
            {
                _ = canvas.Focus();
            }

            if (effects.Cursor is { } cursor)
            {
                canvas.Cursor = cursor.ToCursor();
            }

            if (effects.Capture)
            {
                pointer.Capture(canvas);
            }

            if (effects.ReleaseCapture)
            {
                pointer.Capture(null);
            }

            canvas.ApplyOpenedUi(effects, position);
        }

        /// <summary>Opens the menu or takes the picture that a pointer event asked for.</summary>
        /// <param name="effects">What was asked for.</param>
        /// <param name="position">Where the pointer is on the canvas.</param>
        private void ApplyOpenedUi(InputEffects effects, Point position)
        {
            if (canvas.Tab is not { } tab)
            {
                return;
            }

            if (effects.ShowContextMenu)
            {
                canvas.ShowContextMenu(position);
            }

            if (effects.Choices is { } field)
            {
                canvas.ShowChoices(tab.Forms, field);
            }

            if (effects.Snapshot is { } area)
            {
                _ = canvas.Snapshotter.SnapshotAsync(canvas, tab, new(area.X, area.Y, area.Width, area.Height));
            }
        }
    }

    /// <summary>A pointer press on the page canvas.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer press arguments.</param>
    internal readonly record struct PointerPressRoute(PageCanvas Canvas, PointerPressedEventArgs Args)
    {
        /// <summary>Lets the presentation layer decide what the press does, and does what it asks.</summary>
        internal void Apply()
        {
            var point = Args.GetCurrentPoint(Canvas);
            var inScroller = Canvas.Scroller is { } scroller ? Args.GetPosition(scroller) : point.Position;
            var press = new ViewerPress(
                new(point.Position.X, point.Position.Y),
                new(inScroller.X, inScroller.Y),
                point.ViewerButtons,
                ViewerKeyMixins.ToViewerModifiers(Args.KeyModifiers),
                Args.ClickCount,
                Args.Handled);
            var effects = Canvas.State.Press(press);
            Canvas.ApplyInputEffects(effects, Args.Pointer, point.Position);
            if (effects.Handled)
            {
                Args.Handled = true;
            }
        }
    }

    /// <summary>A pointer movement over the page canvas.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer event arguments.</param>
    internal readonly record struct PointerMoveRoute(PageCanvas Canvas, PointerEventArgs Args)
    {
        /// <summary>Lets the presentation layer decide what the movement does, and does what it asks.</summary>
        internal void Apply()
        {
            var position = Args.GetPosition(Canvas);
            var inScroller = Canvas.Scroller is { } scroller ? Args.GetPosition(scroller) : position;
            var move = new ViewerMove(
                new(position.X, position.Y),
                new(inScroller.X, inScroller.Y),
                Args.GetCurrentPoint(Canvas).Properties.IsLeftButtonPressed);
            Canvas.ApplyInputEffects(Canvas.State.Move(move), Args.Pointer, position);
        }
    }

    /// <summary>A pointer release over the page canvas.</summary>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Args">The pointer release arguments.</param>
    internal readonly record struct PointerReleaseRoute(PageCanvas Canvas, PointerReleasedEventArgs Args)
    {
        /// <summary>Lets the presentation layer decide what the release does, and does what it asks.</summary>
        internal void Apply()
        {
            var position = Args.GetPosition(Canvas);
            var effects = Canvas.State.Release(new(position.X, position.Y), Args.Handled);
            Canvas.ApplyInputEffects(effects, Args.Pointer, position);
            if (effects.Handled)
            {
                Args.Handled = true;
            }
        }
    }
}

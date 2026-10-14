// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// The attached events Avalonia exposes only as static add and remove methods. <c>Events()</c> reads instance events,
/// so these use the factory Primitives documents for add and remove pairs.
/// </summary>
internal static class AttachedEventMixins
{
    /// <summary>Adds the tool tip stream.</summary>
    /// <param name="target">The control whose children show tool tips.</param>
    extension(Control target)
    {
        /// <summary>Gets the tool tips about to open for the control or its children.</summary>
        internal IObservable<CancelRoutedEventArgs> ToolTipOpenings => Signal.FromEvent<EventHandler<CancelRoutedEventArgs>, CancelRoutedEventArgs>(
            static next => (_, args) => next(args),
            handler => ToolTip.AddToolTipOpeningHandler(target, handler),
            handler => ToolTip.RemoveToolTipOpeningHandler(target, handler));
    }

    /// <summary>Adds the wheel stream that runs before the control's own handling.</summary>
    /// <param name="target">The element.</param>
    extension(InputElement target)
    {
        /// <summary>
        /// Gets the wheel turns on their way down to the pointer's target. <c>Events()</c> reads bubbling events, but a
        /// scroll viewer and its scroll bars handle the wheel while it bubbles, so only a tunnelling handler sees every
        /// turn first. The wheel and the key presses below are the routed events the app reads this way.
        /// </summary>
        internal IObservable<PointerWheelEventArgs> TunnelWheels => Signal.FromEvent<EventHandler<PointerWheelEventArgs>, PointerWheelEventArgs>(
            static next => (_, args) => next(args),
            handler => target.AddHandler(InputElement.PointerWheelChangedEvent, handler, RoutingStrategies.Tunnel),
            handler => target.RemoveHandler(InputElement.PointerWheelChangedEvent, handler));

        /// <summary>
        /// Gets the key presses on their way down to the focused element. A tree or list handles arrow keys while the press
        /// bubbles, even with Ctrl and Shift held, so only a tunnelling handler sees every press first.
        /// </summary>
        internal IObservable<KeyEventArgs> TunnelKeyDowns => Signal.FromEvent<EventHandler<KeyEventArgs>, KeyEventArgs>(
            static next => (_, args) => next(args),
            handler => target.AddHandler(InputElement.KeyDownEvent, handler, RoutingStrategies.Tunnel),
            handler => target.RemoveHandler(InputElement.KeyDownEvent, handler));
    }

    /// <summary>Adds the drag and drop streams.</summary>
    /// <param name="target">The element that accepts drops.</param>
    extension(Interactive target)
    {
        /// <summary>Gets the drags moving over the element.</summary>
        internal IObservable<DragEventArgs> DragOvers => Signal.FromEvent<EventHandler<DragEventArgs>, DragEventArgs>(
            static next => (_, args) => next(args),
            handler => DragDrop.AddDragOverHandler(target, handler),
            handler => DragDrop.RemoveDragOverHandler(target, handler));

        /// <summary>Gets the drops on the element.</summary>
        internal IObservable<DragEventArgs> Drops => Signal.FromEvent<EventHandler<DragEventArgs>, DragEventArgs>(
            static next => (_, args) => next(args),
            handler => DragDrop.AddDropHandler(target, handler),
            handler => DragDrop.RemoveDropHandler(target, handler));
    }
}

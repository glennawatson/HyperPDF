// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Interactivity;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Observes routed events that have no <c>Events()</c> member: attached events such as drag and drop, events bubbling
/// up from children, and subscriptions that must also see handled events.
/// </summary>
internal static class RoutedEventMixins
{
    /// <summary>The routes a handler normally listens on.</summary>
    private const RoutingStrategies DefaultRoutes = RoutingStrategies.Direct | RoutingStrategies.Bubble;

    /// <summary>Adds routed event observation to interactive elements.</summary>
    /// <param name="target">The element the handler is added to.</param>
    extension(Interactive target)
    {
        /// <summary>Observes a routed event raised on, or routed through, this element.</summary>
        /// <typeparam name="TEventArgs">The event arguments.</typeparam>
        /// <param name="routedEvent">The routed event.</param>
        /// <param name="routes">The routing strategies to listen on.</param>
        /// <param name="handledEventsToo">Whether to see events another handler already handled.</param>
        /// <returns>The event arguments, one per raised event, while subscribed.</returns>
        internal IObservable<TEventArgs> ObserveRouted<TEventArgs>(
            RoutedEvent<TEventArgs> routedEvent,
            RoutingStrategies routes = DefaultRoutes,
            bool handledEventsToo = false)
            where TEventArgs : RoutedEventArgs
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(routedEvent);
            return Signal.FromEvent<EventHandler<TEventArgs>, TEventArgs>(
                static next => (_, e) => next(e),
                handler => target.AddHandler(routedEvent, handler, routes, handledEventsToo),
                handler => target.RemoveHandler(routedEvent, handler));
        }
    }
}

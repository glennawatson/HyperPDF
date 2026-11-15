// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer;

/// <summary>Splits the events of a window into one stream for each kind of event.</summary>
internal static class WindowEventMixins
{
    /// <summary>Operators over the events of a window.</summary>
    /// <param name="events">The events.</param>
    extension(IObservable<WindowEvent> events)
    {
        /// <summary>Keeps the events of one kind.</summary>
        /// <param name="kind">The kind of event wanted.</param>
        /// <returns>The events of that kind.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<WindowEvent> WhereEventKind(WindowEventKind kind) => events.WhereWith(kind, static (wanted, windowEvent) => windowEvent.Kind == wanted);

        /// <summary>Keeps the events that something that uses events does not use, which then go on to the next user of events.</summary>
        /// <typeparam name="TUser">The kind of thing that uses events.</typeparam>
        /// <param name="user">What uses the events.</param>
        /// <param name="uses">Tells whether the user used an event.</param>
        /// <returns>The events that were not used.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<WindowEvent> WhereUnused<TUser>(TUser user, Func<TUser, WindowEvent, bool> uses) => events.WhereWith(user, (owner, windowEvent) => !uses(owner, windowEvent));
    }
}

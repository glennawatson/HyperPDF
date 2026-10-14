// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// The streams and operators of the window shell: keys no earlier handler used, flyout closings, attached property
/// changes and operating system signals.
/// </summary>
internal static class ShellEventMixins
{
    /// <summary>Adds the change stream of an attached property.</summary>
    /// <typeparam name="TValue">The property's value type.</typeparam>
    /// <param name="property">The property.</param>
    extension<TValue>(AvaloniaProperty<TValue> property)
    {
        /// <summary>
        /// Gets the changes of the property on every control that sets it. An attached property set in XAML raises no
        /// instance event, so this reads the property's class-wide change stream; it is the one place the app does.
        /// </summary>
        internal IObservable<AvaloniaPropertyChangedEventArgs<TValue>> ClassChanges => property.Changed;
    }

    /// <summary>Adds the closing stream of a button's flyout.</summary>
    /// <param name="button">The button.</param>
    extension(Button button)
    {
        /// <summary>Gets the moments the button's flyout closes.</summary>
        internal IObservable<EventArgs> FlyoutClosings => button.Flyout!.Events().Closed;
    }

    /// <summary>Adds the filter for routed events no earlier subscriber used.</summary>
    /// <typeparam name="TArgs">The event arguments.</typeparam>
    /// <param name="events">The events.</param>
    extension<TArgs>(IObservable<TArgs> events)
        where TArgs : RoutedEventArgs
    {
        /// <summary>Keeps the events that are not yet marked handled, so a later step in the window acts only when nothing earlier did.</summary>
        /// <returns>The unhandled events.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TArgs> Unhandled() => events.Where(static args => !args.Handled);
    }

    /// <summary>Adds the operating system signal stream.</summary>
    extension(PosixSignalRegistration)
    {
        /// <summary>
        /// Gets the requests the operating system sends with a signal, such as SIGTERM at logout. Each request is
        /// cancelled as it arrives, so the app decides how to stop. The registration lasts as long as the subscription.
        /// </summary>
        /// <param name="signal">The signal.</param>
        /// <returns>The requests, on the thread the runtime raises them on.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static IObservable<PosixSignalContext> Requests(PosixSignal signal) =>
            Signal.Create<PosixSignalContext>(observer => PosixSignalRegistration.Create(
                signal,
                context =>
                {
                    context.Cancel = true;
                    observer.OnNext(context);
                }));
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>Turns a fixed value into the command parameter a bound button or menu item passes.</summary>
internal static class CommandParameterMixins
{
    /// <summary>Adds the command parameter stream.</summary>
    /// <typeparam name="T">The parameter type.</typeparam>
    /// <param name="value">The parameter.</param>
    extension<T>(T value)
    {
        /// <summary>
        /// Gets a stream that gives the value and stays open. The command binder keeps the control's parameter only while
        /// the stream is open; after a plain <c>Signal.Return</c> completes, the control passes null to the command.
        /// </summary>
        internal IObservable<T> AsCommandParameter => Signal.Concat(Signal.Return(value), Signal.Never<T>());
    }
}

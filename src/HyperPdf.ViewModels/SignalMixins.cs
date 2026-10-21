// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels;

/// <summary>Emitting on a tab's signals from work that may finish after the tab was closed.</summary>
internal static class SignalMixins
{
    /// <summary>Adds the tolerant emit to a signal.</summary>
    /// <typeparam name="T">The type of value the signal emits.</typeparam>
    /// <param name="signal">The signal.</param>
    extension<T>(Signal<T> signal)
    {
        /// <summary>Emits a value unless the signal was disposed, which a disposed signal otherwise answers by throwing.</summary>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EmitIfOpen(T value)
        {
            if (!signal.IsDisposed)
            {
                signal.OnNext(value);
            }
        }
    }
}

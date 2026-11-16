// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>
/// The one place where the PDF library's own async calls become signals for the rest of the app. The library returns
/// <see cref="ValueTask"/>, which <see cref="Signal.FromAsync{T}(Func{CancellationToken, Task{T}})"/> cannot take directly.
/// </summary>
internal static class LibrarySignals
{
    /// <summary>Makes a cold signal of a library call that gives no result.</summary>
    /// <param name="call">The library call, started for each subscriber and cancelled by unsubscribing.</param>
    /// <returns>A cold signal giving one value once the call is done.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<RxVoid> Run(Func<CancellationToken, ValueTask> call) => Signal.FromAsync(token => Wait(call, token));

    /// <summary>Makes a cold signal of a library call that gives a result.</summary>
    /// <typeparam name="T">The type of result.</typeparam>
    /// <param name="call">The library call, started for each subscriber and cancelled by unsubscribing.</param>
    /// <returns>A cold signal giving the result once the call is done.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IObservable<T> Run<T>(Func<CancellationToken, ValueTask<T>> call) => Signal.FromAsync(token => call(token).AsTask());

    /// <summary>Awaits a library call that gives no result.</summary>
    /// <param name="call">The library call.</param>
    /// <param name="token">Cancels the call.</param>
    /// <returns>The completion value.</returns>
    private static async Task<RxVoid> Wait(Func<CancellationToken, ValueTask> call, CancellationToken token)
    {
        await call(token).ConfigureAwait(false);
        return RxVoid.Default;
    }
}

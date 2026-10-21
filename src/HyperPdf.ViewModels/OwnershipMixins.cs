// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels;

/// <summary>Ownership of the resource a view model holds now, so the stream of its values releases each one.</summary>
internal static class OwnershipMixins
{
    /// <summary>Adds the ownership of the latest resource.</summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="resources">The resource held now, or <see langword="null"/> when none is.</param>
    extension<T>(IObservable<T?> resources)
        where T : class, IDisposable
    {
        /// <summary>
        /// Keeps the latest resource open: a new value disposes the one before, and ending the subscription disposes the
        /// last, so the subscription in the owner's container is the only thing that releases them.
        /// </summary>
        /// <returns>A stream that never produces a value; subscribe to it to own the resources.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<RxVoid> OwnLatest() =>
            resources.SwitchMap(static resource => resource is null
                ? Signal.Never<RxVoid>()
                : Signal.Using(() => resource, static _ => Signal.Never<RxVoid>()));
    }
}

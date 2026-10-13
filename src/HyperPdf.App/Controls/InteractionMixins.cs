// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Controls;

/// <summary>Handles the interactions of whichever view model a reused view shows.</summary>
internal static class InteractionMixins
{
    /// <summary>Adds the interaction handler.</summary>
    /// <typeparam name="TInput">The interaction input.</typeparam>
    /// <typeparam name="TOutput">The interaction output.</typeparam>
    /// <param name="interactions">The shown view model's interaction, from <c>WhenChanged</c>.</param>
    extension<TInput, TOutput>(IObservable<Interaction<TInput, TOutput>> interactions)
    {
        /// <summary>
        /// Keeps a handler registered on the interaction of whichever view model the view shows. A reused view stays on
        /// the view model it was given with <c>BindInteraction</c>, rather than following <c>ViewModel</c>.
        /// </summary>
        /// <param name="handler">The handler.</param>
        /// <returns>The registration.</returns>
        internal MultipleDisposable HandleWith(Func<IInteractionContext<TInput, TOutput>, Task> handler)
        {
            var registration = new SwapDisposable();
            var follow = interactions.SubscribeSafe(interaction => registration.Disposable = interaction?.RegisterHandler(handler), static error => Trace.TraceError(error.ToString()));
            return new(follow, registration);
        }
    }
}

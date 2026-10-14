// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using HyperPdf.App.Services;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Controls;

/// <summary>Handles the interactions of whichever view model a reused view shows.</summary>
internal static class InteractionMixins
{
    /// <summary>Adds the dialog handlers for interactions that show a window and give nothing back.</summary>
    /// <typeparam name="TInput">The interaction input.</typeparam>
    /// <param name="interactions">The shown view model's interaction, from <c>WhenChanged</c>.</param>
    extension<TInput>(IObservable<Interaction<TInput, RxVoid>> interactions)
    {
        /// <summary>Shows a window over a view's window for each request, and answers when the window closes.</summary>
        /// <typeparam name="TWindow">The window.</typeparam>
        /// <param name="owner">The view whose window owns the dialog.</param>
        /// <param name="create">Makes the window for a request.</param>
        /// <returns>The registration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MultipleDisposable ShowDialogOver<TWindow>(Visual owner, Func<TInput, TWindow> create)
            where TWindow : Window =>
            interactions.HandleWith(
                async context =>
                {
                    if (TopLevel.GetTopLevel(owner) is Window window)
                    {
                        await create(context.Input).ShowDialog(window);
                    }

                    context.SetOutput(RxVoid.Default);
                });
    }

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
            var follow = interactions.SubscribeSafe(interaction => registration.Disposable = interaction?.RegisterHandler(handler), ErrorTrace.Write);
            return new(follow, registration);
        }
    }

    /// <summary>Adds the dialog handlers for interactions that show a window and take a yes or no back.</summary>
    /// <typeparam name="TInput">The interaction input.</typeparam>
    /// <param name="interactions">The shown view model's interaction, from <c>WhenChanged</c>.</param>
    extension<TInput>(IObservable<Interaction<TInput, bool>> interactions)
    {
        /// <summary>Shows a window over a view's window for each request, and answers with the window's result.</summary>
        /// <typeparam name="TWindow">The window.</typeparam>
        /// <param name="owner">The view whose window owns the dialog.</param>
        /// <param name="create">Makes the window for a request.</param>
        /// <returns>The registration.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal MultipleDisposable AskDialogOver<TWindow>(Visual owner, Func<TInput, TWindow> create)
            where TWindow : Window =>
            interactions.HandleWith(
                async context =>
                {
                    var answer = TopLevel.GetTopLevel(owner) is Window window
                        && await create(context.Input).ShowDialog<bool>(window);
                    context.SetOutput(answer);
                });
    }
}

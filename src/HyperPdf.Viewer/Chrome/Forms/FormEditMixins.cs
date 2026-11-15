// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Services;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>Picks the changes of one row out of the stream of changes a form dialog sends, and writes them to a view model.</summary>
internal static class FormEditMixins
{
    /// <summary>Adds the picking to a stream of changes.</summary>
    /// <param name="edits">The changes the user makes in a form dialog.</param>
    extension(IObservable<FormEdit> edits)
    {
        /// <summary>Keeps the changes of one row.</summary>
        /// <param name="id">The row's number.</param>
        /// <returns>The changes of that row.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<FormEdit> WhereRow(int id) => edits.WhereWith(id, static (wanted, edit) => edit.Id == wanted);

        /// <summary>Writes each change of one row to a view model.</summary>
        /// <typeparam name="T">The kind of view model written to.</typeparam>
        /// <param name="id">The row's number.</param>
        /// <param name="target">The view model.</param>
        /// <param name="write">Writes a change.</param>
        /// <returns>What ends the writing.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDisposable WriteRow<T>(int id, T target, Action<T, FormEdit> write) =>
            edits.WhereRow(id).SubscribeSafe(edit => write(target, edit), ErrorTrace.Write);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms;

/// <summary>
/// Runs the actions of a document's form buttons and pages: reset, hide, show or hide layers, and the ones the host
/// handles. Scripts are never run. Implemented by documents that can; safe to call from any thread.
/// </summary>
public interface IFormActions : IDocumentFeature
{
    /// <summary>Runs the action of a form button, and the actions that follow it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <returns>A cold signal giving what happened; <see cref="FormActionResult.None"/> when the widget has no action once; unsubscribing cancels the work.</returns>
    IObservable<FormActionResult> RunWidgetAction(int pageIndex, int index, IFormActionHost host);

    /// <summary>Gets a value indicating whether a page has actions to run when it opens or closes, so the viewer can skip the run when it has none.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns><see langword="false"/> only when the page certainly has no such actions.</returns>
    bool HasPageActions(int pageIndex) => true;

    /// <summary>Runs the actions a page does when it opens.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <returns>A cold signal giving what happened once; unsubscribing cancels the work.</returns>
    IObservable<FormActionResult> RunPageOpened(int pageIndex, IFormActionHost host);

    /// <summary>Runs the actions a page does when it closes.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <returns>A cold signal giving what happened once; unsubscribing cancels the work.</returns>
    IObservable<FormActionResult> RunPageClosed(int pageIndex, IFormActionHost host);

    /// <summary>Runs the document's additional actions for one event: close, save or print.</summary>
    /// <param name="documentEvent">The event the viewer reached.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <returns>A cold signal giving what happened once; unsubscribing cancels the work.</returns>
    IObservable<FormActionResult> RunDocumentEvent(FormDocumentEvent documentEvent, IFormActionHost host);
}

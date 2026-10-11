// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Forms;

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
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>What happened; <see cref="FormActionResult.None"/> when the widget has no action.</returns>
    ValueTask<FormActionResult> RunWidgetActionAsync(int pageIndex, int index, IFormActionHost host, CancellationToken cancellationToken);

    /// <summary>Runs the actions a page does when it opens.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>What happened.</returns>
    ValueTask<FormActionResult> RunPageOpenedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken);
}

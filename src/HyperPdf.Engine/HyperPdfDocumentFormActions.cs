// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentFormActions over the document's owned state.</summary>
internal static class HyperPdfDocumentFormActions
{
    /// <summary>Runs the action of a form button, and the actions that follow it.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>What happened; <see cref="F:HyperPdf.Core.Forms.FormActionResult.None"/> when the widget has no action.</returns>
    internal static async ValueTask<FormActionResult> RunWidgetActionAsync(HyperPdfDocument self, int pageIndex, int index, IFormActionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return ReadAction(self, pageIndex, index) is { } node
            ? HyperPdfFormRuntime.Complete(self, await HyperPdfFormRuntime.CreateRunner(self, host).RunAsync(node, pageIndex, cancellationToken).ConfigureAwait(false))
            : FormActionResult.None;
    }

    /// <summary>Gets a value indicating whether a page has any additional actions.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns><see langword="true"/> when the page has additional actions.</returns>
    internal static bool HasPageActions(HyperPdfDocument self, int pageIndex) =>
        !self.IsDisposed
        && (uint)pageIndex < (uint)self.Document.PageCount
        && PdfDocumentActions.GetTriggers(self.Document, PdfDocumentPages.GetPage(self.Document, pageIndex)).Length > 0;

    /// <summary>Runs the actions a page does when it opens.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>What happened.</returns>
    internal static async ValueTask<FormActionResult> RunPageOpenedAsync(HyperPdfDocument self, int pageIndex, IFormActionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return self.IsDisposed
            ? FormActionResult.None
            : HyperPdfFormRuntime.Complete(self, await HyperPdfFormRuntime.CreateRunner(self, host).RunPageOpenedAsync(pageIndex, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Runs the actions a page does when it closes.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>What happened.</returns>
    internal static async ValueTask<FormActionResult> RunPageClosedAsync(HyperPdfDocument self, int pageIndex, IFormActionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return self.IsDisposed
            ? FormActionResult.None
            : HyperPdfFormRuntime.Complete(self, await HyperPdfFormRuntime.CreateRunner(self, host).RunPageClosedAsync(pageIndex, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Runs the document's additional actions for one event.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="documentEvent">The event the viewer reached.</param>
    /// <param name="host">The application's part of running actions.</param>
    /// <param name="cancellationToken">A token that cancels the run.</param>
    /// <returns>What happened.</returns>
    internal static async ValueTask<FormActionResult> RunDocumentEventAsync(HyperPdfDocument self, FormDocumentEvent documentEvent, IFormActionHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        return self.IsDisposed || EventKey(documentEvent) is not { } key
            ? FormActionResult.None
            : HyperPdfFormRuntime.Complete(self, await HyperPdfFormRuntime.CreateRunner(self, host).RunDocumentEventAsync(key, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Gets the key of a document event in the catalog's additional actions.</summary>
    /// <param name="documentEvent">The event.</param>
    /// <returns>The key, or <see langword="null"/> for an event the library has no action for.</returns>
    private static string? EventKey(FormDocumentEvent documentEvent) => documentEvent switch
    {
        FormDocumentEvent.WillClose => "WC",
        FormDocumentEvent.WillSave => "WS",
        FormDocumentEvent.DidSave => "DS",
        FormDocumentEvent.WillPrint => "WP",
        FormDocumentEvent.DidPrint => "DP",
        _ => null,
    };

    /// <summary>Reads the action of a widget while no edit runs.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <returns>The action, or <see langword="null"/> when the widget has none.</returns>
    private static PdfActionNode? ReadAction(HyperPdfDocument self, int pageIndex, int index)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount
            || PdfDocumentPages.GetPage(self.Document, pageIndex).Dictionary.GetArray(KnownName.Annots) is not { } annotations
            || (uint)index >= (uint)annotations.Count
            || annotations.GetDictionary(index)?.GetDictionary(KnownName.A) is not { } action
            ? null
            : PdfDocumentActions.ReadActionNode(self.Document, action);
    }
}

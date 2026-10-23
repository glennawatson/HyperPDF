// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Form actions that record the pages whose open actions ran, and hold each run until a test releases it.</summary>
/// <param name="inner">The real form actions.</param>
/// <param name="document">The document the feature belongs to.</param>
/// <param name="engine">The engine that keeps the record and the gate.</param>
internal sealed class RecordingFormActions(IFormActions inner, IDocument document, PausingEngine engine) : IFormActions
{
    /// <inheritdoc/>
    public IDocument Document => document;

    /// <inheritdoc/>
    public ValueTask<FormActionResult> RunWidgetActionAsync(int pageIndex, int index, IFormActionHost host, CancellationToken cancellationToken) =>
        inner.RunWidgetActionAsync(pageIndex, index, host, cancellationToken);

    /// <inheritdoc/>
    public bool HasPageActions(int pageIndex) => engine.PagesHaveActions;

    /// <inheritdoc/>
    public async ValueTask<FormActionResult> RunPageOpenedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken)
    {
        engine.OpenedPages.Enqueue(pageIndex);
        engine.PageActions.Enqueue(new(pageIndex, true));
        if (engine.FormGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await inner.RunPageOpenedAsync(pageIndex, host, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public ValueTask<FormActionResult> RunPageClosedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken)
    {
        engine.PageActions.Enqueue(new(pageIndex, false));
        return inner.RunPageClosedAsync(pageIndex, host, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<FormActionResult> RunDocumentEventAsync(FormDocumentEvent documentEvent, IFormActionHost host, CancellationToken cancellationToken)
    {
        engine.DocumentEvents.Enqueue(documentEvent);
        return inner.RunDocumentEventAsync(documentEvent, host, cancellationToken);
    }
}

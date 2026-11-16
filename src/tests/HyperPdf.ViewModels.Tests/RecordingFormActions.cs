// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

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
    public IObservable<FormActionResult> RunWidgetAction(int pageIndex, int index, IFormActionHost host) => inner.RunWidgetAction(pageIndex, index, host);

    /// <inheritdoc/>
    public bool HasPageActions(int pageIndex) => engine.PagesHaveActions;

    /// <inheritdoc/>
    public IObservable<FormActionResult> RunPageOpened(int pageIndex, IFormActionHost host) =>
        Signal.Defer(() =>
        {
            engine.OpenedPages.Enqueue(pageIndex);
            engine.PageActions.Enqueue(new(pageIndex, true));
            return engine.FormGate is { } gate
                ? Signal.FromAsync(async token =>
                    {
                        await gate.Task.WaitAsync(token).ConfigureAwait(false);
                        return RxVoid.Default;
                    }).SwitchMap(_ => inner.RunPageOpened(pageIndex, host))
                : inner.RunPageOpened(pageIndex, host);
        });

    /// <inheritdoc/>
    public IObservable<FormActionResult> RunPageClosed(int pageIndex, IFormActionHost host) =>
        Signal.Defer(() =>
        {
            engine.PageActions.Enqueue(new(pageIndex, false));
            return inner.RunPageClosed(pageIndex, host);
        });

    /// <inheritdoc/>
    public IObservable<FormActionResult> RunDocumentEvent(FormDocumentEvent documentEvent, IFormActionHost host) =>
        Signal.Defer(() =>
        {
            engine.DocumentEvents.Enqueue(documentEvent);
            return inner.RunDocumentEvent(documentEvent, host);
        });
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Engine;

/// <summary>Provides IFormActions through the owning document.</summary>
internal sealed class HyperPdfFormActionsService : IFormActions
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfFormActionsService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfFormActionsService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<FormActionResult> RunWidgetAction(int pageIndex, int index, IFormActionHost host) =>
        Signal.FromAsync(token => RunWidgetActionAsync(pageIndex, index, host, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasPageActions(int pageIndex) => HyperPdfDocumentFormActions.HasPageActions(_owner, pageIndex);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<FormActionResult> RunPageOpened(int pageIndex, IFormActionHost host) =>
        Signal.FromAsync(token => RunPageOpenedAsync(pageIndex, host, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<FormActionResult> RunPageClosed(int pageIndex, IFormActionHost host) =>
        Signal.FromAsync(token => RunPageClosedAsync(pageIndex, host, token).AsTask());

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<FormActionResult> RunDocumentEvent(FormDocumentEvent documentEvent, IFormActionHost host) =>
        Signal.FromAsync(token => RunDocumentEventAsync(documentEvent, host, token).AsTask());

    /// <summary>Runs RunWidgetAction against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="index">The index.</param>
    /// <param name="host">The host.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<FormActionResult> RunWidgetActionAsync(int pageIndex, int index, IFormActionHost host, CancellationToken cancellationToken) => HyperPdfDocumentFormActions.RunWidgetActionAsync(
            _owner,
            pageIndex,
            index,
            host,
            cancellationToken);

    /// <summary>Runs RunPageOpened against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="host">The host.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<FormActionResult> RunPageOpenedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken) => HyperPdfDocumentFormActions.RunPageOpenedAsync(
            _owner,
            pageIndex,
            host,
            cancellationToken);

    /// <summary>Runs RunPageClosed against the library.</summary>
    /// <param name="pageIndex">The pageIndex.</param>
    /// <param name="host">The host.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<FormActionResult> RunPageClosedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken) => HyperPdfDocumentFormActions.RunPageClosedAsync(
            _owner,
            pageIndex,
            host,
            cancellationToken);

    /// <summary>Runs RunDocumentEvent against the library.</summary>
    /// <param name="documentEvent">The documentEvent.</param>
    /// <param name="host">The host.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The library's task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ValueTask<FormActionResult> RunDocumentEventAsync(
        FormDocumentEvent documentEvent,
        IFormActionHost host,
        CancellationToken cancellationToken) => HyperPdfDocumentFormActions.RunDocumentEventAsync(_owner, documentEvent, host, cancellationToken);
}

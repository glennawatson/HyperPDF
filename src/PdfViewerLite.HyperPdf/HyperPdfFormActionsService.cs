// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;

namespace PdfViewerLite.HyperPdf;

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
    public ValueTask<FormActionResult> RunWidgetActionAsync(int pageIndex, int index, IFormActionHost host, CancellationToken cancellationToken) => HyperPdfDocumentFormActions.RunWidgetActionAsync(
            _owner,
            pageIndex,
            index,
            host,
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<FormActionResult> RunPageOpenedAsync(int pageIndex, IFormActionHost host, CancellationToken cancellationToken) => HyperPdfDocumentFormActions.RunPageOpenedAsync(
            _owner,
            pageIndex,
            host,
            cancellationToken);
}

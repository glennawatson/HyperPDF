// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Redaction;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IDocumentRedactor through the owning document.</summary>
internal sealed class HyperPdfDocumentRedactorService : IDocumentRedactor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocumentRedactorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfDocumentRedactorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<RedactionReport> ApplyAsync(Stream destination, RedactionSettings settings, CancellationToken cancellationToken) => HyperPdfDocumentRedaction.ApplyAsync(
            _owner,
            destination,
            settings,
            cancellationToken);
}

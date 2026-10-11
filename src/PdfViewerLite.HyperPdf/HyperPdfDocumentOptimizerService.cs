// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Optimizing;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IDocumentOptimizer through the owning document.</summary>
internal sealed class HyperPdfDocumentOptimizerService : IDocumentOptimizer
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocumentOptimizerService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfDocumentOptimizerService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Task<OptimizeReport> OptimizeAsync(Stream destination, OptimizeSettings settings, IProgress<OptimizeProgress>? progress, CancellationToken cancellationToken) =>
        HyperPdfDocumentOptimization.OptimizeAsync(
            _owner,
            destination,
            settings,
            progress,
            cancellationToken);
}

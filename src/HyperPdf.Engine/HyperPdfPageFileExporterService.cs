// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Exporting;

namespace HyperPdf.Engine;

/// <summary>Provides IPageFileExporter through the owning document.</summary>
internal sealed class HyperPdfPageFileExporterService : IPageFileExporter
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageFileExporterService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageFileExporterService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<PageFileExportResult> ExportAsync(IReadOnlyList<int> pages, PageFileFormat format, PageFileExportOptions options, Stream destination, CancellationToken cancellationToken) =>
        HyperPdfDocumentPageFileExport.ExportAsync(_owner, pages, format, options, destination, cancellationToken);
}

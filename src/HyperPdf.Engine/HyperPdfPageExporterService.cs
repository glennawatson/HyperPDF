// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Printing;

namespace HyperPdf.Engine;

/// <summary>Provides IPageExporter through the owning document.</summary>
internal sealed class HyperPdfPageExporterService : IPageExporter
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageExporterService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageExporterService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ExportPages(ReadOnlySpan<int> pages, in SheetLayout layout, Stream destination) => HyperPdfDocumentPageExport.ExportPages(
            _owner,
            pages,
            in layout,
            destination);
}

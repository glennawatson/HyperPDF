// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IPageManagementSource through the owning document.</summary>
internal sealed class HyperPdfPageManagementSourceService : IPageManagementSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageManagementSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageManagementSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public IPageManager PageManager { get => HyperPdfDocumentPageManagement.GetPageManager(_owner); }
}

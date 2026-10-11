// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides IFormHighlight through the owning document.</summary>
internal sealed class HyperPdfFormHighlightService : IFormHighlight
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfFormHighlightService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfFormHighlightService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public FormHighlight Highlight { get => HyperPdfDocumentFormHighlight.GetHighlight(_owner); set => HyperPdfDocumentFormHighlight.SetHighlight(_owner, value); }
}

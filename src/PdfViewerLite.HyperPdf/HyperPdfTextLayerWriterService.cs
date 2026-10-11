// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Ocr;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides ITextLayerWriter through the owning document.</summary>
internal sealed class HyperPdfTextLayerWriterService : ITextLayerWriter
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfTextLayerWriterService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfTextLayerWriterService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddTextLayer(int pageIndex, ReadOnlySpan<OcrWord> words) => HyperPdfDocumentTextLayers.AddTextLayer(
            _owner,
            pageIndex,
            words);
}

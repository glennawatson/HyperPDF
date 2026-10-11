// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// A HyperPDF document's characters with its natively read tagged blocks, as the reading view and the page's screen
/// reader peer see them once <see cref="HyperPdfTagged.GetTaggedBlocksNative"/> replaces the PDFium copy.
/// </summary>
/// <param name="document">The document.</param>
[DebuggerDisplay("NativeTaggedSource")]
internal sealed class NativeTaggedSource(HyperPdfDocument document) : ITextLayoutSource, ITaggedStructureSource
{
    /// <inheritdoc/>
    public IDocument Document => document;

    /// <inheritdoc/>
    public void GetCharacters(int pageIndex, List<PageCharacter> output) => PdfViewerLite.HyperPdf.HyperPdfDocumentCharacterLayout.GetCharacters(document, pageIndex, output);

    /// <inheritdoc/>
    public bool GetTaggedBlocks(int pageIndex, List<TaggedBlock> output) => PdfViewerLite.HyperPdf.HyperPdfTagged.GetTaggedBlocksNative(document, pageIndex, output);
}

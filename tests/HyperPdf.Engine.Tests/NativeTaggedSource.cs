// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;

namespace HyperPdf.Engine.Tests;

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
    public void GetCharacters(int pageIndex, List<PageCharacter> output) => HyperPdf.Engine.HyperPdfDocumentCharacterLayout.GetCharacters(document, pageIndex, output);

    /// <inheritdoc/>
    public bool GetTaggedBlocks(int pageIndex, List<TaggedBlock> output) => HyperPdf.Engine.HyperPdfTagged.GetTaggedBlocksNative(document, pageIndex, output);
}

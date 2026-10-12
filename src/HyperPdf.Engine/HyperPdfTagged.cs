// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;
using HyperPdf.Core.Reading;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>Implements Tagged over the document's owned state.</summary>
internal static class HyperPdfTagged
{
    /// <summary>The minimum structure coverage for a usable reading order.</summary>
    internal const double MinimumCoverage = 0.6;

    /// <summary>The minimum marked-content coverage for a usable reading order.</summary>
    internal const double MinimumMarked = 0.5;

    /// <summary>Builds a page's reading nodes from recognised words, for a page that is only an image.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words, in page space.</param>
    /// <returns>The page's nodes, each labelled as text recognition.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="words"/> is <see langword="null"/>.</exception>
    internal static PdfSemanticPage ReadRecognisedWords(int pageIndex, IReadOnlyList<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var converted = new PdfOcrWord[words.Count];
        for (var i = 0; i < converted.Length; i++)
        {
            var (text, bounds, confidence) = words[i];
            converted[i] = new(text, new(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), confidence);
        }

        return PdfReadingStructure.FromOcrWords(pageIndex, converted);
    }

    /// <summary>
    /// Gets a page's block-level elements in logical order from the managed structure reader, matching
    /// <see cref="ITaggedStructureSource.GetTaggedBlocks"/> on the PDFium engine: the same coverage checks, kinds,
    /// levels and replacement text, with characters indexed as <see cref="ITextLayoutSource.GetCharacters"/> gives them.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="output">Receives the elements.</param>
    /// <returns><see langword="true"/> when the page's tags cover its text well enough to be trusted.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    internal static bool GetTaggedBlocksNative(HyperPdfDocument self, int pageIndex, List<TaggedBlock> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (!access.IsActive)
        {
            return false;
        }

        if (self.IsDisposed || (uint)pageIndex >= (uint)self.PageCount
            || PdfDocumentTagged.GetStructureTree(self.Document) is not { IsMarked: true })
        {
            return false;
        }

        var page = PdfReadingStructure.ReadTagged(self.Document, pageIndex);
        var content = page.Content!;
        var marked = content.MarkedCharacterCount;
        if (marked == 0 || marked < content.VisibleCharacterCount * HyperPdfTagged.MinimumMarked)
        {
            return false;
        }

        var characters = new List<PageCharacter>();
        HyperPdfDocumentCharacterLayout.GetCharacters(self, pageIndex, characters);
        var walker = new TaggedBlockWalker(TaggedCharacterMap.Create(content, characters));
        foreach (var node in page.Nodes)
        {
            walker.Visit(node);
        }

        if (walker.Covered < marked * HyperPdfTagged.MinimumCoverage || walker.Blocks.Count == 0)
        {
            return false;
        }

        output.AddRange(walker.Blocks);
        return true;
    }

    /// <summary>Reads a page's reading structure: tagged when it can be, otherwise inferred from the layout.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page's nodes.</returns>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static PdfSemanticPage ReadReadingStructure(HyperPdfDocument self, int pageIndex)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        ObjectDisposedException.ThrowIf(!access.IsActive || self.IsDisposed, self);
        return PdfReadingStructure.Read(self.Document, pageIndex);
    }
}

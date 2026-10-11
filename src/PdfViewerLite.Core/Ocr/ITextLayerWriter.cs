// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Ocr;

/// <summary>Adds an invisible, searchable text layer to pages. Safe to call from any thread.</summary>
public interface ITextLayerWriter : IDocumentFeature
{
    /// <summary>Writes recognised words onto a page as invisible text placed over where they appear.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written.</returns>
    int AddTextLayer(int pageIndex, ReadOnlySpan<OcrWord> words);
}

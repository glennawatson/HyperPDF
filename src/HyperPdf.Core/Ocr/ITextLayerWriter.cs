// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Ocr;

/// <summary>Adds an invisible, searchable text layer to pages. Safe to call from any thread.</summary>
public interface ITextLayerWriter : IDocumentFeature
{
    /// <summary>Writes recognised words onto a page as invisible text placed over where they appear.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written.</returns>
    int AddTextLayer(int pageIndex, ReadOnlySpan<OcrWord> words);

    /// <summary>
    /// Gets a token that names the page at an index and stays with that page when other pages are inserted, deleted or moved.
    /// Take it before reading a page, and write with it, so words read from one page never land on another.
    /// </summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The token, or 0 when the document has no such page.</returns>
    long GetPageToken(int pageIndex);

    /// <summary>Writes recognised words onto the page a token names, wherever that page is now.</summary>
    /// <param name="pageToken">The token from <see cref="GetPageToken"/>.</param>
    /// <param name="words">The words.</param>
    /// <returns>The number of words written; 0 when the page is no longer in the document.</returns>
    int AddTextLayerToPage(long pageToken, ReadOnlySpan<OcrWord> words);
}

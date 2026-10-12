// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Reading;

/// <summary>Reads the logical structure of a tagged document: headings, paragraphs, lists, tables and figures in order.</summary>
public interface ITaggedStructureSource : IDocumentFeature
{
    /// <summary>Gets a page's block-level elements in logical order.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="output">Receives the elements.</param>
    /// <returns>
    /// <see langword="true"/> when the page's tags cover its text well enough to be trusted; otherwise the layout is used.
    /// Text marked as an artifact (running headers, page numbers) belongs to no element and is left out.
    /// </returns>
    bool GetTaggedBlocks(int pageIndex, List<TaggedBlock> output);
}

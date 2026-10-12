// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Reading;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentTaggedStructure over the document's owned state.</summary>
internal static class HyperPdfDocumentTaggedStructure
{
    /// <summary>Gets a page's block-level elements in logical order.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="output">Receives the elements.</param>
    /// <returns>
    /// <see langword="true"/> when the page's tags cover its text well enough to be trusted; otherwise the layout is used.
    /// Text marked as an artifact (running headers, page numbers) belongs to no element and is left out.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool GetTaggedBlocks(HyperPdfDocument self, int pageIndex, List<TaggedBlock> output) => HyperPdfTagged.GetTaggedBlocksNative(self, pageIndex, output);
}

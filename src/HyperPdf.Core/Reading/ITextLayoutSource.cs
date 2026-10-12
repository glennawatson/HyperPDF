// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Reading;

/// <summary>A document that can describe where each character of a page is, for working out reading order.</summary>
public interface ITextLayoutSource : IDocumentFeature
{
    /// <summary>Appends every character of a page, in the engine's order; the index in the list is the character index.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the characters.</param>
    void GetCharacters(int pageIndex, List<PageCharacter> output);
}

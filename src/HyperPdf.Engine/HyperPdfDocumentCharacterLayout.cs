// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Reading;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentCharacterLayout over the document's owned state.</summary>
internal static class HyperPdfDocumentCharacterLayout
{
    /// <summary>Appends every character of a page, in the engine's order; the index in the list is the character index.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the characters.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetCharacters(HyperPdfDocument self, int pageIndex, List<PageCharacter> output) => HyperPdfText.GetCharactersNative(self, pageIndex, output);
}

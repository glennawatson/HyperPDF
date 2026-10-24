// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>One item of a <see cref="PdfList"/>. Its first paragraph sits beside the bullet or number; a <see cref="PdfList"/> among its blocks is a nested list.</summary>
/// <param name="Blocks">The item's blocks.</param>
[DebuggerDisplay("PdfListItem: {Blocks.Count} blocks")]
public sealed record PdfListItem(IReadOnlyList<PdfBlock> Blocks)
{
    /// <summary>Makes an item of one paragraph of plain text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The item.</returns>
    public static PdfListItem Of(string text) => new([PdfParagraph.Of(text)]);
}

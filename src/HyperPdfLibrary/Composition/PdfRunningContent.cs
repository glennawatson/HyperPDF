// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The content of a header or footer, drawn on each page. Fields such as the page number are filled in for each page.</summary>
/// <param name="Blocks">The paragraphs, tables and rules of the header or footer.</param>
[DebuggerDisplay("PdfRunningContent: {Blocks.Count} blocks")]
public sealed record PdfRunningContent(IReadOnlyList<PdfBlock> Blocks)
{
    /// <summary>Makes running content from one paragraph.</summary>
    /// <param name="paragraph">The paragraph.</param>
    /// <returns>The content.</returns>
    public static PdfRunningContent Of(PdfParagraph paragraph) => new([paragraph]);
}

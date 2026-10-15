// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A page's reading structure: its top-level nodes in reading order, and where they came from.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Origin">Where the nodes came from: tags, layout or text recognition.</param>
/// <param name="Nodes">The top-level nodes, in reading order.</param>
/// <param name="Content">What the page draws, which the nodes' glyph items index; <see langword="null"/> for text recognition.</param>
[DebuggerDisplay("PdfSemanticPage: page {PageIndex} {Origin}, {Nodes.Count} nodes")]
public sealed record PdfSemanticPage(int PageIndex, PdfNodeOrigin Origin, IReadOnlyList<PdfSemanticNode> Nodes, PdfMarkedContentPage? Content)
{
    /// <summary>Gets the pagination artifacts found on an inferred page: running headers, footers and page numbers, which are not part of the reading order.</summary>
    public IReadOnlyList<PdfSemanticNode> Artifacts { get; init; } = [];

    /// <summary>Gets a value indicating whether the page shows only images, so text recognition should read it.</summary>
    public bool NeedsTextRecognition => Content is { IsImageOnly: true } && Origin != PdfNodeOrigin.Ocr;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Features;

/// <summary>A node of the document part hierarchy (<c>/DPart</c>): a group of pages with metadata.</summary>
/// <param name="Metadata">The part's metadata (<c>/DPM</c>) as text by key.</param>
/// <param name="Children">The child parts, in order.</param>
/// <param name="StartPage">For a leaf, the zero based first page of its range, or null.</param>
/// <param name="EndPage">For a leaf, the zero based last page of its range (its start page when it has no <c>/End</c>), or null.</param>
/// <param name="Dictionary">The node dictionary.</param>
[DebuggerDisplay("PdfDocumentPartNode: {Children.Length} children")]
public sealed record PdfDocumentPartNode(IReadOnlyDictionary<string, string> Metadata, ImmutableArray<PdfDocumentPartNode> Children, int? StartPage, int? EndPage, PdfDictionary Dictionary);

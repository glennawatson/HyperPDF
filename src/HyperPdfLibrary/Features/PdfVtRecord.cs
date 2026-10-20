// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>One record of a PDF/VT file: the pages of a document part at the record level.</summary>
/// <param name="StartPage">The zero based first page, or <see langword="null"/> when the node has no page range.</param>
/// <param name="EndPage">The zero based last page, or <see langword="null"/> when the node has no page range.</param>
/// <param name="Metadata">The part's production metadata as text by key; kept as data and never run.</param>
[DebuggerDisplay("PdfVtRecord: pages {StartPage}-{EndPage}")]
public sealed record PdfVtRecord(int? StartPage, int? EndPage, IReadOnlyDictionary<string, string> Metadata);

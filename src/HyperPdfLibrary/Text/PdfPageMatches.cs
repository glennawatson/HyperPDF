// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>The matches of a search on one page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Matches">The matches, in reading order.</param>
[DebuggerDisplay("PdfPageMatches: page {PageIndex}, {Matches.Length} matches")]
public readonly record struct PdfPageMatches(int PageIndex, PdfTextMatch[] Matches);

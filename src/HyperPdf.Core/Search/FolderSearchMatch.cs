// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Search;

/// <summary>One place a folder search found the words.</summary>
/// <param name="PageIndex">The page, zero-based.</param>
/// <param name="Snippet">The words around the match, on one line.</param>
/// <param name="MatchStart">Where the match starts within <paramref name="Snippet"/>.</param>
/// <param name="MatchLength">The match's length within <paramref name="Snippet"/>.</param>
[DebuggerDisplay("FolderSearchMatch: Page {PageIndex}: {Snippet}")]
public sealed record FolderSearchMatch(int PageIndex, string Snippet, int MatchStart, int MatchLength);

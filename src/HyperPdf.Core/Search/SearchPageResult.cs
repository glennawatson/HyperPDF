// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Search;

/// <summary>The matches found on one page.</summary>
/// <param name="PageIndex">The zero based page.</param>
/// <param name="Hits">The hits, in reading order.</param>
[DebuggerDisplay("SearchPageResult: Page {PageIndex}: {Hits.Count} hits")]
public sealed record SearchPageResult(int PageIndex, IReadOnlyList<SearchHit> Hits);

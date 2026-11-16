// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Search;

/// <summary>What searching one page found, with the page's place in the search.</summary>
/// <param name="Position">How many pages into the search the page is.</param>
/// <param name="Result">The hits, or <see langword="null"/> when nothing matched or the document closed.</param>
internal sealed record SearchedPage(int Position, SearchPageResult? Result);

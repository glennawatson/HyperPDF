// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Search;

/// <summary>Puts the results of pages searched side by side back into search order, holding back a page until every page before it has reported.</summary>
internal sealed class SearchOrder
{
    /// <summary>The pages that finished before their turn.</summary>
    private readonly Dictionary<int, SearchPageResult?> _waiting = [];

    /// <summary>The position of the next page to hand on.</summary>
    private int _next;

    /// <summary>Takes the result of a page and hands on every result that is now in turn.</summary>
    /// <param name="searched">The page's position in the search and its result.</param>
    /// <returns>The results now in turn, in search order; pages with no hit leave nothing.</returns>
    internal List<SearchPageResult> Release(SearchedPage searched)
    {
        var ready = new List<SearchPageResult>();
        _waiting[searched.Position] = searched.Result;
        while (_waiting.Remove(_next, out var result))
        {
            _next++;
            if (result is not null)
            {
                ready.Add(result);
            }
        }

        return ready;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Search;

/// <summary>Searches a document page by page on the thread pool, yielding each page's results as soon as they are found.</summary>
public static class DocumentSearch
{
    /// <summary>Searches every page, starting at <paramref name="startPage"/> and wrapping around.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>Pages with at least one hit, in search order.</returns>
    public static IAsyncEnumerable<SearchPageResult> SearchAsync(IDocument document, string query, SearchOptions options, int startPage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(query);
        return SearchCoreAsync(document, query, options, startPage, cancellationToken);
    }

    /// <summary>Searches one page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">The page.</param>
    /// <returns>The result, or <see langword="null"/> when nothing matched or the document closed.</returns>
    public static SearchPageResult? SearchPage(IDocument document, string query, SearchOptions options, int page)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.IsDisposed)
        {
            return null;
        }

        var matches = new List<TextMatch>();
        document.Find(page, query, options, matches);
        if (matches.Count == 0)
        {
            return null;
        }

        var hits = new SearchHit[matches.Count];
        var bounds = new List<PageRect>();
        for (var i = 0; i < matches.Count; i++)
        {
            bounds.Clear();
            var match = matches[i];
            document.GetTextBounds(page, match.Start, match.Length, bounds);
            hits[i] = new(match, [.. bounds]);
        }

        return new(page, hits);
    }

    /// <summary>The search iterator.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>Pages with at least one hit.</returns>
    private static async IAsyncEnumerable<SearchPageResult> SearchCoreAsync(
        IDocument document,
        string query,
        SearchOptions options,
        int startPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (query.Length == 0 || document.PageCount == 0)
        {
            yield break;
        }

        var pageCount = document.PageCount;
        startPage = Math.Clamp(startPage, 0, pageCount - 1);
        for (var n = 0; n < pageCount; n++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = (startPage + n) % pageCount;
            await document.PreparePageAsync(page, cancellationToken).ConfigureAwait(false);
            var result = await Task.Run(() => SearchPage(document, query, options, page), cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                yield return result;
            }
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Search;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Core.Tests.Search;

/// <summary>Tests for <see cref="DocumentSearch"/>.</summary>
public sealed class DocumentSearchTests
{
    /// <summary>Verifies every page is searched starting from the requested page and wrapping.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchesFromStartPageAndWraps()
    {
        const int startPage = 2;
        const int matchesPerPage = 2;
        var document = new FakeDocument("a.pdf", FakeEngine.A4, FakeEngine.A4, FakeEngine.A4);
        var pages = new List<int>();
        var total = 0;

        await foreach (var result in DocumentSearch.SearchAsync(document, "ALPHA", SearchOptions.None, startPage, CancellationToken.None))
        {
            pages.Add(result.PageIndex);
            total += result.Hits.Count;
        }

        await Assert.That(pages).IsEquivalentTo([startPage, 0, 1]);
        await Assert.That(pages[0]).IsEqualTo(startPage);
        await Assert.That(total).IsEqualTo(document.PageCount * matchesPerPage);
    }

    /// <summary>Verifies case sensitive searches that match nothing yield nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchCaseFiltersResults()
    {
        var document = new FakeDocument("a.pdf", FakeEngine.A4);
        var count = 0;

        await foreach (var unused in DocumentSearch.SearchAsync(document, "ALPHA", SearchOptions.MatchCase, 0, CancellationToken.None))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }
}

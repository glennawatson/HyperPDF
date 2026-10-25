// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Printing;

namespace HyperPdf.Core.Tests.Printing;

/// <summary>Tests for <see cref="PageRanges"/>.</summary>
public sealed class PageRangesTests
{
    /// <summary>The pages in the imagined document.</summary>
    private const int PageCount = 12;

    /// <summary>Verifies pages, ranges and open-ended ranges, sorted with each page once.</summary>
    /// <param name="text">The ranges.</param>
    /// <param name="expected">The expected zero based pages, comma separated.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("3", "2")]
    [Arguments("1-3, 7", "0,1,2,6")]
    [Arguments("10-", "9,10,11")]
    [Arguments("5;2 2-3", "1,2,4")]
    [Arguments(" 12 , 12 ", "11")]
    public async Task ReadsRanges(string text, string expected)
    {
        List<int> pages = [];
        var valid = PageRanges.TryParse(text, PageCount, pages);

        await Assert.That(valid).IsTrue();
        await Assert.That(string.Join(',', pages)).IsEqualTo(expected);
    }

    /// <summary>Verifies pages outside the document, backwards ranges and nonsense are refused.</summary>
    /// <param name="text">The ranges.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("0")]
    [Arguments("13")]
    [Arguments("4-2")]
    [Arguments("two")]
    [Arguments("")]
    [Arguments("1-3, x")]
    public async Task RefusesInvalidRanges(string text)
    {
        List<int> pages = [];
        var valid = PageRanges.TryParse(text, PageCount, pages);

        await Assert.That(valid).IsFalse();
        await Assert.That(pages.Count).IsEqualTo(0);
    }
}

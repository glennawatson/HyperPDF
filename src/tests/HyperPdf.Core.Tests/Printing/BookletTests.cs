// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Printing;

namespace HyperPdf.Core.Tests.Printing;

/// <summary>Checks booklet page order: folded, the printed sheets read in order, with blanks at the end.</summary>
public sealed class BookletTests
{
    /// <summary>A booklet that fills its sheets exactly.</summary>
    private const int Eight = 8;

    /// <summary>A booklet that needs blank pages.</summary>
    private const int Five = 5;

    /// <summary>The sheets eight pages need.</summary>
    private const int TwoSheets = 2;

    /// <summary>Pages on a sheet.</summary>
    private const int PagesPerSheet = 4;

    /// <summary>The printing order of eight pages.</summary>
    private static readonly int[] EightPageOrder = [7, 0, 1, 6, 5, 2, 3, 4];

    /// <summary>The printing order of five pages, with blanks.</summary>
    private static readonly int[] FivePageOrder = [-1, 0, 1, -1, -1, 2, 3, 4];

    /// <summary>Eight pages fill two sheets: the outer sheet carries the first, second, second-last and last pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdersEightPages()
    {
        var order = new List<int>();

        Booklet.Arrange(Eight, order);

        await Assert.That(order).IsEquivalentTo(EightPageOrder);
        await Assert.That(Booklet.SheetCount(Eight)).IsEqualTo(TwoSheets);
    }

    /// <summary>Five pages are padded to eight with blanks, which fall at the back of the booklet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PadsWithBlanksAtTheEnd()
    {
        var order = new List<int>();

        Booklet.Arrange(Five, order);

        await Assert.That(order).IsEquivalentTo(FivePageOrder);
    }

    /// <summary>Every page appears exactly once, whatever the count.</summary>
    /// <param name="pages">The page count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(3)]
    [Arguments(12)]
    [Arguments(13)]
    public async Task PlacesEveryPageOnce(int pages)
    {
        var order = new List<int>();

        Booklet.Arrange(pages, order);

        await Assert.That(order.Count % PagesPerSheet).IsEqualTo(0);
        await Assert.That(order.Where(static p => p >= 0).Order().ToArray()).IsEquivalentTo(Enumerable.Range(0, pages).ToArray());
    }
}

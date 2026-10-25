// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Core.Tests.Layout;

/// <summary>Checks the layout of two page spreads that read right to left.</summary>
public sealed class DocumentLayoutRightToLeftTests
{
    /// <summary>The page width used in tests.</summary>
    private const float PageWidth = 100F;

    /// <summary>The page height used in tests.</summary>
    private const float PageHeight = 200F;

    /// <summary>The gap between pages.</summary>
    private const double Spacing = 10;

    /// <summary>The margin around the pages.</summary>
    private const double Margin = 20;

    /// <summary>The viewport width.</summary>
    private const double Viewport = 500;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 5;

    /// <summary>The second page, the other half of the first spread.</summary>
    private const int SecondPage = 1;

    /// <summary>The left edge of a two page spread centred in the viewport.</summary>
    private const double SpreadLeft = (Viewport - ((2 * PageWidth) + Spacing)) / 2;

    /// <summary>Verifies that the first page of a spread sits on the right and the second on the left.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dual_PutsTheFirstPageOnTheRight()
    {
        var layout = Create(PageLayoutMode.Dual, true);

        await Assert.That(layout.GetPageBounds(SecondPage).X).IsEqualTo(SpreadLeft);
        await Assert.That(layout.GetPageBounds(0).X).IsEqualTo(SpreadLeft + PageWidth + Spacing);
    }

    /// <summary>Verifies that left to right layout is unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Dual_LeftToRight_KeepsTheFirstPageOnTheLeft()
    {
        var layout = Create(PageLayoutMode.Dual, false);

        await Assert.That(layout.GetPageBounds(0).X).IsEqualTo(SpreadLeft);
        await Assert.That(layout.GetPageBounds(SecondPage).X).IsEqualTo(SpreadLeft + PageWidth + Spacing);
    }

    /// <summary>Verifies that a cover page sits in the left slot when the spread reads right to left.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DualCover_PutsTheCoverOnTheLeft()
    {
        var layout = Create(PageLayoutMode.DualCover, true);

        await Assert.That(layout.GetPageBounds(0).X).IsEqualTo(SpreadLeft);
        await Assert.That(layout.GetPageBounds(SecondPage).X).IsGreaterThan(layout.GetPageBounds(SecondPage + 1).X);
    }

    /// <summary>Verifies that mirroring a row changes no page's size or height.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Mirroring_KeepsEveryPageTheSameSize()
    {
        var mirrored = Create(PageLayoutMode.Dual, true);
        var plain = Create(PageLayoutMode.Dual, false);

        for (var i = 0; i < Pages; i++)
        {
            await Assert.That(mirrored.GetPageBounds(i).Width).IsEqualTo(plain.GetPageBounds(i).Width);
            await Assert.That(mirrored.GetPageBounds(i).Y).IsEqualTo(plain.GetPageBounds(i).Y);
        }
    }

    /// <summary>Creates a layout of equal pages.</summary>
    /// <param name="mode">The arrangement.</param>
    /// <param name="rightToLeft">Whether spreads read right to left.</param>
    /// <returns>The layout.</returns>
    private static DocumentLayout Create(PageLayoutMode mode, bool rightToLeft)
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        return DocumentLayout.Create(sizes, new(PageRotation.None, mode, 1, Spacing, Margin, Viewport) { RightToLeft = rightToLeft });
    }
}

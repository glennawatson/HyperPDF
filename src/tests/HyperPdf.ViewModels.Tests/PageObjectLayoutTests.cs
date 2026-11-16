// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Markup;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks how objects are lined up and spread.</summary>
public sealed class PageObjectLayoutTests
{
    /// <summary>The left edge of the first box.</summary>
    private const float FirstLeft = 10;

    /// <summary>The left edge of the second box.</summary>
    private const float SecondLeft = 50;

    /// <summary>The left edge of the third box.</summary>
    private const float ThirdLeft = 200;

    /// <summary>The side of each box.</summary>
    private const float Side = 20;

    /// <summary>The count of boxes.</summary>
    private const int Count = 3;

    /// <summary>How far two gaps may differ, in points, for float rounding.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>The top of the first box.</summary>
    private const float Top = 5;

    /// <summary>Moves every box to the left edge of the leftmost one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlignsOnTheLeftEdge()
    {
        PageRect[] boxes = [new(FirstLeft, Top, Side, Side), new(SecondLeft, Top, Side, Side), new(ThirdLeft, Top, Side, Side)];
        var moves = new PagePoint[Count];

        PageObjectLayout.Align(boxes, PageObjectAlignment.Left, moves);

        using (Assert.Multiple())
        {
            await Assert.That(moves[0].X).IsEqualTo(0F);
            await Assert.That(moves[1].X).IsEqualTo(FirstLeft - SecondLeft);
            await Assert.That(moves[2].X).IsEqualTo(FirstLeft - ThirdLeft);
            await Assert.That(moves[2].Y).IsEqualTo(0F);
        }
    }

    /// <summary>Spreads three boxes so the gaps between them match, leaving the outer two where they are.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpreadsWithEqualGaps()
    {
        PageRect[] boxes = [new(FirstLeft, Top, Side, Side), new(SecondLeft, Top, Side, Side), new(ThirdLeft, Top, Side, Side)];
        var moves = new PagePoint[Count];

        PageObjectLayout.Spread(boxes, true, moves);
        var middleLeft = boxes[1].Left + moves[1].X;
        var firstGap = middleLeft - boxes[0].Right;
        var secondGap = boxes[2].Left - (middleLeft + Side);

        using (Assert.Multiple())
        {
            await Assert.That(moves[0].X).IsEqualTo(0F);
            await Assert.That(moves[2].X).IsEqualTo(0F);
            await Assert.That(firstGap).IsEqualTo(secondGap).Within(Tolerance);
        }
    }
}

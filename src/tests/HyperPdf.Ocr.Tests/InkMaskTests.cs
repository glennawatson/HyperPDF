// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Layout;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the separation of ink from paper.</summary>
public sealed class InkMaskTests
{
    /// <summary>The width of the test page.</summary>
    private const int PageWidth = 30;

    /// <summary>The height of the test page.</summary>
    private const int PageHeight = 20;

    /// <summary>The one dark box on the test page.</summary>
    private static readonly Region Mark = new(5, 7, 6, 4);

    /// <summary>Dark marks on a light page are the ink.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DarkOnLightIsInk()
    {
        var mask = InkMask.Build(Page().Pixels, PageWidth, PageHeight, new());

        await Assert.That(mask.InkIsDark).IsTrue();
        await Assert.That(mask.Pixels.ToArray().Sum(static pixel => pixel)).IsEqualTo(Mark.Pixels);
        await Assert.That(mask.TryGetExtent(new(0, 0, PageWidth - 1, PageHeight - 1), out var extent)).IsTrue();
        await Assert.That(extent.Left).IsEqualTo(Mark.Left);
        await Assert.That(extent.Top).IsEqualTo(Mark.Top);
        await Assert.That(extent.Right).IsEqualTo(Mark.Right);
        await Assert.That(extent.Bottom).IsEqualTo(Mark.Bottom);
    }

    /// <summary>Light marks on a dark page are the ink, as the marks are the smaller side.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LightOnDarkIsInk()
    {
        var mask = InkMask.Build(Page().Invert().Pixels, PageWidth, PageHeight, new());

        await Assert.That(mask.InkIsDark).IsFalse();
        await Assert.That(mask.Pixels.ToArray().Sum(static pixel => pixel)).IsEqualTo(Mark.Pixels);
    }

    /// <summary>An area with no ink has no extent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlankAreaHasNoExtent()
    {
        var mask = InkMask.Build(Page().Pixels, PageWidth, PageHeight, new());

        await Assert.That(mask.TryGetExtent(new(0, 0, Mark.Left - 1, PageHeight - 1), out _)).IsFalse();
    }

    /// <summary>Makes the test page: one dark box.</summary>
    /// <returns>The page.</returns>
    private static GreyPage Page() => new GreyPage(PageWidth, PageHeight).Ink(Mark);
}

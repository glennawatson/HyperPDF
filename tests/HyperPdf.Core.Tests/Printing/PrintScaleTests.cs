// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Printing;

namespace HyperPdf.Core.Tests.Printing;

/// <summary>Tests for <see cref="PrintScale"/>, which sizes a page on the paper.</summary>
public sealed class PrintScaleTests
{
    /// <summary>A Letter page's width in points.</summary>
    private const float PageWidth = 612;

    /// <summary>A Letter page's height in points.</summary>
    private const float PageHeight = 792;

    /// <summary>Room twice the page's size.</summary>
    private const float Roomy = 2;

    /// <summary>Room half the page's size.</summary>
    private const float Cramped = 0.5F;

    /// <summary>A custom percentage below the smallest offered.</summary>
    private const int TooSmall = 1;

    /// <summary>A custom percentage above the largest offered.</summary>
    private const int TooLarge = 1000;

    /// <summary>A custom scale of three quarters.</summary>
    private const int ThreeQuarters = 75;

    /// <summary>Each choice gives the promised scale when the paper has room to spare.</summary>
    /// <param name="scaling">The choice.</param>
    /// <param name="expected">The scale.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintScaling.FitToPaper, Roomy)]
    [Arguments(PrintScaling.ActualSize, 1F)]
    [Arguments(PrintScaling.ShrinkOversized, 1F)]
    [Arguments(PrintScaling.Custom, 0.75F)]
    public async Task ScalesOnRoomyPaper(PrintScaling scaling, float expected) =>
        await Assert.That(PrintScale.For(scaling, ThreeQuarters, PageWidth, PageHeight, PageWidth * Roomy, PageHeight * Roomy)).IsEqualTo(expected);

    /// <summary>Fitting and shrinking both make an oversized page fit; actual size keeps it and lets the edges go.</summary>
    /// <param name="scaling">The choice.</param>
    /// <param name="expected">The scale.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintScaling.FitToPaper, Cramped)]
    [Arguments(PrintScaling.ShrinkOversized, Cramped)]
    [Arguments(PrintScaling.ActualSize, 1F)]
    public async Task ScalesOnSmallPaper(PrintScaling scaling, float expected) =>
        await Assert.That(PrintScale.For(scaling, PrintScale.TrueSize, PageWidth, PageHeight, PageWidth * Cramped, PageHeight * Cramped)).IsEqualTo(expected);

    /// <summary>A typed percentage stays within the offered range, and an empty page prints at true size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsCustomScaleInRange()
    {
        await Assert.That(PrintScale.ClampPercent(TooSmall)).IsEqualTo(PrintScale.MinPercent);
        await Assert.That(PrintScale.ClampPercent(TooLarge)).IsEqualTo(PrintScale.MaxPercent);
        await Assert.That(PrintScale.For(PrintScaling.FitToPaper, PrintScale.TrueSize, 0, PageHeight, PageWidth, PageHeight)).IsEqualTo(1F);
    }

    /// <summary>Every paper size has a portrait size, turned sideways on request.</summary>
    /// <param name="paper">The paper.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.A4)]
    [Arguments(PaperSize.Letter)]
    [Arguments(PaperSize.A3)]
    [Arguments(PaperSize.A5)]
    [Arguments(PaperSize.Legal)]
    [Arguments(PaperSize.Tabloid)]
    public async Task SizesEveryPaper(PaperSize paper)
    {
        SheetGrid.GetSheetSize(paper, false, out var width, out var height);
        SheetGrid.GetSheetSize(paper, true, out var sideWidth, out var sideHeight);

        await Assert.That(width).IsLessThan(height);
        await Assert.That(sideWidth).IsEqualTo(height);
        await Assert.That(sideHeight).IsEqualTo(width);
    }
}

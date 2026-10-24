// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Layout;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the finding of ink on a page.</summary>
public sealed class RunLabellerTests
{
    /// <summary>The width of the test page.</summary>
    private const int PageWidth = 40;

    /// <summary>The height of the test page.</summary>
    private const int PageHeight = 20;

    /// <summary>The number of separate boxes drawn in the first test.</summary>
    private const int SeparateBlobCount = 2;

    /// <summary>The first of two separate boxes.</summary>
    private static readonly Region FirstBox = new(2, 3, 5, 4);

    /// <summary>The second of two separate boxes.</summary>
    private static readonly Region SecondBox = new(20, 10, 3, 6);

    /// <summary>A box that touches <see cref="DiagonalSecond"/> only at a corner.</summary>
    private static readonly Region DiagonalFirst = new(2, 2, 3, 3);

    /// <summary>A box that touches <see cref="DiagonalFirst"/> only at a corner.</summary>
    private static readonly Region DiagonalSecond = new(5, 5, 3, 3);

    /// <summary>The left arm of a U shape.</summary>
    private static readonly Region LeftArm = new(2, 2, 2, 8);

    /// <summary>The right arm of a U shape.</summary>
    private static readonly Region RightArm = new(8, 2, 2, 8);

    /// <summary>The base joining the arms of a U shape.</summary>
    private static readonly Region Base = new(2, 10, 8, 2);

    /// <summary>Separate boxes of ink are separate blobs, each with its own box and pixel count.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparateBoxesAreSeparateBlobs()
    {
        var blobs = Label(new GreyPage(PageWidth, PageHeight).Ink(FirstBox).Ink(SecondBox));

        await Assert.That(blobs.Count).IsEqualTo(SeparateBlobCount);
        await AssertMatches(blobs[0], FirstBox);
        await AssertMatches(blobs[1], SecondBox);
    }

    /// <summary>Boxes that touch only at a corner are one blob.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CornersJoinBlobs()
    {
        var blobs = Label(new GreyPage(PageWidth, PageHeight).Ink(DiagonalFirst).Ink(DiagonalSecond));

        await Assert.That(blobs.Count).IsEqualTo(1);
        await Assert.That(blobs[0].Left).IsEqualTo(DiagonalFirst.Left);
        await Assert.That(blobs[0].Top).IsEqualTo(DiagonalFirst.Top);
        await Assert.That(blobs[0].Right).IsEqualTo(DiagonalSecond.Right);
        await Assert.That(blobs[0].Bottom).IsEqualTo(DiagonalSecond.Bottom);
        await Assert.That(blobs[0].Pixels).IsEqualTo(DiagonalFirst.Pixels + DiagonalSecond.Pixels);
    }

    /// <summary>A shape made of two arms that meet lower down is one blob, however the arms were first seen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArmsThatMeetLaterAreOneBlob()
    {
        var blobs = Label(new GreyPage(PageWidth, PageHeight).Ink(LeftArm).Ink(RightArm).Ink(Base));

        await Assert.That(blobs.Count).IsEqualTo(1);
        await Assert.That(blobs[0].Left).IsEqualTo(LeftArm.Left);
        await Assert.That(blobs[0].Right).IsEqualTo(RightArm.Right);
        await Assert.That(blobs[0].Top).IsEqualTo(LeftArm.Top);
        await Assert.That(blobs[0].Bottom).IsEqualTo(Base.Bottom);
        await Assert.That(blobs[0].Pixels).IsEqualTo(LeftArm.Pixels + RightArm.Pixels + Base.Pixels);
    }

    /// <summary>Finds the blobs of ink on a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The blobs.</returns>
    private static List<ComponentBox> Label(GreyPage page)
    {
        var blobs = new List<ComponentBox>();
        var scratch = new RecognitionScratch();
        RunLabeller.Label(InkMask.Build(page.Pixels, page.Width, page.Height, scratch), blobs, scratch);
        return blobs;
    }

    /// <summary>Checks a blob is exactly the box that was drawn.</summary>
    /// <param name="blob">The blob.</param>
    /// <param name="region">The box.</param>
    /// <returns>A task.</returns>
    private static async Task AssertMatches(ComponentBox blob, Region region)
    {
        await Assert.That(blob.Left).IsEqualTo(region.Left);
        await Assert.That(blob.Top).IsEqualTo(region.Top);
        await Assert.That(blob.Right).IsEqualTo(region.Right);
        await Assert.That(blob.Bottom).IsEqualTo(region.Bottom);
        await Assert.That(blob.Pixels).IsEqualTo(region.Pixels);
    }
}

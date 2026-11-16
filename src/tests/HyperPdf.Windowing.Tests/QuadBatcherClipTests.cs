// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that the quads a GPU backend draws keep the clip they were added under, and that clips split the runs.</summary>
public sealed class QuadBatcherClipTests
{
    /// <summary>The colour of every quad.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>An opaque red.</summary>
    private const uint OpaqueRed = 0xFFFF0000;

    /// <summary>How much the faded quads count.</summary>
    private const double HalfOpacity = 0.5;

    /// <summary>The vertex colour of white at alpha 128, premultiplied: 128 in every channel.</summary>
    private const uint HalfWhiteVertex = 0x80808080;

    /// <summary>The vertex colour of red at alpha 128, premultiplied, with red in the low byte.</summary>
    private const uint HalfRedVertex = 0x80000080;

    /// <summary>The size of a quad and of a clip, in pixels.</summary>
    private const int Size = 10;

    /// <summary>The number of runs made by a quad before a clip, one inside it, and one after it.</summary>
    private const int ThreeRuns = 3;

    /// <summary>The frame size the clip is made whole pixels inside.</summary>
    private const int Frame = 100;

    /// <summary>The left edge of a clip that starts between two pixels.</summary>
    private const double HalfLeft = 1.5;

    /// <summary>The top edge of a clip that starts between two pixels.</summary>
    private const double HalfTop = 2.5;

    /// <summary>The width and height of that clip.</summary>
    private const double Span = 3;

    /// <summary>The whole pixels that clip touches: the first column, first row, one past the last column and one past the last row.</summary>
    private const int WholeLeft = 1;

    /// <summary>The first row the clip touches.</summary>
    private const int WholeTop = 2;

    /// <summary>One past the last column the clip touches.</summary>
    private const int WholeRight = 5;

    /// <summary>One past the last row the clip touches.</summary>
    private const int WholeBottom = 6;

    /// <summary>How far a clip reaches past the corner of the frame.</summary>
    private const int Over = 2;

    /// <summary>Quads on the same tile join into one run, until a clip starts or ends, which starts another run each time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipSplitsTheRuns()
    {
        var batcher = new QuadBatcher<string>();
        LayoutRect clip = new(1, WholeTop, Size, Size);
        batcher.AddQuad(null, smooth: false, new(0, 0, Size, Size), White);
        batcher.AddQuad(null, smooth: false, new(Size, 0, Size, Size), White);
        batcher.SetClip(clip);
        batcher.AddQuad(null, smooth: false, new(0, 0, Size, Size), White);
        batcher.ClearClip();
        batcher.AddQuad(null, smooth: false, new(0, 0, Size, Size), White);

        var runs = batcher.Batches.ToArray();

        await Assert.That(runs.Length).IsEqualTo(ThreeRuns);
        await Assert.That(runs[0].Clip).IsNull();
        await Assert.That(runs[1].Clip).IsEqualTo(clip);
        await Assert.That(runs[ThreeRuns - 1].Clip).IsNull();
    }

    /// <summary>A new frame starts without the clip the last one ended under.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipDoesNotLastIntoTheNextFrame()
    {
        var batcher = new QuadBatcher<string>();
        batcher.SetClip(new(0, 0, Size, Size));
        batcher.AddQuad(null, smooth: false, new(0, 0, Size, Size), White);
        batcher.Reset();
        batcher.AddQuad(null, smooth: false, new(0, 0, Size, Size), White);

        await Assert.That(batcher.Batches[0].Clip).IsNull();
    }

    /// <summary>A tile added under a lower opacity gets white at that alpha, a fill gets its own alpha lowered, and the next frame starts opaque.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityFadesTilesAndFillsAndEndsWithTheFrame()
    {
        var batcher = new QuadBatcher<string>();
        batcher.SetOpacity(HalfOpacity);
        batcher.AddQuad("tile", smooth: false, new(0, 0, Size, Size), White);
        batcher.AddQuad(null, smooth: false, new(0, 0, Size, Size), OpaqueRed);
        var tile = batcher.Vertices[0].Color;
        var fill = batcher.Vertices[ThreeRuns + ThreeRuns].Color;
        batcher.Reset();
        batcher.AddQuad("tile", smooth: false, new(0, 0, Size, Size), White);

        // Premultiplied, a white tile at half alpha is half of every channel; red is in the low byte of the vertex colour.
        await Assert.That(tile).IsEqualTo(HalfWhiteVertex);
        await Assert.That(fill).IsEqualTo(HalfRedVertex);
        await Assert.That(batcher.Vertices[0].Color).IsEqualTo(White);
    }

    /// <summary>A clip becomes the whole pixels it touches, kept inside the frame, and one outside the frame is empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipBecomesWholePixelsInsideTheFrame()
    {
        var partial = PixelClip.Of(new(HalfLeft, HalfTop, Span, Span), Frame, Frame);
        var overhang = PixelClip.Of(new(Frame - Over, Frame - Over, Size, Size), Frame, Frame);
        var outside = PixelClip.Of(new(Frame + Size, 0, Size, Size), Frame, Frame);
        PixelClip expectedPartial = new(WholeLeft, WholeTop, WholeRight, WholeBottom);
        PixelClip expectedOverhang = new(Frame - Over, Frame - Over, Frame, Frame);

        await Assert.That(partial).IsEqualTo(expectedPartial);
        await Assert.That(overhang).IsEqualTo(expectedOverhang);
        await Assert.That(outside.Width).IsEqualTo(0);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that a frame whose clear waits for the page paper that follows it is the frame painting the clear at once gives, for
/// paper inside the frame, paper that reaches past its edges, paper that is too small to wait for, and draws over the paper.
/// </summary>
public sealed class CpuClearNarrowTests
{
    /// <summary>The frame's width in pixels.</summary>
    private const int Width = 200;

    /// <summary>The frame's height in pixels.</summary>
    private const int Height = 160;

    /// <summary>The opaque colour the frame is cleared to.</summary>
    private const uint Canvas = 0xFF303A44;

    /// <summary>The opaque colour of the paper.</summary>
    private const uint Paper = 0xFFF8F8F0;

    /// <summary>The see-through colour drawn over the paper.</summary>
    private const uint Tint = 0x50A04020;

    /// <summary>The opaque colour of a bar drawn over the frame.</summary>
    private const uint Bar = 0xFF405060;

    /// <summary>The height of the bar drawn across the top of the frame.</summary>
    private const int BarHeight = 12;

    /// <summary>The left and top of paper well inside the frame.</summary>
    private const int InsideLeft = 24;

    /// <summary>The top of paper well inside the frame.</summary>
    private const int InsideTop = 20;

    /// <summary>The width of paper well inside the frame.</summary>
    private const int InsideWidth = 140;

    /// <summary>The height of paper well inside the frame.</summary>
    private const int InsideHeight = 120;

    /// <summary>How far paper reaches past the left edge of the frame.</summary>
    private const int PastLeft = -30;

    /// <summary>How far paper reaches past the top edge of the frame.</summary>
    private const int PastTop = -20;

    /// <summary>The width of paper reaching past the top left.</summary>
    private const int PastTopLeftWidth = 190;

    /// <summary>The height of paper reaching past the top left.</summary>
    private const int PastTopLeftHeight = 150;

    /// <summary>The left of paper reaching past the bottom right.</summary>
    private const int PastRightLeft = 60;

    /// <summary>The top of paper reaching past the bottom right.</summary>
    private const int PastBottomTop = 40;

    /// <summary>The width of paper reaching past the bottom right.</summary>
    private const int PastBottomRightWidth = 200;

    /// <summary>The height of paper reaching past the bottom right.</summary>
    private const int PastBottomRightHeight = 180;

    /// <summary>How far paper that covers everything reaches past every edge.</summary>
    private const int Reach = 10;

    /// <summary>Where paper that is too small to wait for starts, across and down.</summary>
    private const int SmallAt = 40;

    /// <summary>The width and height of paper that is too small to wait for.</summary>
    private const int SmallSize = 60;

    /// <summary>How far past the frame the paper outside it starts.</summary>
    private const int Beyond = 20;

    /// <summary>The width and height of the paper outside the frame.</summary>
    private const int OutsideSize = 90;

    /// <summary>Where the tint over the paper starts, across and down.</summary>
    private const int TintAt = 30;

    /// <summary>The width of the tint over the paper.</summary>
    private const int TintWidth = 90;

    /// <summary>The height of the tint over the paper.</summary>
    private const int TintHeight = 70;

    /// <summary>Where the paper of the clear-twice test starts, across and down.</summary>
    private const int PageAt = 20;

    /// <summary>The width and height of the paper of the clear-twice test.</summary>
    private const int PageSize = 100;

    /// <summary>Where the paper is, as left, top, width and height.</summary>
    public enum PaperPlace
    {
        /// <summary>Well inside the frame, with canvas on every side.</summary>
        Inside = 0,

        /// <summary>Reaching past the left and top edges of the frame.</summary>
        PastTopLeft = 1,

        /// <summary>Reaching past the right and bottom edges of the frame.</summary>
        PastBottomRight = 2,

        /// <summary>Covering the whole frame.</summary>
        Everything = 3,

        /// <summary>Too small to wait for.</summary>
        Small = 4,

        /// <summary>Outside the frame, so it covers none of it.</summary>
        Outside = 5,
    }

    /// <summary>A frame cleared and then covered by paper is the frame that paints the clear before the paper.</summary>
    /// <param name="place">Where the paper is.</param>
    /// <param name="overlays">Whether a see-through tint and an opaque bar are drawn over the paper afterwards.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperPlace.Inside, false)]
    [Arguments(PaperPlace.Inside, true)]
    [Arguments(PaperPlace.PastTopLeft, true)]
    [Arguments(PaperPlace.PastBottomRight, true)]
    [Arguments(PaperPlace.Everything, true)]
    [Arguments(PaperPlace.Small, true)]
    [Arguments(PaperPlace.Outside, true)]
    public async Task ClearThenPaperShowsTheSameAsAClearPaintedAtOnce(PaperPlace place, bool overlays)
    {
        using var waiting = NewBackend();
        using var immediate = NewBackend();

        using (var pass = waiting.BeginFrame())
        {
            pass.Clear(Canvas);
            Draw(pass, place, overlays);
        }

        using (var pass = immediate.BeginFrame())
        {
            pass.Clear(Canvas);

            // A fill with nothing in it still makes the clear painted before whatever comes next.
            pass.FillRect(new(0, 0, 1, 1), 0);
            Draw(pass, place, overlays);
        }

        await Assert.That(CountDifferences(waiting.FramePixels, immediate.FramePixels)).IsEqualTo(0);
    }

    /// <summary>A frame cleared and not drawn on is all the colour it was cleared to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearAloneFillsTheFrame()
    {
        using var waiting = NewBackend();
        using var immediate = NewBackend();

        using (var pass = waiting.BeginFrame())
        {
            pass.Clear(Canvas);
        }

        using (var pass = immediate.BeginFrame())
        {
            pass.Clear(Canvas);
            pass.FillRect(new(0, 0, 1, 1), 0);
        }

        await Assert.That(CountDifferences(waiting.FramePixels, immediate.FramePixels)).IsEqualTo(0);
    }

    /// <summary>A second clear in a frame replaces the first, whatever was drawn between them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ASecondClearReplacesTheFirst()
    {
        using var waiting = NewBackend();
        using var immediate = NewBackend();

        using (var pass = waiting.BeginFrame())
        {
            pass.Clear(Canvas);
            pass.FillRect(new(PageAt, PageAt, PageSize, PageSize), Paper);
            pass.Clear(Bar);
        }

        using (var pass = immediate.BeginFrame())
        {
            pass.Clear(Canvas);
            pass.FillRect(new(0, 0, 1, 1), 0);
            pass.FillRect(new(PageAt, PageAt, PageSize, PageSize), Paper);
            pass.FillRect(new(0, 0, 1, 1), 0);
            pass.Clear(Bar);
            pass.FillRect(new(0, 0, 1, 1), 0);
        }

        await Assert.That(CountDifferences(waiting.FramePixels, immediate.FramePixels)).IsEqualTo(0);
    }

    /// <summary>Draws the paper and, when asked, what lies over it.</summary>
    /// <param name="pass">The frame's pass.</param>
    /// <param name="place">Where the paper is.</param>
    /// <param name="overlays">Whether a tint and a bar are drawn over it.</param>
    private static void Draw(IFramePass pass, PaperPlace place, bool overlays)
    {
        pass.FillRect(PaperArea(place), Paper);
        if (!overlays)
        {
            return;
        }

        pass.FillRect(new(TintAt, TintAt, TintWidth, TintHeight), Tint);
        pass.FillRect(new(0, 0, Width, BarHeight), Bar);
    }

    /// <summary>Gets the rectangle of the paper.</summary>
    /// <param name="place">Where the paper is.</param>
    /// <returns>The rectangle.</returns>
    private static LayoutRect PaperArea(PaperPlace place) => place switch
    {
        PaperPlace.Inside => new(InsideLeft, InsideTop, InsideWidth, InsideHeight),
        PaperPlace.PastTopLeft => new(PastLeft, PastTop, PastTopLeftWidth, PastTopLeftHeight),
        PaperPlace.PastBottomRight => new(PastRightLeft, PastBottomTop, PastBottomRightWidth, PastBottomRightHeight),
        PaperPlace.Everything => new(-Reach, -Reach, Width + Reach + Reach, Height + Reach + Reach),
        PaperPlace.Small => new(SmallAt, SmallAt, SmallSize, SmallSize),
        _ => new(Width + Beyond, Height + Beyond, OutsideSize, OutsideSize),
    };

    /// <summary>Makes a backend with a frame.</summary>
    /// <returns>The backend.</returns>
    private static CpuRenderBackend NewBackend()
    {
        var backend = new CpuRenderBackend(static (_, _, _) => { });
        backend.Resize(new(Width, Height));
        return backend;
    }

    /// <summary>Counts the bytes that differ between two frames.</summary>
    /// <param name="first">One frame.</param>
    /// <param name="second">The other.</param>
    /// <returns>The number of bytes that differ, counting a difference in length as every byte.</returns>
    private static int CountDifferences(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (first.Length != second.Length)
        {
            return Math.Max(first.Length, second.Length);
        }

        var differences = 0;
        for (var i = 0; i < first.Length; i++)
        {
            if (first[i] != second[i])
            {
                differences++;
            }
        }

        return differences;
    }
}

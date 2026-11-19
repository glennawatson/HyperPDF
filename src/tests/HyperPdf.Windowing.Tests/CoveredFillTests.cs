// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Cpu;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that holding back a large opaque fill for the pictures drawn over it changes nothing that is seen: every pixel is the
/// picture where a picture is, the fill where only the fill is, and the cleared frame elsewhere, whether the pictures cover the
/// fill exactly, leave a gap, overlap, reach past it or are cut by a clip.
/// </summary>
public sealed class CoveredFillTests
{
    /// <summary>The frame's width and height in pixels.</summary>
    private const int FrameSize = 256;

    /// <summary>The width and height of each picture, in pixels.</summary>
    private const int TileSize = 64;

    /// <summary>Where the fill starts, across and down: off the grid of pictures, so strips of it show around them.</summary>
    private const int FillStart = 24;

    /// <summary>The width and height of the fill: large enough to be held back.</summary>
    private const int FillSize = 200;

    /// <summary>Where the first picture goes, across and down.</summary>
    private const int TilesStart = 40;

    /// <summary>The first column and row of the clip, which cuts through the fill and the pictures.</summary>
    private const int ClipStart = 70;

    /// <summary>The clip's width and height.</summary>
    private const int ClipSize = 120;

    /// <summary>Half a picture across, which puts a picture over its neighbour.</summary>
    private const double HalfAcross = 0.5;

    /// <summary>The place of a picture that starts inside the fill and ends past it.</summary>
    private const int Far = 2;

    /// <summary>The place of a picture that is wholly past the fill.</summary>
    private const int Further = 3;

    /// <summary>The colour the frame is cleared to.</summary>
    private const uint Cleared = 0xFF202020;

    /// <summary>The colour of the fill.</summary>
    private const uint Paper = 0xFFFFFFFF;

    /// <summary>The colour of the first picture; each later one is a step further on.</summary>
    private const uint FirstTile = 0xFF102030;

    /// <summary>The step between the colours of pictures.</summary>
    private const uint TileStep = 0x00111111;

    /// <summary>Pictures that cover a square of the fill exactly leave the fill showing around them and nowhere else.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicturesThatCoverASquareHideTheFillUnderThem() =>
        await Assert.That(Check([new(0, 0), new(1, 0), new(0, 1), new(1, 1)], false)).IsTrue();

    /// <summary>A missing picture leaves the fill showing in its place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AGapBetweenPicturesShowsTheFill() =>
        await Assert.That(Check([new(0, 0), new(1, 0), new(0, 1)], false)).IsTrue();

    /// <summary>Pictures that overlap are drawn in order over the whole fill.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingPicturesAreDrawnInOrder() =>
        await Assert.That(Check([new(0, 0), new(HalfAcross, 0), new(0, 1), new(1, 1)], false)).IsTrue();

    /// <summary>Pictures that reach past the fill are drawn whole, and the fill shows only inside its own bounds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicturesPastTheFillAreDrawnWhole() =>
        await Assert.That(Check([new(Far, 0), new(Further, 0), new(Far, 1), new(Further, 1)], false)).IsTrue();

    /// <summary>A clip cuts the fill and the pictures alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AClipCutsTheFillAndThePictures() =>
        await Assert.That(Check([new(0, 0), new(1, 0), new(0, 1), new(1, 1)], true)).IsTrue();

    /// <summary>Draws a frame with the fill and the pictures and compares every pixel with what painting each in turn gives.</summary>
    /// <param name="places">Where each picture goes, in pictures across and down from the first.</param>
    /// <param name="clipped">Whether a clip cuts through the fill and the pictures.</param>
    /// <returns><see langword="true"/> when every pixel is right.</returns>
    private static bool Check(TilePlace[] places, bool clipped)
    {
        using var backend = new CpuRenderBackend(static (_, _, _) => { });
        backend.Resize(new(FrameSize, FrameSize));
        var tiles = new IRenderSurface[places.Length];
        var expected = new uint[FrameSize * FrameSize];
        expected.AsSpan().Fill(Cleared);
        var reach = clipped ? new Box(ClipStart, ClipStart, ClipStart + ClipSize, ClipStart + ClipSize) : new Box(0, 0, FrameSize, FrameSize);
        Paint(expected, new(FillStart, FillStart, FillStart + FillSize, FillStart + FillSize), reach, Paper);
        using (var pass = backend.BeginFrame())
        {
            pass.Clear(Cleared);
            if (clipped)
            {
                pass.SetClip(new(ClipStart, ClipStart, ClipSize, ClipSize));
            }

            pass.FillRect(new(FillStart, FillStart, FillSize, FillSize), Paper);
            for (var i = 0; i < places.Length; i++)
            {
                var color = FirstTile + ((uint)i * TileStep);
                tiles[i] = backend.Surfaces.Create(TileSize, TileSize);
                _ = tiles[i].Write(color, static (target, in fill) =>
                {
                    MemoryMarshal.Cast<byte, uint>(target.Pixels).Fill(fill);
                    return true;
                });
                var x = TilesStart + (int)(places[i].Column * TileSize);
                var y = TilesStart + (int)(places[i].Row * TileSize);
                pass.DrawSurface(tiles[i], new(x, y, TileSize, TileSize), false);
                Paint(expected, new(x, y, x + TileSize, y + TileSize), reach, color);
            }
        }

        var same = MemoryMarshal.Cast<byte, uint>(backend.FramePixels).SequenceEqual(expected);
        foreach (var tile in tiles)
        {
            tile.Dispose();
        }

        return same;
    }

    /// <summary>Paints a box of the expected frame, inside the reach of the clip and the frame.</summary>
    /// <param name="pixels">The expected frame.</param>
    /// <param name="box">The box.</param>
    /// <param name="reach">The pixels drawing may reach.</param>
    /// <param name="color">The colour.</param>
    private static void Paint(uint[] pixels, Box box, Box reach, uint color)
    {
        for (var y = Math.Max(box.Top, Math.Max(reach.Top, 0)); y < Math.Min(box.Bottom, Math.Min(reach.Bottom, FrameSize)); y++)
        {
            for (var x = Math.Max(box.Left, Math.Max(reach.Left, 0)); x < Math.Min(box.Right, Math.Min(reach.Right, FrameSize)); x++)
            {
                pixels[(y * FrameSize) + x] = color;
            }
        }
    }

    /// <summary>Where a picture goes, counted in pictures from the first.</summary>
    /// <param name="Column">The pictures across.</param>
    /// <param name="Row">The pictures down.</param>
    private readonly record struct TilePlace(double Column, double Row);

    /// <summary>A box of pixels.</summary>
    /// <param name="Left">The first column.</param>
    /// <param name="Top">The first row.</param>
    /// <param name="Right">The column after the last.</param>
    /// <param name="Bottom">The row after the last.</param>
    private readonly record struct Box(int Left, int Top, int Right, int Bottom);
}

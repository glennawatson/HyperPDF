// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Rendering.Vulkan;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// Checks that a Vulkan frame asked for again is shown without drawing its scene again, and that every frame still has the pixels
/// a frame drawn from nothing has: after the draws change, after a tile is written again and when the same draws come back.
/// The tests skip on machines with no Vulkan driver.
/// </summary>
public sealed class VulkanKeptFrameTests
{
    /// <summary>The frame's width and height in pixels.</summary>
    private const int FrameSize = 24;

    /// <summary>The tile's width and height in pixels.</summary>
    private const int TileSize = 8;

    /// <summary>Where the tile is drawn, across and down.</summary>
    private const double TileAt = 4;

    /// <summary>Where the see-through fill starts, across and down.</summary>
    private const double FillAt = 12;

    /// <summary>The width and height of the see-through fill.</summary>
    private const double FillSide = 10;

    /// <summary>The first opaque colour the frame is cleared to.</summary>
    private const uint FirstCanvas = 0xFF203040;

    /// <summary>The second opaque colour the frame is cleared to.</summary>
    private const uint SecondCanvas = 0xFF605040;

    /// <summary>The see-through colour drawn over the tile.</summary>
    private const uint Veil = 0x6040A0E0;

    /// <summary>The byte the tile's channels hold first.</summary>
    private const byte FirstInk = 0x30;

    /// <summary>The byte the tile's channels hold after it is written again.</summary>
    private const byte SecondInk = 0xC0;

    /// <summary>The bytes of one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the alpha byte in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The frames drawn from the same draws in a row.</summary>
    private const int Repeats = 3;

    /// <summary>A frame drawn again shows the pixels it showed, a changed frame shows its own, and only a repeated frame is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyAFrameWithTheSameDrawsAndTilesIsKept()
    {
        var context = VulkanContext.TryCreate();
        if (context is null)
        {
            Skip.Test("This machine has no Vulkan driver.");
            return;
        }

        using var gpu = new VulkanRenderBackend(context);
        gpu.Resize(new(FrameSize, FrameSize));
        using var tile = gpu.Surfaces.Create(TileSize, TileSize);
        Write(tile, FirstInk);

        var first = DrawAndRead(gpu, tile, FirstCanvas);
        var differences = 0;
        for (var repeat = 0; repeat < Repeats; repeat++)
        {
            differences += CountDifferences(first, DrawAndRead(gpu, tile, FirstCanvas));
        }

        var keptAfterRepeats = gpu.KeptFrames;

        var changed = DrawAndRead(gpu, tile, SecondCanvas);
        var changedMatches = CountDifferences(changed, DrawFresh(SecondCanvas, FirstInk));
        var keptAfterChange = gpu.KeptFrames;

        var back = DrawAndRead(gpu, tile, FirstCanvas);
        var backDifferences = CountDifferences(first, back);

        Write(tile, SecondInk);
        var written = DrawAndRead(gpu, tile, FirstCanvas);
        var writtenMatches = CountDifferences(written, DrawFresh(FirstCanvas, SecondInk));
        var keptAfterWrite = gpu.KeptFrames;

        using (Assert.Multiple())
        {
            await Assert.That(differences).IsEqualTo(0);
            await Assert.That(keptAfterRepeats).IsEqualTo(Repeats);
            await Assert.That(CountDifferences(first, changed)).IsGreaterThan(0);
            await Assert.That(changedMatches).IsEqualTo(0);
            await Assert.That(keptAfterChange).IsEqualTo(Repeats);
            await Assert.That(backDifferences).IsEqualTo(0);
            await Assert.That(CountDifferences(first, written)).IsGreaterThan(0);
            await Assert.That(writtenMatches).IsEqualTo(0);
            await Assert.That(keptAfterWrite).IsEqualTo(Repeats);
        }
    }

    /// <summary>A frame drawn after the backend was resized is drawn afresh, not kept from the frame the old target held.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AResizedFrameIsNotKept()
    {
        var context = VulkanContext.TryCreate();
        if (context is null)
        {
            Skip.Test("This machine has no Vulkan driver.");
            return;
        }

        using var gpu = new VulkanRenderBackend(context);
        gpu.Resize(new(FrameSize, FrameSize));
        using var tile = gpu.Surfaces.Create(TileSize, TileSize);
        Write(tile, FirstInk);
        _ = DrawAndRead(gpu, tile, FirstCanvas);

        gpu.Resize(new(FrameSize + TileSize, FrameSize + TileSize));
        var resized = new byte[(FrameSize + TileSize) * (FrameSize + TileSize) * BytesPerPixel];
        Draw(gpu, tile, FirstCanvas);
        gpu.ReadPixels(resized);

        await Assert.That(gpu.KeptFrames).IsEqualTo(0);
        await Assert.That(resized.Length).IsGreaterThan(FrameSize * FrameSize * BytesPerPixel);
    }

    /// <summary>Draws the frame on a backend and reads its pixels.</summary>
    /// <param name="gpu">The backend.</param>
    /// <param name="tile">The tile drawn.</param>
    /// <param name="canvas">The colour the frame is cleared to.</param>
    /// <returns>The pixels.</returns>
    private static byte[] DrawAndRead(VulkanRenderBackend gpu, IRenderSurface tile, uint canvas)
    {
        Draw(gpu, tile, canvas);
        var pixels = new byte[FrameSize * FrameSize * BytesPerPixel];
        gpu.ReadPixels(pixels);
        return pixels;
    }

    /// <summary>Draws the frame on a new backend that has drawn nothing before, and reads its pixels.</summary>
    /// <param name="canvas">The colour the frame is cleared to.</param>
    /// <param name="ink">The byte the tile's channels hold.</param>
    /// <returns>The pixels.</returns>
    private static byte[] DrawFresh(uint canvas, byte ink)
    {
        using var gpu = new VulkanRenderBackend(VulkanContext.TryCreate()!);
        gpu.Resize(new(FrameSize, FrameSize));
        using var tile = gpu.Surfaces.Create(TileSize, TileSize);
        Write(tile, ink);
        return DrawAndRead(gpu, tile, canvas);
    }

    /// <summary>Draws a clear, a tile and a see-through fill over it.</summary>
    /// <param name="gpu">The backend.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="canvas">The colour the frame is cleared to.</param>
    private static void Draw(VulkanRenderBackend gpu, IRenderSurface tile, uint canvas)
    {
        using var pass = gpu.BeginFrame();
        pass.Clear(canvas);
        pass.DrawSurface(tile, new(TileAt, TileAt, TileSize, TileSize), smooth: false);
        pass.FillRect(new(FillAt, FillAt, FillSide, FillSide), Veil);
    }

    /// <summary>Writes a tile's pixels.</summary>
    /// <param name="tile">The tile.</param>
    /// <param name="ink">The byte its colour channels hold.</param>
    private static void Write(IRenderSurface tile, byte ink) =>
        _ = tile.Write(ink, static (target, in value) =>
        {
            target.Pixels.Fill(value);
            for (var i = AlphaOffset; i < target.Pixels.Length; i += BytesPerPixel)
            {
                target.Pixels[i] = byte.MaxValue;
            }

            return true;
        });

    /// <summary>Counts the bytes that differ between two frames.</summary>
    /// <param name="first">One frame.</param>
    /// <param name="second">The other.</param>
    /// <returns>The number of bytes that differ.</returns>
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

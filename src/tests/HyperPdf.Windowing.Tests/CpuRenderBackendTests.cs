// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the managed CPU backend draws and presents frames.</summary>
public sealed class CpuRenderBackendTests
{
    /// <summary>The frame width in pixels.</summary>
    private const int FrameSize = 8;

    /// <summary>The tile width in pixels.</summary>
    private const int TileSize = 2;

    /// <summary>A place well outside the frame.</summary>
    private const int FarAway = 100;

    /// <summary>The opaque white colour.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>The opaque red colour.</summary>
    private const uint Red = 0xFFFF0000;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the red byte in BGRA.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha byte in BGRA.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The byte value of a full channel.</summary>
    private const byte Full = 255;

    /// <summary>A cleared frame is presented once with the colour everywhere.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearedFrameIsPresentedWithTheColour()
    {
        var presented = 0;
        byte first = 0;
        using var backend = new CpuRenderBackend((pixels, _, _) =>
        {
            presented++;
            first = pixels[0];
        });
        backend.Resize(new(FrameSize, FrameSize));

        using (var pass = backend.BeginFrame())
        {
            pass.Clear(White);
        }

        await Assert.That(presented).IsEqualTo(1);
        await Assert.That(first).IsEqualTo(Full);
    }

    /// <summary>A tile at one to one lands at its position and the rest stays clear.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TileIsCopiedToItsDestination()
    {
        using var backend = NewBackend();
        using var tile = backend.Surfaces.Create(TileSize, TileSize);
        _ = tile.Write(0, static (target, in _) =>
        {
            for (var i = 0; i < TileSize * TileSize; i++)
            {
                target.Pixels[(i * BytesPerPixel) + RedOffset] = Full;
                target.Pixels[(i * BytesPerPixel) + AlphaOffset] = Full;
            }

            return true;
        });

        using (var pass = backend.BeginFrame())
        {
            pass.Clear(White);
            pass.DrawSurface(tile, new(TileSize, TileSize, TileSize, TileSize), smooth: false);
        }

        var inside = backend.FramePixels[(TileSize * backend.Stride) + (TileSize * BytesPerPixel)];
        var outside = backend.FramePixels[0];

        await Assert.That(inside).IsEqualTo((byte)0);
        await Assert.That(outside).IsEqualTo(Full);
    }

    /// <summary>A tile scaled up covers the larger area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScaledTileCoversTheDestination()
    {
        using var backend = NewBackend();
        using var tile = backend.Surfaces.Create(TileSize, TileSize);
        _ = tile.Write(0, static (target, in _) =>
        {
            target.Pixels.Fill(Full);
            return true;
        });

        using (var pass = backend.BeginFrame())
        {
            pass.Clear(Red);
            pass.DrawSurface(tile, new(0, 0, FrameSize, FrameSize), smooth: true);
        }

        await Assert.That(backend.FramePixels[(FrameSize * backend.Stride) - 1]).IsEqualTo(Full);
        await Assert.That(backend.FramePixels[0]).IsEqualTo(Full);
    }

    /// <summary>A tile partly outside the frame is clipped without error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TileOutsideTheFrameIsClipped()
    {
        using var backend = NewBackend();
        using var tile = backend.Surfaces.Create(TileSize, TileSize);

        using (var pass = backend.BeginFrame())
        {
            pass.DrawSurface(tile, new(-1, -1, TileSize, TileSize), smooth: false);
            pass.DrawSurface(tile, new(FrameSize - 1, FrameSize - 1, TileSize, TileSize), smooth: false);
            pass.DrawSurface(tile, new(FarAway, 0, TileSize, TileSize), smooth: false);
        }

        await Assert.That(backend.IsAlive).IsTrue();
    }

    /// <summary>Makes a backend of the test frame size that ignores its frames.</summary>
    /// <returns>The backend.</returns>
    private static CpuRenderBackend NewBackend()
    {
        var backend = new CpuRenderBackend(static (_, _, _) => { });
        backend.Resize(new(FrameSize, FrameSize));
        return backend;
    }
}

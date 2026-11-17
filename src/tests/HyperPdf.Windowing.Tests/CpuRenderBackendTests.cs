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

    /// <summary>Where the clip of the clip tests starts, in pixels.</summary>
    private const int ClipStart = 2;

    /// <summary>The width and height of the clip of the clip tests, in pixels.</summary>
    private const int ClipSize = 3;

    /// <summary>A place well outside the frame.</summary>
    private const int FarAway = 100;

    /// <summary>The opaque white colour.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>The opaque red colour.</summary>
    private const uint Red = 0xFFFF0000;

    /// <summary>The opaque green colour.</summary>
    private const uint Green = 0xFF00FF00;

    /// <summary>How much the layer of the layer test counts.</summary>
    private const double HalfOpacity = 0.5;

    /// <summary>Half of a full channel, which is what a half opaque layer leaves of a channel the layer has none of over white.</summary>
    private const int HalfChannel = 127;

    /// <summary>The most a mixed channel may be off by, for rounding.</summary>
    private const int Rounding = 2;

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

    /// <summary>A fill limited by a clip changes only the pixels inside the clip, and reaches the whole frame again once the clip is cleared.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClippedFillChangesOnlyTheClip()
    {
        using var backend = NewBackend();
        using (var pass = backend.BeginFrame())
        {
            pass.Clear(White);
            pass.SetClip(new(ClipStart, ClipStart, ClipSize, ClipSize));
            pass.FillRect(new(0, 0, FrameSize, FrameSize), Red);
            pass.ClearClip();
            pass.FillRect(new(0, 0, 1, 1), Red);
        }

        var inside = backend.FramePixels[(ClipStart * backend.Stride) + (ClipStart * BytesPerPixel)];
        var beside = backend.FramePixels[(ClipStart * backend.Stride) + ((ClipStart + ClipSize) * BytesPerPixel)];
        var after = backend.FramePixels[0];

        await Assert.That(inside).IsEqualTo((byte)0);
        await Assert.That(beside).IsEqualTo(Full);
        await Assert.That(after).IsEqualTo((byte)0);
    }

    /// <summary>A half opaque layer is mixed with what was there as one: a fill under another in the layer does not show through, and what comes after is opaque.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HalfOpaqueLayerIsMixedWithTheFrameAsOne()
    {
        using var backend = NewBackend();
        using (var pass = backend.BeginFrame())
        {
            pass.Clear(White);
            pass.SetOpacity(HalfOpacity);
            pass.FillRect(new(0, 0, FrameSize, FrameSize), Green);
            pass.FillRect(new(0, 0, FrameSize, FrameSize), Red);
            pass.SetOpacity(1);
            pass.FillRect(new(0, 0, 1, 1), Red);
        }

        // Half red over white, with the green the red covered not showing through: full red, and half green and blue.
        var middle = (ClipStart * backend.Stride) + (ClipStart * BytesPerPixel);
        var pixels = backend.FramePixels.ToArray();

        await Assert.That(pixels[middle + RedOffset]).IsGreaterThanOrEqualTo((byte)(Full - 1));
        await Assert.That(pixels[middle]).IsBetween((byte)(HalfChannel - Rounding), (byte)(HalfChannel + Rounding));
        await Assert.That(pixels[middle + 1]).IsBetween((byte)(HalfChannel - Rounding), (byte)(HalfChannel + Rounding));
        await Assert.That(pixels[0]).IsEqualTo((byte)0);
    }

    /// <summary>A scaled tile limited by a clip changes only the pixels inside the clip, and a clip does not last into the next frame.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClippedTileChangesOnlyTheClip()
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
            pass.SetClip(new(ClipStart, ClipStart, ClipSize, ClipSize));
            pass.DrawSurface(tile, new(0, 0, FrameSize, FrameSize), smooth: false);
        }

        var inside = backend.FramePixels[(ClipStart * backend.Stride) + (ClipStart * BytesPerPixel)];
        var outside = backend.FramePixels[0];

        // The clip of the frame that ended is gone when the next one starts.
        using (var pass = backend.BeginFrame())
        {
            pass.Clear(White);
            pass.FillRect(new(0, 0, FrameSize, FrameSize), Red);
        }

        await Assert.That(inside).IsEqualTo((byte)0);
        await Assert.That(outside).IsEqualTo(Full);
        await Assert.That(backend.FramePixels[0]).IsEqualTo((byte)0);
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

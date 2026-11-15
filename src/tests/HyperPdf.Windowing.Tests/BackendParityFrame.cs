// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>
/// One small frame every render backend draws the same way: a clear, a tile at its own size, the tile scaled up two times and a half
/// transparent fill. GPU backend tests compare their pixels with the CPU backend's.
/// </summary>
internal static class BackendParityFrame
{
    /// <summary>The frame size in pixels.</summary>
    internal const int FrameSize = 16;

    /// <summary>The bytes per pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The most a channel may differ between two backends, for rounding in blending.</summary>
    internal const int Tolerance = 2;

    /// <summary>The tile size in pixels.</summary>
    private const int TileSize = 4;

    /// <summary>Half the frame size in pixels.</summary>
    private const double HalfFrame = 8;

    /// <summary>Twice the tile size in pixels.</summary>
    private const double DoubleTile = 8;

    /// <summary>The opaque white used to clear the frame.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>A half transparent blue.</summary>
    private const uint HalfBlue = 0x800000FF;

    /// <summary>The step between the colour values of neighbouring tile pixels.</summary>
    private const int Step = 40;

    /// <summary>The red value of every tile pixel.</summary>
    private const byte Red = 100;

    /// <summary>An opaque alpha value.</summary>
    private const byte Opaque = 255;

    /// <summary>The offset of the green byte in BGRA.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red byte in BGRA.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha byte in BGRA.</summary>
    private const int AlphaOffset = 3;

    /// <summary>Draws the frame on the CPU backend and returns its pixels.</summary>
    /// <returns>The premultiplied BGRA pixels.</returns>
    internal static byte[] DrawOnCpu()
    {
        using var cpu = new CpuRenderBackend(static (_, _, _) => { });
        cpu.Resize(new(FrameSize, FrameSize));
        Draw(cpu);
        return cpu.FramePixels.ToArray();
    }

    /// <summary>Draws the frame on a backend.</summary>
    /// <param name="backend">The backend, already sized to the frame.</param>
    internal static void Draw(IRenderBackend backend)
    {
        using var tile = backend.Surfaces.Create(TileSize, TileSize);
        _ = tile.Write(0, static (target, in _) => FillTile(target));
        using var pass = backend.BeginFrame();
        pass.Clear(White);
        pass.DrawSurface(tile, new(TileSize, TileSize, TileSize, TileSize), smooth: false);
        pass.DrawSurface(tile, new(HalfFrame, 0, DoubleTile, DoubleTile), smooth: false);
        pass.FillRect(new(0, HalfFrame, TileSize, TileSize), HalfBlue);
    }

    /// <summary>Finds the largest difference between any channel of two frames.</summary>
    /// <param name="first">The first frame.</param>
    /// <param name="second">The second frame.</param>
    /// <returns>The largest channel difference.</returns>
    internal static int MaxDifference(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var largest = 0;
        for (var i = 0; i < first.Length; i++)
        {
            largest = Math.Max(largest, Math.Abs(first[i] - second[i]));
        }

        return largest;
    }

    /// <summary>Fills a tile with a gradient so every pixel differs.</summary>
    /// <param name="target">The tile pixels.</param>
    /// <returns><see langword="true"/> always.</returns>
    private static bool FillTile(RenderTarget target)
    {
        for (var y = 0; y < target.Height; y++)
        {
            for (var x = 0; x < target.Width; x++)
            {
                var at = (y * target.Stride) + (x * BytesPerPixel);
                target.Pixels[at] = (byte)(x * Step);
                target.Pixels[at + GreenOffset] = (byte)(y * Step);
                target.Pixels[at + RedOffset] = Red;
                target.Pixels[at + AlphaOffset] = Opaque;
            }
        }

        return true;
    }
}

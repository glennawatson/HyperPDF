// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Rendering.Cpu;
using HyperPdf.Rendering.Vulkan;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Draws the same frame with the CPU backend and the Vulkan backend and compares the pixels. The tests skip on machines with no Vulkan driver.</summary>
public sealed class VulkanRenderBackendTests
{
    /// <summary>The frame size in pixels.</summary>
    private const int FrameSize = 16;

    /// <summary>The tile size in pixels.</summary>
    private const int TileSize = 4;

    /// <summary>Half the frame size in pixels.</summary>
    private const double HalfFrame = 8;

    /// <summary>Twice the tile size in pixels.</summary>
    private const double DoubleTile = 8;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The opaque white used to clear the frame.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>A half transparent blue.</summary>
    private const uint HalfBlue = 0x800000FF;

    /// <summary>The most a channel may differ between the two backends, for rounding in blending.</summary>
    private const int Tolerance = 2;

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

    /// <summary>A tile drawn unscaled, a tile scaled up two times and a solid fill land on the same pixels in both backends.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GpuFrameMatchesTheCpuFrame()
    {
        var context = VulkanContext.TryCreate();
        if (context is null)
        {
            Skip.Test("This machine has no Vulkan driver.");
            return;
        }

        using var gpu = new VulkanRenderBackend(context);
        using var cpu = new CpuRenderBackend(static (_, _, _) => { });
        var gpuPixels = DrawAndRead(gpu, cpu, out var cpuPixels);

        await Assert.That(MaxDifference(cpuPixels, gpuPixels)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>Draws the test frame on both backends and returns the pixels of each.</summary>
    /// <param name="gpu">The Vulkan backend.</param>
    /// <param name="cpu">The CPU backend.</param>
    /// <param name="cpuPixels">Receives the CPU pixels.</param>
    /// <returns>The Vulkan pixels.</returns>
    private static byte[] DrawAndRead(VulkanRenderBackend gpu, CpuRenderBackend cpu, out byte[] cpuPixels)
    {
        gpu.Resize(new(FrameSize, FrameSize));
        cpu.Resize(new(FrameSize, FrameSize));
        Draw(gpu);
        Draw(cpu);
        var gpuPixels = new byte[FrameSize * FrameSize * BytesPerPixel];
        gpu.ReadPixels(gpuPixels);
        cpuPixels = cpu.FramePixels.ToArray();
        return gpuPixels;
    }

    /// <summary>Draws the test frame on a backend.</summary>
    /// <param name="backend">The backend.</param>
    private static void Draw(IRenderBackend backend)
    {
        using var tile = backend.Surfaces.Create(TileSize, TileSize);
        _ = tile.Write(0, static (target, in _) => FillTile(target));
        using var pass = backend.BeginFrame();
        pass.Clear(White);
        pass.DrawSurface(tile, new(TileSize, TileSize, TileSize, TileSize), smooth: false);
        pass.DrawSurface(tile, new(HalfFrame, 0, DoubleTile, DoubleTile), smooth: false);
        pass.FillRect(new(0, HalfFrame, TileSize, TileSize), HalfBlue);
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

    /// <summary>Finds the largest difference between any channel of two frames.</summary>
    /// <param name="first">The first frame.</param>
    /// <param name="second">The second frame.</param>
    /// <returns>The largest channel difference.</returns>
    private static int MaxDifference(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var largest = 0;
        for (var i = 0; i < first.Length; i++)
        {
            largest = Math.Max(largest, Math.Abs(first[i] - second[i]));
        }

        return largest;
    }
}

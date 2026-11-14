// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using HyperPdf.Rendering.Metal;

namespace HyperPdf.Windowing.Tests;

/// <summary>Draws the same frame with the CPU backend and the Metal backend and compares the pixels. The test runs on a Mac whose GPU Metal can use.</summary>
public sealed class MetalRenderBackendTests
{
    /// <summary>A tile drawn unscaled, a tile scaled up two times and a solid fill land on the same pixels in both backends.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GpuFrameMatchesTheCpuFrame()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("Metal only runs on macOS.");
            return;
        }

        var gpuPixels = DrawOnMetal();
        if (gpuPixels is null)
        {
            Skip.Test("This Mac has no GPU Metal can use.");
            return;
        }

        await Assert.That(BackendParityFrame.MaxDifference(BackendParityFrame.DrawOnCpu(), gpuPixels)).IsLessThanOrEqualTo(BackendParityFrame.Tolerance);
    }

    /// <summary>Draws the parity frame with Metal and reads it back.</summary>
    /// <returns>The pixels, or <see langword="null"/> when the machine has no GPU Metal can use.</returns>
    [SupportedOSPlatform("macos")]
    private static byte[]? DrawOnMetal()
    {
        MetalRenderBackend gpu;
        try
        {
            gpu = new();
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        using (gpu)
        {
            var pixels = new byte[BackendParityFrame.FrameSize * BackendParityFrame.FrameSize * BackendParityFrame.BytesPerPixel];
            gpu.Resize(new(BackendParityFrame.FrameSize, BackendParityFrame.FrameSize));
            BackendParityFrame.Draw(gpu);
            gpu.ReadPixels(pixels);
            return pixels;
        }
    }
}

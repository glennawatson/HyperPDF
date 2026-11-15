// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Direct3D;

namespace HyperPdf.Windowing.Tests;

/// <summary>Draws the same frame with the CPU backend and the Direct3D backend and compares the pixels. On Windows without a GPU the software rasteriser stands in.</summary>
public sealed class Direct3DRenderBackendTests
{
    /// <summary>A tile drawn unscaled, a tile scaled up two times and a solid fill land on the same pixels in both backends.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GpuFrameMatchesTheCpuFrame()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Direct3D only runs on Windows.");
            return;
        }

        var gpuPixels = new byte[BackendParityFrame.FrameSize * BackendParityFrame.FrameSize * BackendParityFrame.BytesPerPixel];
        using (var gpu = new Direct3DRenderBackend())
        {
            gpu.Resize(new(BackendParityFrame.FrameSize, BackendParityFrame.FrameSize));
            BackendParityFrame.Draw(gpu);
            gpu.ReadPixels(gpuPixels);
        }

        await Assert.That(BackendParityFrame.MaxDifference(BackendParityFrame.DrawOnCpu(), gpuPixels)).IsLessThanOrEqualTo(BackendParityFrame.Tolerance);
    }
}

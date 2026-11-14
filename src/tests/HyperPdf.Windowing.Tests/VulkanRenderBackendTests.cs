// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Vulkan;

namespace HyperPdf.Windowing.Tests;

/// <summary>Draws the same frame with the CPU backend and the Vulkan backend and compares the pixels. The test skips on machines with no Vulkan driver.</summary>
public sealed class VulkanRenderBackendTests
{
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

        var gpuPixels = new byte[BackendParityFrame.FrameSize * BackendParityFrame.FrameSize * BackendParityFrame.BytesPerPixel];
        using (var gpu = new VulkanRenderBackend(context))
        {
            gpu.Resize(new(BackendParityFrame.FrameSize, BackendParityFrame.FrameSize));
            BackendParityFrame.Draw(gpu);
            gpu.ReadPixels(gpuPixels);
        }

        await Assert.That(BackendParityFrame.MaxDifference(BackendParityFrame.DrawOnCpu(), gpuPixels)).IsLessThanOrEqualTo(BackendParityFrame.Tolerance);
    }
}

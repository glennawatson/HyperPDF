// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Cpu;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that the head starts the GPU by default and falls back to the CPU when it cannot.</summary>
public sealed class RenderBackendSelectorTests
{
    /// <summary>The GPU is used first when it starts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GpuPreferenceUsesTheGpuWhenItStarts()
    {
        using var gpu = NewCpu();

        var choice = RenderBackendSelector.Choose(RenderBackendPreference.Gpu, gpu, static g => g, static _ => NewCpu());

        await Assert.That(choice.Backend).IsSameReferenceAs(gpu);
        await Assert.That(choice.FellBack).IsFalse();
    }

    /// <summary>A missing GPU falls back to the CPU and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GpuPreferenceFallsBackWhenNoGpuIsFound()
    {
        var choice = RenderBackendSelector.Choose(RenderBackendPreference.Gpu, 0, static _ => null, static _ => NewCpu());

        await Assert.That(choice.Backend.Kind).IsEqualTo(RenderBackendKind.Cpu);
        await Assert.That(choice.FellBack).IsTrue();
    }

    /// <summary>A missing native library counts as no GPU, not a crash.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingDriverLibraryFallsBack()
    {
        var choice = RenderBackendSelector.Choose(RenderBackendPreference.Gpu, 0, static _ => throw new DllNotFoundException("libvulkan"), static _ => NewCpu());

        await Assert.That(choice.FellBack).IsTrue();
    }

    /// <summary>The CPU preference never starts the GPU.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CpuPreferenceSkipsTheGpu()
    {
        var gpuStarted = false;

        var choice = RenderBackendSelector.Choose(
            RenderBackendPreference.Cpu,
            0,
            _ =>
            {
                gpuStarted = true;
                return null;
            },
            static _ => NewCpu());

        await Assert.That(gpuStarted).IsFalse();
        await Assert.That(choice.FellBack).IsFalse();
    }

    /// <summary>Makes a CPU backend that ignores its frames.</summary>
    /// <returns>The backend.</returns>
    private static CpuRenderBackend NewCpu() => new(static (_, _, _) => { });
}

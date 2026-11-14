// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Rendering;

/// <summary>Chooses the backend a head starts: the GPU by default, and the CPU when the GPU is not wanted or cannot start.</summary>
public static class RenderBackendSelector
{
    /// <summary>Starts a backend following the preference.</summary>
    /// <typeparam name="TState">The state handed to both factories.</typeparam>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="state">The state, such as the native window handle.</param>
    /// <param name="gpu">Starts the GPU backend, or returns <see langword="null"/> when this machine has no usable GPU path.</param>
    /// <param name="cpu">Starts the CPU backend, which must succeed.</param>
    /// <returns>The backend, and whether the CPU was used only because the GPU failed.</returns>
    public static BackendChoice Choose<TState>(
        RenderBackendPreference preference,
        TState state,
        Func<TState, IRenderBackend?> gpu,
        Func<TState, IRenderBackend> cpu)
    {
        ArgumentNullException.ThrowIfNull(gpu);
        ArgumentNullException.ThrowIfNull(cpu);

        if (preference != RenderBackendPreference.Gpu)
        {
            return new(cpu(state), FellBack: false);
        }

        var started = Start(gpu, state);
        if (started is { IsAlive: true })
        {
            return new(started, FellBack: false);
        }

        started?.Dispose();
        return new(cpu(state), FellBack: true);
    }

    /// <summary>Runs the GPU factory and treats a missing driver or library as "no GPU".</summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="gpu">The GPU factory.</param>
    /// <param name="state">The state.</param>
    /// <returns>The backend, or <see langword="null"/>.</returns>
    private static IRenderBackend? Start<TState>(Func<TState, IRenderBackend?> gpu, TState state)
    {
        try
        {
            return gpu(state);
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException)
        {
            // A missing driver or library is an expected reason to use the CPU, not a fault to surface.
            return null;
        }
    }
}

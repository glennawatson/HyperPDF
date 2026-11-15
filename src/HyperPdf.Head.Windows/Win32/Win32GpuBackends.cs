// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Rendering.Direct3D;
using HyperPdf.Rendering.Direct3D12;
using HyperPdf.Rendering.Vulkan;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Starts the graphics backend a Windows window draws with, in the order the user's choice asks for, so a choice the system cannot use falls to the next one.</summary>
internal static class Win32GpuBackends
{
    /// <summary>Starts the first graphics backend that works, following the preference. Direct3D 11 is the default because it runs on every Windows GPU driver.</summary>
    /// <param name="preference">The choice of the settings.</param>
    /// <param name="window">The native window handle.</param>
    /// <returns>The backend, or <see langword="null"/> when none could start, so the caller uses the processor.</returns>
    internal static IRenderBackend? Start(RenderBackendPreference preference, nint window) => preference switch
    {
        RenderBackendPreference.Direct3D12 => Direct3D12(window) ?? Direct3D11(window) ?? (IRenderBackend?)Vulkan(window),
        RenderBackendPreference.Vulkan => Vulkan(window) ?? Direct3D11(window) ?? (IRenderBackend?)Direct3D12(window),
        _ => Direct3D11(window) ?? Direct3D12(window) ?? (IRenderBackend?)Vulkan(window),
    };

    /// <summary>Starts Direct3D 11.</summary>
    /// <param name="window">The native window handle.</param>
    /// <returns>The backend, or <see langword="null"/> when this machine has no Direct3D 11 device.</returns>
    private static Direct3DRenderBackend? Direct3D11(nint window)
    {
        try
        {
            return new(window);
        }
        catch (InvalidOperationException)
        {
            // No device, such as in a session without a GPU or a driver: the next backend gets its turn.
            return null;
        }
    }

    /// <summary>Starts Direct3D 12.</summary>
    /// <param name="window">The native window handle.</param>
    /// <returns>The backend, or <see langword="null"/> when this machine has no Direct3D 12 device.</returns>
    private static Direct3D12RenderBackend? Direct3D12(nint window)
    {
        try
        {
            return new(window);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            // Windows before version 10 has no d3d12.dll, and a GPU without a Direct3D 12 driver fails to make a device.
            return null;
        }
    }

    /// <summary>Starts Vulkan and attaches it to the window.</summary>
    /// <param name="window">The native window handle.</param>
    /// <returns>The backend, or <see langword="null"/> when the machine has no Vulkan driver that can present to a Windows window.</returns>
    private static VulkanRenderBackend? Vulkan(nint window)
    {
        var context = VulkanContext.TryCreate(forWindow: true);
        if (context is null)
        {
            return null;
        }

        var backend = new VulkanRenderBackend(context);
        if (backend.TryPresentToWin32Window(NativeMethods.GetModuleHandle(null), window))
        {
            return backend;
        }

        backend.Dispose();
        return null;
    }
}

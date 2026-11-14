// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Linux.Wayland;
using HyperPdf.Head.Linux.X11;
using HyperPdf.Rendering.Vulkan;
using HyperPdf.Windowing.Accessibility;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Head.Linux;

/// <summary>Opens the window of a Linux session: Wayland when a compositor is reachable, otherwise X11.</summary>
public static class LinuxWindows
{
    /// <summary>Opens a window on the best window system the session offers.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The bridge that publishes the window to AT-SPI.</param>
    /// <param name="system">Receives the window system that was used.</param>
    /// <returns>The window.</returns>
    public static IPlatformWindow Open(RenderBackendPreference preference, IAccessibilityBridge accessibility, out LinuxWindowSystem system)
    {
        ArgumentNullException.ThrowIfNull(accessibility);

        // The GPU backend presents to X11 windows today, so a session that has an X server (a Wayland session does, through XWayland)
        // and a GPU that can present gets an X11 window. The Wayland window draws with the CPU until it can present GPU frames too.
        if (preference == RenderBackendPreference.Gpu && HasX11Display() && VulkanContext.TryCreate(forWindow: true) is { } context)
        {
            if (context.CanPresent)
            {
                system = LinuxWindowSystem.X11;
                return new X11Window(preference, accessibility, context);
            }

            context.Dispose();
        }

        if (WaylandWindow.IsAvailable())
        {
            var wayland = TryOpenWayland(preference, accessibility);
            if (wayland is not null)
            {
                system = LinuxWindowSystem.Wayland;
                return wayland;
            }
        }

        system = LinuxWindowSystem.X11;
        return new X11Window(preference, accessibility);
    }

    /// <summary>Tells whether the session names an X server.</summary>
    /// <returns><see langword="true"/> when DISPLAY is set.</returns>
    private static bool HasX11Display() => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

    /// <summary>Opens a Wayland window, or reports that the compositor cannot host one.</summary>
    /// <param name="preference">What the settings ask for.</param>
    /// <param name="accessibility">The accessibility bridge.</param>
    /// <returns>The window, or <see langword="null"/> when the compositor lacks a protocol the head needs or does not answer.</returns>
    private static WaylandWindow? TryOpenWayland(RenderBackendPreference preference, IAccessibilityBridge accessibility)
    {
        try
        {
            return new(preference, accessibility);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or TimeoutException or IOException)
        {
            // A compositor without xdg-shell or wl_shm, or one that stops answering, is a reason to try X11 (through XWayland), not a failure.
            return null;
        }
    }
}

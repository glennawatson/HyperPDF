// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Vulkan;
using PdfViewerLite.App.Services;
using PdfViewerLite.Skia;

namespace PdfViewerLite.App;

/// <summary>Extension methods for configuring the Avalonia application builder with desktop backends and modern Skia rendering.</summary>
internal static class AppBuilderExtensions
{
    /// <summary>The Skia resource cache limit, separate from the viewer's tile cache.</summary>
    private const long GpuResourceCacheBytes = 128L * 1024 * 1024;

    /// <summary>The Vulkan version needed by the pinned Skia backend.</summary>
    private const int VulkanMinorVersion = 4;

    /// <summary>Configures rendering and windowing for an application.</summary>
    /// <param name="builder">The application builder.</param>
    extension(AppBuilder builder)
    {
        /// <summary>Configures the modern Skia rendering subsystem.</summary>
        /// <returns>The configured application builder.</returns>
        internal AppBuilder UseSkia()
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder
                .UseTextShapingSubsystem(static () => SkiaPlatform.InitializeTextShaping(), "HarfBuzz")
                .UseRenderingSubsystem(static () => SkiaPlatform.Initialize(new SkiaOptions { MaxGpuResourceSizeBytes = GpuResourceCacheBytes }), "Skia");
        }

        /// <summary>Configures the windowing subsystem and modern Skia rendering for the target desktop environment.</summary>
        /// <param name="isLinux">Whether running on Linux.</param>
        /// <param name="x11Display">The DISPLAY environment value.</param>
        /// <param name="waylandDisplay">The WAYLAND_DISPLAY environment value.</param>
        /// <returns>The configured application builder.</returns>
        internal AppBuilder UseDesktopPlatform(bool isLinux, string? x11Display, string? waylandDisplay)
        {
            ArgumentNullException.ThrowIfNull(builder);

            if (isLinux || OperatingSystem.IsLinux())
            {
                builder = DesktopBackend.ShouldUseWayland(isLinux, x11Display, waylandDisplay)
                    ? builder.UseWayland().With(new WaylandPlatformOptions { WlDisplayName = waylandDisplay })
                    : builder.UseX11().With(CreateVulkanOptions());
            }
            else if (OperatingSystem.IsWindows())
            {
                builder = builder.UseWin32().With(CreateVulkanOptions()).With(new Win32PlatformOptions
                {
                    RenderingMode = [Win32RenderingMode.Vulkan, Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software],
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                builder = builder.UseAvaloniaNative().With(new AvaloniaNativePlatformOptions
                {
                    RenderingMode = [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software],
                });
            }

            return builder.UseSkia();
        }
    }

    /// <summary>Requests the Vulkan procedures the pinned Skia backend uses before trying this graphics path.</summary>
    /// <returns>The Vulkan instance settings.</returns>
    private static VulkanOptions CreateVulkanOptions() => new() { VulkanInstanceCreationOptions = new() { VulkanVersion = new(1, VulkanMinorVersion, 0) } };
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Platform;
using Avalonia.Vulkan;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Wraps an Avalonia Vulkan swapchain image as a Skia render target.</summary>
internal sealed class VulkanRenderTarget : ISkiaGpuRenderTarget
{
    /// <summary>The graphics context shared with the compositor.</summary>
    private readonly VulkanSkiaGpu _gpu;

    /// <summary>The platform's swapchain target.</summary>
    private IVulkanRenderTarget? _target;

    /// <summary>Initializes a new instance of the <see cref="VulkanRenderTarget"/> class.</summary>
    /// <param name="gpu">The shared graphics context.</param>
    /// <param name="target">The compositor's swapchain target.</param>
    internal VulkanRenderTarget(VulkanSkiaGpu gpu, IVulkanRenderTarget target)
    {
        _gpu = gpu;
        _target = target;
    }

    /// <inheritdoc/>
    public PlatformRenderTargetState State => Volatile.Read(ref _target)?.State ?? PlatformRenderTargetState.Disposed;

    /// <inheritdoc/>
    public ISkiaGpuRenderSession BeginRenderingSession(IRenderTarget.RenderTargetSceneInfo sceneInfo)
    {
        var platform = (Volatile.Read(ref _target) ?? throw new ObjectDisposedException(nameof(VulkanRenderTarget))).BeginDraw();
        try
        {
            var size = platform.Size;
            if (size.Width <= 0 || size.Height <= 0 || platform.Scaling <= 0)
            {
                throw new InvalidOperationException($"Invalid Vulkan target size {size} or scaling {platform.Scaling}.");
            }

            return CreateSession(_gpu, platform);
        }
        catch
        {
            platform.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _target, null)?.Dispose();

    /// <summary>Wraps a valid swapchain image and transfers ownership to the session.</summary>
    /// <param name="gpu">The shared graphics context.</param>
    /// <param name="platform">The active swapchain image.</param>
    /// <returns>The rendering session.</returns>
    /// <exception cref="InvalidOperationException">The image format is unsupported.</exception>
    private static VulkanRenderSession CreateSession(VulkanSkiaGpu gpu, IVulkanRenderSession platform)
    {
        var context = gpu.GrContext;
        context.ResetContext();
        var image = platform.ImageInfo;
        var imageInfo = new GRVkImageInfo
        {
            CurrentQueueFamily = gpu.Vulkan.Device.GraphicsQueueFamilyIndex,
            Format = image.Format,
            Image = image.Handle,
            ImageLayout = image.Layout,
            ImageTiling = image.Tiling,
            ImageUsageFlags = image.UsageFlags,
            LevelCount = image.LevelCount,
            SampleCount = image.SampleCount,
            Protected = image.IsProtected,
            Alloc = new GRVkAlloc { Memory = image.MemoryHandle, Size = image.MemorySize },
        };
        GRBackendRenderTarget backend = new(platform.Size.Width, platform.Size.Height, imageInfo);
        try
        {
            using var srgb = SKColorSpace.CreateSrgb();
            var surface = CreateSurface(context, backend, platform.IsYFlipped, platform.IsRgba, srgb)
                ?? throw new InvalidOperationException("Unable to create a Skia surface for the Vulkan swapchain image.");
            return new(context, backend, surface, platform);
        }
        catch
        {
            backend.Dispose();
            throw;
        }
    }

    /// <summary>Creates a surface using the swapchain's channel order and orientation.</summary>
    /// <param name="context">The shared context.</param>
    /// <param name="backend">The wrapped image.</param>
    /// <param name="isYFlipped">Whether the platform image is vertically flipped.</param>
    /// <param name="isRgba">Whether the image channels are RGBA.</param>
    /// <param name="srgb">The output colour space.</param>
    /// <returns>The surface or null when the image is unsupported.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SKSurface? CreateSurface(GRContext context, GRBackendRenderTarget backend, bool isYFlipped, bool isRgba, SKColorSpace srgb) =>
        SKSurface.Create(context, backend, isYFlipped ? GRSurfaceOrigin.TopLeft : GRSurfaceOrigin.BottomLeft, isRgba ? SKColorType.Rgba8888 : SKColorType.Bgra8888, srgb);

    /// <summary>Owns one swapchain image and submits its Skia commands before presentation.</summary>
    private sealed class VulkanRenderSession : ISkiaGpuRenderSession
    {
        /// <summary>The wrapped swapchain image.</summary>
        private readonly GRBackendRenderTarget _backend;

        /// <summary>The platform rendering session.</summary>
        private readonly IVulkanRenderSession _platform;

        /// <summary>Initializes a new instance of the <see cref="VulkanRenderSession"/> class.</summary>
        /// <param name="context">The shared context.</param>
        /// <param name="backend">The wrapped image.</param>
        /// <param name="surface">The Skia surface.</param>
        /// <param name="platform">The swapchain session.</param>
        internal VulkanRenderSession(GRContext context, GRBackendRenderTarget backend, SKSurface surface, IVulkanRenderSession platform)
        {
            GrContext = context;
            _backend = backend;
            SkSurface = surface;
            _platform = platform;
        }

        /// <inheritdoc/>
        public GRContext GrContext { get; }

        /// <inheritdoc/>
        public SKSurface SkSurface { get; }

        /// <inheritdoc/>
        public SKColorType ColorType => _platform.IsRgba ? SKColorType.Rgba8888 : SKColorType.Bgra8888;

        /// <inheritdoc/>
        public double ScaleFactor => _platform.Scaling;

        /// <inheritdoc/>
        public GRSurfaceOrigin SurfaceOrigin => _platform.IsYFlipped ? GRSurfaceOrigin.TopLeft : GRSurfaceOrigin.BottomLeft;

        /// <inheritdoc/>
        public void Dispose()
        {
            SkSurface.Canvas.Flush();
            SkSurface.Dispose();
            GrContext.Flush();
            GrContext.Submit();
            _platform.Dispose();
            _backend.Dispose();
        }
    }
}

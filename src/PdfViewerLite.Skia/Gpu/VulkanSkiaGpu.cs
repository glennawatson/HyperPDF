// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Vulkan;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Shares Avalonia's Vulkan device and graphics queue with Skia page rendering.</summary>
internal sealed class VulkanSkiaGpu : ISkiaGpu
{
    /// <summary>The Avalonia-owned Vulkan context.</summary>
    private readonly IVulkanPlatformGraphicsContext _platform;

    /// <summary>The Skia Vulkan context description, kept alive with its procedure lookup callback.</summary>
    private readonly GRVkBackendContext _backend;

    /// <summary>The Skia context for the compositor's Vulkan device.</summary>
    private readonly GRContext _graphicsContext;

    /// <summary>Initializes a new instance of the <see cref="VulkanSkiaGpu"/> class.</summary>
    /// <param name="platform">The compositor's Vulkan context.</param>
    /// <param name="maxResourceBytes">The optional Skia cache limit.</param>
    /// <param name="useStencilBuffers">Whether stencil buffers are requested.</param>
    /// <exception cref="InvalidOperationException">Skia cannot use the Vulkan device.</exception>
    internal VulkanSkiaGpu(IVulkanPlatformGraphicsContext platform, long? maxResourceBytes, bool? useStencilBuffers)
    {
        ArgumentNullException.ThrowIfNull(platform);
        _platform = platform;
        var device = platform.Device;
        using var current = platform.EnsureCurrent();
        _backend = new()
        {
            VkInstance = device.Instance.Handle,
            VkPhysicalDevice = device.PhysicalDeviceHandle,
            VkDevice = device.Handle,
            VkQueue = device.MainQueueHandle,
            GraphicsQueueIndex = device.GraphicsQueueFamilyIndex,
            GetProcedureAddress = LookupProcedure,
        };
        try
        {
            _graphicsContext = CreateContext(_backend, useStencilBuffers)
                ?? throw new InvalidOperationException("Unable to create a Skia context for the Vulkan device.");
            if (maxResourceBytes is { } limit)
            {
                _graphicsContext.SetResourceCacheLimit(limit);
            }
        }
        catch
        {
            _backend.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public bool IsLost => _platform.IsLost;

    /// <inheritdoc/>
    public IPlatformGraphicsContext PlatformGraphicsContext => _platform;

    /// <summary>Gets the context shared with the compositor.</summary>
    internal GRContext GrContext => _graphicsContext;

    /// <summary>Gets Avalonia's Vulkan context for render targets.</summary>
    internal IVulkanPlatformGraphicsContext Vulkan => _platform;

    /// <inheritdoc/>
    public ISkiaGpuRenderTarget? TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        return IsReadyToCreateRenderTarget(surfaces)
            ? new VulkanRenderTarget(this, _platform.CreateRenderTarget(surfaces))
            : null;
    }

    /// <inheritdoc/>
    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var factory = _platform.TryGetFeature<IVulkanKhrSurfacePlatformSurfaceFactory>();
        foreach (var surface in surfaces)
        {
            if (surface is IVulkanKhrSurfacePlatformSurface || factory?.CanRenderToSurface(_platform, surface) == true)
            {
                return surface.IsReady;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ISkiaSurface? TryCreateSurface(PixelSize size, ISkiaGpuRenderSession? session) => null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IScopedResource<GRContext> TryGetGrContext() => ScopedResource<GRContext>.Create(_graphicsContext, EnsureCurrent().Dispose);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable EnsureCurrent() => _platform.EnsureCurrent();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? TryGetFeature(Type featureType) => null;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsLost)
        {
            _graphicsContext.AbandonContext();
        }
        else
        {
            using var current = _platform.EnsureCurrent();
            _graphicsContext.AbandonContext(true);
        }

        SkiaGpuImageRetirement.Retire(_graphicsContext);
        _graphicsContext.Dispose();
        _backend.Dispose();
        _platform.Dispose();
    }

    /// <summary>Creates Skia's context from the compositor device and queue.</summary>
    /// <param name="backend">The Vulkan handles and procedure callback.</param>
    /// <param name="useStencilBuffers">Whether stencil buffers are requested.</param>
    /// <returns>The graphics context, or null when unsupported.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GRContext? CreateContext(GRVkBackendContext backend, bool? useStencilBuffers) =>
        GRContext.CreateVulkan(backend, new GRContextOptions { AvoidStencilBuffers = useStencilBuffers != true });

    /// <summary>Finds a Vulkan procedure through the active compositor instance and device.</summary>
    /// <param name="name">The procedure name.</param>
    /// <param name="instance">The requested instance, when provided.</param>
    /// <param name="device">The requested device, when provided.</param>
    /// <returns>The procedure address or zero.</returns>
    private nint LookupProcedure(string name, nint instance, nint device)
    {
        var api = _platform.Instance;
        var deviceAddress = device != 0 ? api.GetDeviceProcAddress(device, name) : 0;
        if (deviceAddress != 0)
        {
            return deviceAddress;
        }

        var instanceAddress = instance != 0 ? api.GetInstanceProcAddress(instance, name) : 0;
        return instanceAddress != 0 ? instanceAddress : api.GetInstanceProcAddress(0, name);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Metal;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Shares Avalonia's Metal device and command queue with Skia page rendering.</summary>
internal sealed class MetalSkiaGpu : ISkiaGpu
{
    /// <summary>The Avalonia-owned Metal device.</summary>
    private readonly IMetalDevice _device;

    /// <summary>The Skia context for that device.</summary>
    private readonly GRContext _graphicsContext;

    /// <summary>Initializes a new instance of the <see cref="MetalSkiaGpu"/> class.</summary>
    /// <param name="device">The compositor's Metal device.</param>
    /// <param name="maxResourceBytes">The optional Skia cache limit.</param>
    /// <param name="useStencilBuffers">Whether stencil buffers are requested.</param>
    /// <exception cref="InvalidOperationException">Skia cannot use the Metal device.</exception>
    internal MetalSkiaGpu(IMetalDevice device, long? maxResourceBytes, bool? useStencilBuffers)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        using var current = device.EnsureCurrent();
        using var backend = new GRMtlBackendContext { DeviceHandle = device.Device, QueueHandle = device.CommandQueue };
        _graphicsContext = CreateContext(backend, useStencilBuffers)
            ?? throw new InvalidOperationException("Unable to create a Skia context for the Metal device.");
        if (maxResourceBytes is { } limit)
        {
            _graphicsContext.SetResourceCacheLimit(limit);
        }
    }

    /// <inheritdoc/>
    public bool IsLost => _device.IsLost;

    /// <inheritdoc/>
    public IPlatformGraphicsContext PlatformGraphicsContext => _device;

    /// <summary>Gets the context shared with the compositor.</summary>
    internal GRContext GrContext => _graphicsContext;

    /// <inheritdoc/>
    public ISkiaGpuRenderTarget? TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        foreach (var surface in surfaces)
        {
            if (surface is IMetalPlatformSurface metalSurface)
            {
                return new MetalRenderTarget(this, metalSurface.CreateMetalRenderTarget(_device));
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        foreach (var surface in surfaces)
        {
            if (surface is IMetalPlatformSurface)
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
    public IDisposable EnsureCurrent() => _device.EnsureCurrent();

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
            using var current = _device.EnsureCurrent();
            _graphicsContext.AbandonContext(true);
        }

        SkiaGpuImageRetirement.Retire(_graphicsContext);
        _graphicsContext.Dispose();
    }

    /// <summary>Creates Skia's context for the compositor's device and queue.</summary>
    /// <param name="backend">The Metal handles.</param>
    /// <param name="useStencilBuffers">Whether stencil buffers are requested.</param>
    /// <returns>The graphics context, or null when unsupported.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static GRContext? CreateContext(GRMtlBackendContext backend, bool? useStencilBuffers) =>
        GRContext.CreateMetal(backend, new GRContextOptions { AvoidStencilBuffers = useStencilBuffers != true });
}

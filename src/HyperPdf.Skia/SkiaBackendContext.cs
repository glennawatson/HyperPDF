// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using HyperPdf.Skia.Bitmaps;
using HyperPdf.Skia.Gpu;
using HyperPdf.Skia.RenderTargets;

namespace HyperPdf.Skia;

/// <summary>Backend rendering context for the Skia rendering engine.</summary>
/// <param name="gpu">The owned graphics context, or null for software rendering.</param>
internal sealed class SkiaBackendContext(ISkiaGpu? gpu) : IPlatformRenderInterfaceContext
{
    /// <summary>The dpi scale factor.</summary>
    private const double DpiScaleFactor = 96.0;

    /// <summary>The gpu.</summary>
    private ISkiaGpu? _gpu = gpu;

    /// <inheritdoc/>
    public bool IsLost => _gpu?.IsLost ?? false;

    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, object> PublicFeatures { get; } = new Dictionary<Type, object>();

    /// <inheritdoc/>
    public PixelSize? MaxOffscreenRenderTargetPixelSize { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _gpu?.Dispose();
        _gpu = null;
    }

    /// <inheritdoc/>
    public IRenderTarget CreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var surfaceList = surfaces as IList<IPlatformRenderSurface> ?? [.. surfaces];
        if (_gpu?.TryCreateRenderTarget(surfaceList) is { } gpuRenderTarget)
        {
            return new SkiaGpuRenderTarget(_gpu, gpuRenderTarget);
        }

        foreach (var surface in surfaceList)
        {
            if (surface is IFramebufferPlatformSurface framebufferSurface)
            {
                return new FramebufferRenderTarget(framebufferSurface);
            }
        }

        throw new NotSupportedException("Unable to create a Skia render target from any of the provided surfaces.");
    }

    /// <inheritdoc/>
    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var surfaceList = surfaces as IList<IPlatformRenderSurface> ?? [.. surfaces];
        if (_gpu is not null)
        {
            return _gpu.IsReadyToCreateRenderTarget(surfaceList);
        }

        foreach (var surface in surfaceList)
        {
            if (surface is IFramebufferPlatformSurface)
            {
                return surface.IsReady;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, Vector scaling, bool enableTextAntialiasing)
    {
        using var gr = _gpu?.TryGetGrContext();
        var createInfo = new SurfaceRenderTargetCreateInfo
        {
            Width = pixelSize.Width,
            Height = pixelSize.Height,
            Dpi = scaling * DpiScaleFactor,
            Format = null,
            DisableTextLcdRendering = !enableTextAntialiasing,
            GrContext = gr?.Value,
            Gpu = _gpu,
            DisableManualFbo = true,
            Session = null,
        };
        return new SurfaceRenderTarget(createInfo);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? TryGetFeature(Type featureType) => _gpu?.TryGetFeature(featureType);
}

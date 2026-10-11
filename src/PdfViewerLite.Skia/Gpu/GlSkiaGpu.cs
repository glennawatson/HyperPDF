// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>OpenGL Skia GPU implementation.</summary>
internal sealed class GlSkiaGpu : ISkiaGpu
{
    /// <summary>The gr context.</summary>
    private readonly GRContext _graphicsContext;

    /// <summary>The gl context.</summary>
    private readonly IGlContext _openGlContext;

    /// <summary>Initializes a new instance of the <see cref = "GlSkiaGpu"/> class.</summary>
    /// <param name = "context">The OpenGL platform context.</param>
    /// <param name = "maxResourceBytes">Optional resource cache limit.</param>
    /// <param name = "useStencilBuffers">Optional stencil buffer preference.</param>
    public GlSkiaGpu(IGlContext context, long? maxResourceBytes, bool? useStencilBuffers)
    {
        ArgumentNullException.ThrowIfNull(context);
        _openGlContext = context;
        using (_openGlContext.EnsureCurrent())
        {
            var iface = context.Version.Type == GlProfileType.OpenGL ? GRGlInterface.CreateOpenGl(context.GlInterface.GetProcAddress) : GRGlInterface.CreateGles(context.GlInterface.GetProcAddress);
            using (iface)
            {
                _graphicsContext = GRContext.CreateGl(iface, new GRContextOptions { AvoidStencilBuffers = useStencilBuffers != true });
                if (maxResourceBytes.HasValue)
                {
                    _graphicsContext.SetResourceCacheLimit(maxResourceBytes.Value);
                }
            }
        }
    }

    /// <summary>Gets the underlying Skia GRContext.</summary>
    public GRContext GrContext => _graphicsContext;

    /// <summary>Gets the underlying OpenGL context.</summary>
    public IGlContext GlContext => _openGlContext;

    /// <inheritdoc/>
    public bool IsLost => _openGlContext.IsLost;

    /// <inheritdoc/>
    public IPlatformGraphicsContext? PlatformGraphicsContext => _openGlContext;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDisposable EnsureCurrent() => _openGlContext.EnsureCurrent();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IScopedResource<GRContext> TryGetGrContext() => ScopedResource<GRContext>.Create(GrContext, EnsureCurrent().Dispose);

    /// <inheritdoc/>
    public ISkiaGpuRenderTarget? TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        var customFactory = _openGlContext.TryGetFeature<IGlPlatformSurfaceRenderTargetFactory>();
        foreach (var surface in surfaces)
        {
            if (customFactory?.CanRenderToSurface(_openGlContext, surface) == true)
            {
                return new GlRenderTarget(_graphicsContext, _openGlContext, new SurfaceWrapper(surface));
            }

            if (surface is IGlPlatformSurface platformSurface)
            {
                return new GlRenderTarget(_graphicsContext, _openGlContext, platformSurface);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
    {
        var customFactory = _openGlContext.TryGetFeature<IGlPlatformSurfaceRenderTargetFactory>();
        foreach (var surface in surfaces)
        {
            if (customFactory?.CanRenderToSurface(_openGlContext, surface) == true || surface is IGlPlatformSurface)
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
    public object? TryGetFeature(Type featureType) => null;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_openGlContext.IsLost)
        {
            _graphicsContext.AbandonContext();
        }
        else
        {
            _graphicsContext.AbandonContext(true);
        }

        SkiaGpuImageRetirement.Retire(_graphicsContext);
        _graphicsContext.Dispose();
    }

    /// <summary>Owns the surface wrapper.</summary>
    /// <param name = "surface">The surface.</param>
    private sealed class SurfaceWrapper(IPlatformRenderSurface surface) : IGlPlatformSurface
    {
        /// <summary>The surface.</summary>
        private readonly IPlatformRenderSurface _surface = surface;

        /// <summary>Gets the is ready.</summary>
        public bool IsReady => _surface.IsReady;

        /// <summary>Returns the create gl render target.</summary>
        /// <param name = "context">The context.</param>
        /// <returns>The requested result.</returns>
        public IGlPlatformSurfaceRenderTarget CreateGlRenderTarget(IGlContext context)
        {
            var factory = context.TryGetFeature<IGlPlatformSurfaceRenderTargetFactory>()!;
            return factory.CreateRenderTarget(context, _surface);
        }
    }
}

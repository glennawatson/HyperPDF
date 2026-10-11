// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Metal;
using Avalonia.Platform;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Wraps the compositor's Metal drawable as a Skia render target.</summary>
internal sealed class MetalRenderTarget : ISkiaGpuRenderTarget
{
    /// <summary>The graphics context shared with the compositor.</summary>
    private readonly MetalSkiaGpu _gpu;

    /// <summary>The platform target.</summary>
    private IMetalPlatformSurfaceRenderTarget? _target;

    /// <summary>Initializes a new instance of the <see cref="MetalRenderTarget"/> class.</summary>
    /// <param name="gpu">The shared graphics context.</param>
    /// <param name="target">The compositor's drawable target.</param>
    internal MetalRenderTarget(MetalSkiaGpu gpu, IMetalPlatformSurfaceRenderTarget target)
    {
        _gpu = gpu;
        _target = target;
    }

    /// <inheritdoc/>
    public PlatformRenderTargetState State => Volatile.Read(ref _target)?.State ?? PlatformRenderTargetState.Disposed;

    /// <inheritdoc/>
    public ISkiaGpuRenderSession BeginRenderingSession(IRenderTarget.RenderTargetSceneInfo sceneInfo)
    {
        var platform = (Volatile.Read(ref _target) ?? throw new ObjectDisposedException(nameof(MetalRenderTarget))).BeginRendering();
        try
        {
            var size = platform.Size;
            if (size.Width <= 0 || size.Height <= 0 || platform.Scaling <= 0)
            {
                throw new InvalidOperationException($"Invalid Metal target size {size} or scaling {platform.Scaling}.");
            }

            return CreateSession(_gpu.GrContext, platform);
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

    /// <summary>Wraps a valid drawable and transfers ownership to the session.</summary>
    /// <param name="context">The shared context.</param>
    /// <param name="platform">The active drawable.</param>
    /// <returns>The owned rendering session.</returns>
    /// <exception cref="InvalidOperationException">The drawable format is unsupported.</exception>
    private static MetalRenderSession CreateSession(GRContext context, IMetalPlatformSurfaceRenderingSession platform)
    {
        GRBackendRenderTarget backend = new(platform.Size.Width, platform.Size.Height, new GRMtlTextureInfo(platform.Texture));
        try
        {
            using var srgb = SKColorSpace.CreateSrgb();
            var surface = CreateSurface(context, backend, platform.IsYFlipped, srgb)
                ?? throw new InvalidOperationException("Unable to create a Skia surface for the Metal drawable.");
            return new(context, backend, surface, platform);
        }
        catch
        {
            backend.Dispose();
            throw;
        }
    }

    /// <summary>Creates the drawable surface with the platform's orientation.</summary>
    /// <param name="context">The shared graphics context.</param>
    /// <param name="backend">The Metal drawable.</param>
    /// <param name="isYFlipped">Whether the drawable is vertically flipped.</param>
    /// <param name="srgb">The output colour space.</param>
    /// <returns>The surface or null when this drawable format is unsupported.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SKSurface? CreateSurface(GRContext context, GRBackendRenderTarget backend, bool isYFlipped, SKColorSpace srgb) =>
        SKSurface.Create(context, backend, isYFlipped ? GRSurfaceOrigin.BottomLeft : GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888, srgb);

    /// <summary>Owns one drawable and its Skia wrapper until command submission.</summary>
    private sealed class MetalRenderSession : ISkiaGpuRenderSession
    {
        /// <summary>The wrapped Metal texture.</summary>
        private readonly GRBackendRenderTarget _backend;

        /// <summary>The drawable session.</summary>
        private readonly IMetalPlatformSurfaceRenderingSession _platform;

        /// <summary>Initializes a new instance of the <see cref="MetalRenderSession"/> class.</summary>
        /// <param name="context">The shared graphics context.</param>
        /// <param name="backend">The wrapped drawable.</param>
        /// <param name="surface">The Skia surface.</param>
        /// <param name="platform">The drawable session.</param>
        internal MetalRenderSession(GRContext context, GRBackendRenderTarget backend, SKSurface surface, IMetalPlatformSurfaceRenderingSession platform)
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
        public SKColorType ColorType => SKColorType.Bgra8888;

        /// <inheritdoc/>
        public double ScaleFactor => _platform.Scaling;

        /// <inheritdoc/>
        public GRSurfaceOrigin SurfaceOrigin => _platform.IsYFlipped ? GRSurfaceOrigin.BottomLeft : GRSurfaceOrigin.TopLeft;

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

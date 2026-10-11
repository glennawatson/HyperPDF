// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Surfaces;
using Avalonia.Platform;
using SkiaSharp;
using static Avalonia.OpenGL.GlConsts;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>OpenGL-backed Skia GPU render target.</summary>
internal sealed class GlRenderTarget : ISkiaGpuRenderTarget
{
    /// <summary>The gl rgba8format.</summary>
    private const uint GlRgba8Format = 0x8058;

    /// <summary>The surface props.</summary>
    private static readonly SKSurfaceProperties SurfaceProps = new(SKPixelGeometry.RgbHorizontal);

    /// <summary>The surface.</summary>
    private readonly IGlPlatformSurfaceRenderTarget _surface;

    /// <summary>Initializes a new instance of the <see cref = "GlRenderTarget"/> class.</summary>
    /// <param name = "graphicsContext">The Skia GPU context.</param>
    /// <param name = "openGlContext">The OpenGL context.</param>
    /// <param name = "platformSurface">The OpenGL surface.</param>
    public GlRenderTarget(GRContext graphicsContext, IGlContext openGlContext, IGlPlatformSurface platformSurface)
    {
        ArgumentNullException.ThrowIfNull(openGlContext);
        ArgumentNullException.ThrowIfNull(platformSurface);
        GraphicsContext = graphicsContext;
        using (openGlContext.EnsureCurrent())
        {
            _surface = platformSurface.CreateGlRenderTarget(openGlContext);
        }
    }

    /// <inheritdoc/>
    public PlatformRenderTargetState State => _surface.State;

    /// <summary>Gets the gr context.</summary>
    private GRContext GraphicsContext { get; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _surface.Dispose();

    /// <inheritdoc/>
    public ISkiaGpuRenderSession BeginRenderingSession(IRenderTarget.RenderTargetSceneInfo sceneInfo)
    {
        var platformSession = _surface.BeginDraw(sceneInfo);
        var success = false;
        try
        {
            var disp = platformSession.Context;
            disp.GlInterface.GetIntegerv(GL_FRAMEBUFFER_BINDING, out var fb);
            var size = platformSession.Size;
            const SKColorType colorType = SKColorType.Rgba8888;
            var scaling = platformSession.Scaling;
            if (size.Width <= 0 || size.Height <= 0 || scaling < 0)
            {
                platformSession.Dispose();
                throw new InvalidOperationException($"Invalid drawing context surface size {size} and scaling {scaling}");
            }

            lock (GraphicsContext)
            {
                GraphicsContext.ResetContext();
                var samples = disp.SampleCount;
                var maxSamples = GraphicsContext.GetMaxSurfaceSampleCount(colorType);
                if (samples > maxSamples)
                {
                    samples = maxSamples;
                }

                var framebufferInfo = new GRGlFramebufferInfo((uint)fb, GlRgba8Format);
                var renderTarget = new GRBackendRenderTarget(size.Width, size.Height, samples, disp.StencilSize, framebufferInfo);
                var surface = SKSurface.Create(
                    GraphicsContext,
                    renderTarget,
                    platformSession.IsYFlipped ? GRSurfaceOrigin.TopLeft : GRSurfaceOrigin.BottomLeft,
                    colorType,
                    SurfaceProps);
                success = true;
                return new GlGpuSession(GraphicsContext, renderTarget, surface, platformSession);
            }
        }
        finally
        {
            if (!success)
            {
                platformSession.Dispose();
            }
        }
    }

    /// <summary>Owns the gl gpu session.</summary>
    /// <param name = "graphicsContext">The gr context.</param>
    /// <param name = "backendRenderTarget">The backend render target.</param>
    /// <param name = "surface">The surface.</param>
    /// <param name = "platformSession">The gl session.</param>
    private sealed class GlGpuSession(
        GRContext graphicsContext,
        GRBackendRenderTarget backendRenderTarget,
        SKSurface surface,
        IGlPlatformSurfaceRenderingSession platformSession) : ISkiaGpuRenderSession
    {
        /// <summary>The backend render target.</summary>
        private readonly GRBackendRenderTarget _backendRenderTarget = backendRenderTarget;

        /// <summary>The surface.</summary>
        private readonly SKSurface _surface = surface;

        /// <summary>The gl session.</summary>
        private readonly IGlPlatformSurfaceRenderingSession _platformSession = platformSession;

        /// <summary>Gets the gr context.</summary>
        public GRContext GrContext => GraphicsContext;

        /// <summary>Gets the sk surface.</summary>
        public SKSurface SkSurface => _surface;

        /// <inheritdoc/>
        public SKColorType ColorType => SKColorType.Rgba8888;

        /// <summary>Gets the scale factor.</summary>
        public double ScaleFactor => _platformSession.Scaling;

        /// <summary>Gets the surface origin.</summary>
        public GRSurfaceOrigin SurfaceOrigin => _platformSession.IsYFlipped ? GRSurfaceOrigin.TopLeft : GRSurfaceOrigin.BottomLeft;

        /// <summary>Gets the gr context.</summary>
        private GRContext GraphicsContext { get; } = graphicsContext;

        /// <inheritdoc/>
        public void Dispose()
        {
            _surface.Canvas.Flush();
            _surface.Dispose();
            _backendRenderTarget.Dispose();
            GraphicsContext.Flush();
            _platformSession.Dispose();
        }
    }
}

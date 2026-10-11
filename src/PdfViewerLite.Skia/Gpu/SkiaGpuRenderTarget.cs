// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Platform;
using PdfViewerLite.Skia.Rendering;
using SkiaSharp;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Adapts an <see cref = "ISkiaGpuRenderTarget"/> to Avalonia's <see cref = "IRenderTarget"/> pipeline.</summary>
/// <param name="skiaGpu">The shared graphics context.</param>
/// <param name="renderTarget">The owned render target.</param>
internal sealed class SkiaGpuRenderTarget(ISkiaGpu skiaGpu, ISkiaGpuRenderTarget renderTarget) : IRenderTarget
{
    /// <summary>The render target.</summary>
    private readonly ISkiaGpuRenderTarget _renderTarget = renderTarget;

    /// <inheritdoc/>
    public PlatformRenderTargetState PlatformRenderTargetState => _renderTarget.State;

    /// <inheritdoc/>
    public RenderTargetProperties Properties { get; }

    /// <summary>Gets the skia gpu.</summary>
    private ISkiaGpu SkiaGpu { get; } = skiaGpu;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _renderTarget.Dispose();

    /// <inheritdoc/>
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        properties = default;
        var trace = GpuFrameEventSource.Log.IsEnabled();
        var started = trace ? Stopwatch.GetTimestamp() : 0;
        var session = _renderTarget.BeginRenderingSession(sceneInfo);
        if (trace)
        {
            session = new TimedGpuRenderSession(session, started);
        }

        try
        {
            SkiaGpuImageRetirement.Drain(session.GrContext);
            var createInfo = new DrawingContextImpl.CreateInfo
            {
                GrContext = session.GrContext,
                Surface = session.SkSurface,
                SurfaceColorType = session.ColorType,
                Dpi = SkiaPlatform.DefaultDpi * session.ScaleFactor,
                ScaleDrawingToDpi = false,
                Gpu = SkiaGpu,
                CurrentSession = session,
            };
            return new DrawingContextImpl(createInfo, session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>Measures a compositor session only while EventPipe is collecting frames.</summary>
    private sealed class TimedGpuRenderSession : ISkiaGpuRenderSession
    {
        /// <summary>The platform rendering session.</summary>
        private readonly ISkiaGpuRenderSession _inner;

        /// <summary>The timestamp before target acquisition.</summary>
        private readonly long _started;

        /// <summary>The backend copied before platform disposal.</summary>
        private readonly int _backend;

        /// <summary>One after this wrapper is disposed.</summary>
        private int _disposed;

        /// <summary>Initializes a new instance of the <see cref="TimedGpuRenderSession"/> class.</summary>
        /// <param name="inner">The owned platform session.</param>
        /// <param name="started">The timestamp before target acquisition.</param>
        internal TimedGpuRenderSession(ISkiaGpuRenderSession inner, long started)
        {
            _inner = inner;
            _started = started;
            _backend = (int)inner.GrContext.Backend;
        }

        /// <inheritdoc/>
        public GRContext GrContext => _inner.GrContext;

        /// <inheritdoc/>
        public SKSurface SkSurface => _inner.SkSurface;

        /// <inheritdoc/>
        public SKColorType ColorType => _inner.ColorType;

        /// <inheritdoc/>
        public double ScaleFactor => _inner.ScaleFactor;

        /// <inheritdoc/>
        public GRSurfaceOrigin SurfaceOrigin => _inner.SurfaceOrigin;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                _inner.Dispose();
            }
            finally
            {
                var submitted = (int)Stopwatch.GetElapsedTime(_started).TotalMicroseconds;
                GpuFrameEventSource.Log.FrameSessionSubmitted(_backend, submitted);
            }
        }
    }
}

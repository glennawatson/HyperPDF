// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Skia.Gpu;
using HyperPdf.Skia.Rendering;
using SkiaSharp;

namespace HyperPdf.Skia.Bitmaps;

/// <summary>Skia render target that renders to an in-memory surface or GPU surface.</summary>
internal sealed class SurfaceRenderTarget : IDrawableBitmapImpl, IDrawingContextLayerWithRenderContextAffinityImpl
{
    /// <summary>The default jpeg quality.</summary>
    private const int DefaultJpegQuality = 100;

    /// <summary>The surface props.</summary>
    private static readonly SKSurfaceProperties SurfaceProps = new(SKPixelGeometry.RgbHorizontal);

    /// <summary>The use scaled drawing.</summary>
    private readonly bool _useScaledDrawing;

    /// <summary>The surface.</summary>
    private readonly ISkiaSurface _surface;

    /// <summary>The canvas.</summary>
    private readonly SKCanvas _canvas;

    /// <summary>The disable lcd rendering.</summary>
    private readonly bool _disableLcdRendering;

    /// <summary>The gr context.</summary>
    private readonly GRContext? _graphicsContext;

    /// <summary>The gpu.</summary>
    private readonly ISkiaGpu? _gpu;

    /// <summary>Initializes a new instance of the <see cref = "SurfaceRenderTarget"/> class.</summary>
    /// <param name = "createInfo">Creation parameters.</param>
    /// <exception cref = "InvalidOperationException">Thrown when <c>surface?.Surface.Canvas is not { } canvas</c>.</exception>
    public SurfaceRenderTarget(in SurfaceRenderTargetCreateInfo createInfo)
    {
        _useScaledDrawing = createInfo.UseScaledDrawing;
        PixelSize = new(createInfo.Width, createInfo.Height);
        Dpi = createInfo.Dpi;
        _disableLcdRendering = createInfo.DisableTextLcdRendering;
        _graphicsContext = createInfo.GrContext;
        _gpu = createInfo.Gpu;
        ISkiaSurface? surface = null;
        if (!createInfo.DisableManualFbo)
        {
            surface = _gpu?.TryCreateSurface(PixelSize, createInfo.Session);
        }

        if (surface is null && CreateSurface(createInfo.GrContext, PixelSize.Width, PixelSize.Height, createInfo.Format) is { } skSurface)
        {
            surface = new SkiaSurfaceWrapper(skSurface);
        }

        if (surface?.Surface.Canvas is not { } canvas)
        {
            throw new InvalidOperationException("Failed to create Skia render target surface.");
        }

        _surface = surface;
        _canvas = canvas;
    }

    /// <inheritdoc/>
    public Vector Dpi { get; }

    /// <inheritdoc/>
    public PixelSize PixelSize { get; }

    /// <inheritdoc/>
    public int Version { get; private set; } = 1;

    /// <inheritdoc/>
    public bool CanBlit => true;

    /// <inheritdoc/>
    public bool IsCorrupted => _gpu?.IsLost == true;

    /// <inheritdoc/>
    public bool HasRenderContextAffinity => _graphicsContext is not null;

    /// <inheritdoc/>
    public void Blit(IDrawingContextImpl context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var drawing = (DrawingContextImpl)context;
        if (_surface.CanBlit)
        {
            _surface.Surface.Canvas.Flush();
            _surface.Blit(drawing.Canvas);
        }
        else
        {
            var oldMatrix = drawing.Canvas.TotalMatrix;
            drawing.Canvas.ResetMatrix();
            _surface.Surface.Draw(drawing.Canvas, 0, 0, null);
            drawing.Canvas.SetMatrix(oldMatrix);
        }
    }

    /// <inheritdoc/>
    public void Draw(DrawingContextImpl context, SKRect sourceRect, SKRect destRect, SKSamplingOptions samplingOptions, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var image = SnapshotImage();
        context.Canvas.DrawImage(image, sourceRect, destRect, samplingOptions, paint);
    }

    /// <inheritdoc/>
    public IDrawingContextImpl CreateDrawingContext()
    {
        _canvas.RestoreToCount(-1);
        _canvas.ResetMatrix();
        var createInfo = new DrawingContextCreateInfo
        {
            Surface = _surface.Surface,
            Dpi = Dpi,
            ScaleDrawingToDpi = _useScaledDrawing,
            DisableSubpixelTextRendering = _disableLcdRendering,
            GrContext = _graphicsContext,
            Gpu = _gpu,
        };
        return new DrawingContextImpl(createInfo, DisposeAction.Create(() => Version++));
    }

    /// <inheritdoc/>
    public IBitmapImpl CreateNonAffinedSnapshot()
    {
        if (!HasRenderContextAffinity)
        {
            throw new InvalidOperationException("Surface does not have render context affinity.");
        }

        using var image = SnapshotImage();
        return new ImmutableBitmap(image.ToRasterImage(true));
    }

    /// <inheritdoc/>
    public void Save(Stream stream, BitmapEncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var image = SnapshotImage();
        var format = options is JpegBitmapEncoderOptions ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        var quality = (options as JpegBitmapEncoderOptions)?.Quality ?? DefaultJpegQuality;
        using var data = image.Encode(format, quality) ?? throw new InvalidOperationException("Failed to encode image.");
        data.SaveTo(stream);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _canvas.Dispose();
        _surface.Dispose();
    }

    /// <summary>Creates a snapshot of the current surface contents.</summary>
    /// <returns>A Skia image snapshot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SKImage SnapshotImage() => _surface.Surface.Snapshot();

    /// <summary>Creates a surface compatible with the target pixels.</summary>
    /// <param name = "gpu">The gpu.</param>
    /// <param name = "width">The width.</param>
    /// <param name = "height">The height.</param>
    /// <param name = "format">The format.</param>
    /// <returns>The requested result.</returns>
    private static SKSurface? CreateSurface(GRContext? gpu, int width, int height, PixelFormat? format)
    {
        var colorType = format?.ToSkColorType() ?? SKImageInfo.PlatformColorType;
        var imageInfo = new SKImageInfo(Math.Max(width, 1), Math.Max(height, 1), colorType, SKAlphaType.Premul);
        return gpu is not null ? SKSurface.Create(gpu, false, imageInfo, SurfaceProps) : SKSurface.Create(imageInfo, SurfaceProps);
    }

    /// <summary>Owns the skia surface wrapper.</summary>
    /// <param name = "surface">The surface.</param>
    private sealed class SkiaSurfaceWrapper(SKSurface surface) : ISkiaSurface
    {
        /// <summary>The surface.</summary>
        private SKSurface? _surface = surface;

        /// <summary>Gets the surface.</summary>
        public SKSurface Surface => _surface ?? throw new ObjectDisposedException(nameof(SkiaSurfaceWrapper));

        /// <summary>Gets the can blit.</summary>
        public bool CanBlit => false;

        /// <inheritdoc/>
        public void Blit(SKCanvas canvas) => throw new NotSupportedException();

        /// <inheritdoc/>
        public void Dispose()
        {
            _surface?.Dispose();
            _surface = null;
        }
    }
}

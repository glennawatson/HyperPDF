// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Rendering.Composition;
using HyperPdf.Skia.RenderTargets;
using HyperPdf.Skia.Rendering;
using SkiaSharp;

namespace HyperPdf.Skia.Bitmaps;

/// <summary>Render target bitmap implementation backed by a Skia framebuffer.</summary>
internal sealed class RenderTargetBitmapImpl : IRenderTargetBitmapImpl, IDrawableBitmapImpl, IFramebufferPlatformSurface
{
    /// <summary>The dpi scale factor.</summary>
    private const double DpiScaleFactor = 96.0;

    /// <summary>The owned pixel buffer.</summary>
    private readonly WriteableBitmapImpl _bitmap;

    /// <summary>The render target.</summary>
    private FramebufferRenderTarget? _renderTarget;

    /// <summary>Initializes a new instance of the <see cref = "RenderTargetBitmapImpl"/> class.</summary>
    /// <param name = "size">Bitmap size in pixels.</param>
    /// <param name = "dpi">Bitmap DPI.</param>
    public RenderTargetBitmapImpl(PixelSize size, Vector dpi)
    {
        var format = SKImageInfo.PlatformColorType == SKColorType.Rgba8888 ? PixelFormat.Rgba8888 : PixelFormat.Bgra8888;
        _bitmap = new(size, dpi, format, Avalonia.Platform.AlphaFormat.Premul);
    }

    /// <inheritdoc />
    public PixelSize PixelSize => _bitmap.PixelSize;

    /// <inheritdoc />
    public Vector Dpi => _bitmap.Dpi;

    /// <inheritdoc />
    public int Version => _bitmap.Version;

    /// <inheritdoc />
    public PixelFormat? Format => _bitmap.Format;

    /// <inheritdoc />
    public AlphaFormat? AlphaFormat => _bitmap.AlphaFormat;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDrawingContextImpl CreateDrawingContext() =>
        (_renderTarget ??= new(this, true)).CreateDrawingContext(new(PixelSize, Dpi.X / DpiScaleFactor, CompositionTransparencyLevel.None), out _);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IFramebufferRenderTarget CreateFramebufferRenderTarget() => new FuncFramebufferRenderTarget(Lock);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ILockedFramebuffer Lock() => _bitmap.Lock();

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Save(Stream stream, Avalonia.Media.Imaging.BitmapEncoderOptions options) => _bitmap.Save(stream, options);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Draw(DrawingContextImpl context, SKRect sourceRect, SKRect destRect, SKSamplingOptions samplingOptions, SKPaint paint) =>
        _bitmap.Draw(context, sourceRect, destRect, samplingOptions, paint);

    /// <inheritdoc/>
    public void Dispose()
    {
        _renderTarget?.Dispose();
        _bitmap.Dispose();
    }
}

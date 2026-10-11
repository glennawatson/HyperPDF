// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Metal;
using Avalonia.OpenGL;
using Avalonia.Platform;
using Avalonia.Vulkan;
using PdfViewerLite.Skia.Bitmaps;
using PdfViewerLite.Skia.Fonts;
using PdfViewerLite.Skia.Geometry;
using PdfViewerLite.Skia.Gpu;
using SkiaSharp;

namespace PdfViewerLite.Skia;

/// <summary>Platform render interface providing Skia-based geometry, bitmap, and context creation.</summary>
internal sealed class PlatformRenderInterface : IPlatformRenderInterface
{
    /// <summary>The max resource bytes.</summary>
    private readonly long? _maxResourceBytes;

    /// <summary>The use stencil buffers.</summary>
    private readonly bool? _useStencilBuffers;

    /// <summary>Initializes a new instance of the <see cref = "PlatformRenderInterface"/> class.</summary>
    /// <param name = "maxResourceBytes">Optional GPU resource cache size limit.</param>
    /// <param name = "useStencilBuffers">Optional stencil buffer preference.</param>
    public PlatformRenderInterface(long? maxResourceBytes = null, bool? useStencilBuffers = null)
    {
        _maxResourceBytes = maxResourceBytes;
        _useStencilBuffers = useStencilBuffers;
        DefaultPixelFormat = SKImageInfo.PlatformColorType.ToAvalonia() ?? PixelFormat.Rgba8888;
    }

    /// <inheritdoc/>
    public bool SupportsIndividualRoundRects => true;

    /// <inheritdoc/>
    public AlphaFormat DefaultAlphaFormat => AlphaFormat.Premul;

    /// <inheritdoc/>
    public PixelFormat DefaultPixelFormat { get; }

    /// <inheritdoc/>
    public bool SupportsRegions => true;

    /// <inheritdoc/>
    public IPlatformRenderInterfaceContext CreateBackendContext(IPlatformGraphicsContext? graphicsApiContext)
    {
        if (graphicsApiContext is null)
        {
            return new SkiaBackendContext(null);
        }

        if (graphicsApiContext is ISkiaGpu skiaGpu)
        {
            return new SkiaBackendContext(skiaGpu);
        }

        if (graphicsApiContext is IGlContext gl)
        {
            return new SkiaBackendContext(new GlSkiaGpu(gl, _maxResourceBytes, _useStencilBuffers));
        }

        if (graphicsApiContext is IMetalDevice metal)
        {
            return new SkiaBackendContext(new MetalSkiaGpu(metal, _maxResourceBytes, _useStencilBuffers));
        }

        if (graphicsApiContext is IVulkanPlatformGraphicsContext vulkan)
        {
            return new SkiaBackendContext(new VulkanSkiaGpu(vulkan, _maxResourceBytes, _useStencilBuffers));
        }

        throw new ArgumentException("Graphics context type is not supported.", nameof(graphicsApiContext));
    }

    /// <inheritdoc/>
    public bool IsSupportedBitmapPixelFormat(PixelFormat format) => format == PixelFormat.Rgb565 || format == PixelFormat.Bgra8888 || format == PixelFormat.Rgba8888;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPlatformRenderInterfaceRegion CreateRegion() => new SkiaRegionImpl();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IGeometryImpl CreateEllipseGeometry(Rect rect) => new EllipseGeometryImpl(rect);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IGeometryImpl CreateLineGeometry(Point p1, Point p2) => new LineGeometryImpl(p1, p2);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IGeometryImpl CreateRectangleGeometry(Rect rect) => new RectangleGeometryImpl(rect);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IStreamGeometryImpl CreateStreamGeometry() => new StreamGeometryImpl();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IGeometryImpl CreateGeometryGroup(FillRule fillRule, IReadOnlyList<IGeometryImpl> children) => new GeometryGroupImpl(fillRule, children);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IGeometryImpl CreateCombinedGeometry(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2) => CombinedGeometryImpl.ForceCreate(combineMode, g1, g2);

    /// <inheritdoc/>
    public IGeometryImpl BuildGlyphRunGeometry(GlyphRun glyphRun)
    {
        ArgumentNullException.ThrowIfNull(glyphRun);
        if (glyphRun.GlyphTypeface.PlatformTypeface is not SkiaTypeface glyphTypeface)
        {
            throw new InvalidOperationException("Platform typeface must be a SkiaTypeface.");
        }

        using var font = glyphTypeface.CreateSKFont((float)glyphRun.FontRenderingEmSize);
        font.Hinting = SKFontHinting.None;
        using var builder = new SKPathBuilder();
        var (currentX, currentY) = glyphRun.BaselineOrigin;
        for (var i = 0; i < glyphRun.GlyphInfos.Count; i++)
        {
            var glyph = glyphRun.GlyphInfos[i].GlyphIndex;
            var glyphPath = font.GetGlyphPath(glyph);
            if (glyphPath is not null && !glyphPath.IsEmpty)
            {
                builder.AddPath(glyphPath, (float)currentX, (float)currentY);
            }

            currentX += glyphRun.GlyphInfos[i].GlyphAdvance;
        }

        var path = builder.Detach();
        return new StreamGeometryImpl(path, path);
    }

    /// <inheritdoc/>
    public IBitmapImpl LoadBitmap(string fileName)
    {
        using var stream = File.OpenRead(fileName);
        return LoadBitmap(stream);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IBitmapImpl LoadBitmap(Stream stream) => new ImmutableBitmap(stream);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IBitmapImpl LoadBitmap(PixelFormat format, AlphaFormat alphaFormat, IntPtr data, PixelSize size, Vector dpi, int stride) => new ImmutableBitmap(
        size,
        dpi,
        stride,
        format,
        alphaFormat,
        data);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IWriteableBitmapImpl LoadWriteableBitmapToWidth(
        Stream stream,
        int width,
        BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) => new WriteableBitmapImpl(stream, width, true, interpolationMode);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IWriteableBitmapImpl LoadWriteableBitmapToHeight(
        Stream stream,
        int height,
        BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) => new WriteableBitmapImpl(stream, height, false, interpolationMode);

    /// <inheritdoc/>
    public IWriteableBitmapImpl LoadWriteableBitmap(string fileName)
    {
        using var stream = File.OpenRead(fileName);
        return LoadWriteableBitmap(stream);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IWriteableBitmapImpl LoadWriteableBitmap(Stream stream) => new WriteableBitmapImpl(stream);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IBitmapImpl LoadBitmapToWidth(
        Stream stream,
        int width,
        BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) => new ImmutableBitmap(stream, width, true, interpolationMode);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IBitmapImpl LoadBitmapToHeight(
        Stream stream,
        int height,
        BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality) => new ImmutableBitmap(stream, height, false, interpolationMode);

    /// <inheritdoc/>
    public IBitmapImpl ResizeBitmap(IBitmapImpl bitmapImpl, PixelSize destinationSize, BitmapInterpolationMode interpolationMode = BitmapInterpolationMode.HighQuality)
    {
        if (bitmapImpl is ImmutableBitmap ibmp)
        {
            return new ImmutableBitmap(ibmp, destinationSize, interpolationMode);
        }

        throw new ArgumentException("Invalid source bitmap type for resize.", nameof(bitmapImpl));
    }

    /// <inheritdoc/>
    public IRenderTargetBitmapImpl CreateRenderTargetBitmap(PixelSize size, Vector dpi)
    {
        if (size.Width < 1)
        {
            throw new ArgumentException("Width can't be less than 1.", nameof(size));
        }

        if (size.Height < 1)
        {
            throw new ArgumentException("Height can't be less than 1.", nameof(size));
        }

        return new RenderTargetBitmapImpl(size, dpi);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IWriteableBitmapImpl CreateWriteableBitmap(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat) => new WriteableBitmapImpl(size, dpi, format, alphaFormat);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IGlyphRunImpl CreateGlyphRun(
        GlyphTypeface glyphTypeface,
        double fontRenderingEmSize,
        IReadOnlyList<GlyphInfo> glyphInfos,
        Point baselineOrigin) => new GlyphRunImpl(glyphTypeface, fontRenderingEmSize, glyphInfos, baselineOrigin);
}

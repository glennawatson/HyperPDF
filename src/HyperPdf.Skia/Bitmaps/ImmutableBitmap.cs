// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Skia.Rendering;
using SkiaSharp;

namespace HyperPdf.Skia.Bitmaps;

/// <summary>Immutable Skia bitmap wrapping an <see cref = "SKImage"/> with zero-copy rendering support.</summary>
internal sealed class ImmutableBitmap : IDrawableBitmapImpl, IReadableBitmapImpl
{
    /// <summary>The default PNG compression quality.</summary>
    private const int DefaultQuality = 100;

    /// <summary>The underlying Skia image.</summary>
    private readonly SKImage _image;

    /// <summary>The underlying Skia bitmap, if retained.</summary>
    private readonly SKBitmap? _bitmap;

    /// <summary>The borrowed raster pixels retained by the image.</summary>
    private readonly SKPixmap? _pixels;

    /// <summary>Custom dispose callback.</summary>
    private readonly Action? _customDispose;

    /// <summary>Initializes a new instance of the <see cref = "ImmutableBitmap"/> class from an encoded stream.</summary>
    /// <param name = "stream">The encoded image stream.</param>
    /// <exception cref = "ArgumentException">Thrown when <c>SKBitmap.Decode(data)</c> is <see langword="null"/>.</exception>
    public ImmutableBitmap(Stream stream)
    {
        using var managedStream = new SKManagedStream(stream);
        using var data = SKData.Create(managedStream);
        _bitmap = SKBitmap.Decode(data) ?? throw new ArgumentException("Unable to decode bitmap from stream.", nameof(stream));
        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
        PixelSize = new(_image.Width, _image.Height);
        Dpi = SkiaPlatform.DefaultDpi;
    }

    /// <summary>Initializes a new instance of the <see cref = "ImmutableBitmap"/> class.</summary>
    /// <param name = "stream">The encoded image.</param>
    /// <param name = "targetDimension">The requested width or height.</param>
    /// <param name = "isWidth">Whether the width is fixed.</param>
    /// <param name = "interpolationMode">The sampling mode.</param>
    /// <exception cref = "ArgumentException">Thrown when <c>SKBitmap.Decode(stream)</c> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The bitmap could not be scaled.</exception>
    public ImmutableBitmap(Stream stream, int targetDimension, bool isWidth, BitmapInterpolationMode interpolationMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetDimension);
        using var source = SKBitmap.Decode(stream) ?? throw new ArgumentException("Unable to decode bitmap.", nameof(stream));
        var width = isWidth ? targetDimension : Math.Max(1, (int)Math.Round(source.Width * ((double)targetDimension / source.Height)));
        var height = isWidth ? Math.Max(1, (int)Math.Round(source.Height * ((double)targetDimension / source.Width))) : targetDimension;
        _bitmap = new(new SKImageInfo(width, height, source.ColorType, source.AlphaType));
        using var pixels = _bitmap.PeekPixels();
        if (!source.ScalePixels(pixels, interpolationMode.ToSKSamplingOptions(width > source.Width || height > source.Height)))
        {
            _bitmap.Dispose();
            throw new InvalidOperationException("Unable to scale bitmap.");
        }

        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
        PixelSize = new(width, height);
        Dpi = SkiaPlatform.DefaultDpi;
    }

    /// <summary>Initializes a new instance of the <see cref = "ImmutableBitmap"/> class from an existing <see cref = "SKImage"/>.</summary>
    /// <param name = "image">The Skia image.</param>
    /// <param name = "customDispose">Optional custom disposal action.</param>
    /// <exception cref = "ArgumentNullException">Thrown when <c>image</c> is <see langword="null"/>.</exception>
    public ImmutableBitmap(SKImage image, Action? customDispose = null)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _pixels = image.PeekPixels();
        _customDispose = customDispose;
        PixelSize = new(image.Width, image.Height);
        Dpi = SkiaPlatform.DefaultDpi;
    }

    /// <summary>Initializes a new instance of the <see cref = "ImmutableBitmap"/> class scaled from another bitmap.</summary>
    /// <param name = "source">The source bitmap.</param>
    /// <param name = "destinationSize">The destination pixel size.</param>
    /// <param name = "interpolationMode">The interpolation mode.</param>
    public ImmutableBitmap(ImmutableBitmap source, PixelSize destinationSize, BitmapInterpolationMode interpolationMode)
    {
        ArgumentNullException.ThrowIfNull(source);
        var isUpscaling = destinationSize.Width > source.PixelSize.Width || destinationSize.Height > source.PixelSize.Height;
        var info = new SKImageInfo(destinationSize.Width, destinationSize.Height, SKColorType.Bgra8888);
        _bitmap = new(info);
        using var pixels = _bitmap.PeekPixels();
        _ = source._image.ScalePixels(pixels, interpolationMode.ToSKSamplingOptions(isUpscaling));
        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
        PixelSize = destinationSize;
        Dpi = source.Dpi;
    }

    /// <summary>Initializes a new instance of the <see cref = "ImmutableBitmap"/> class by copying pixel bytes.</summary>
    /// <param name = "size">The size in pixels.</param>
    /// <param name = "dpi">The DPI.</param>
    /// <param name = "stride">The row stride in bytes.</param>
    /// <param name = "format">The pixel format.</param>
    /// <param name = "alphaFormat">The alpha format.</param>
    /// <param name = "data">The pointer to raw pixel data.</param>
    /// <exception cref = "InvalidOperationException">Thrown when <c>!_bitmap.TryAllocPixels(info)</c>.</exception>
    public ImmutableBitmap(PixelSize size, Vector dpi, int stride, PixelFormat format, AlphaFormat alphaFormat, IntPtr data)
    {
        var info = new SKImageInfo(size.Width, size.Height, format.ToSkColorType(), alphaFormat.ToSkAlphaType());
        _bitmap = new();
        if (!_bitmap.TryAllocPixels(info))
        {
            _bitmap.Dispose();
            throw new InvalidOperationException("Failed to allocate bitmap memory.");
        }

        unsafe
        {
            var srcPtr = (byte*)data;
            var dstPtr = (byte*)_bitmap.GetPixels();
            var copyRows = size.Height;
            var bytesPerRow = Math.Min(stride, _bitmap.RowBytes);
            for (var y = 0; y < copyRows; y++)
            {
                Buffer.MemoryCopy(srcPtr + (y * stride), dstPtr + (y * _bitmap.RowBytes), _bitmap.RowBytes, bytesPerRow);
            }
        }

        _bitmap.SetImmutable();
        _image = SKImage.FromBitmap(_bitmap);
        PixelSize = size;
        Dpi = dpi;
    }

    /// <inheritdoc/>
    public Vector Dpi { get; }

    /// <inheritdoc/>
    public PixelSize PixelSize { get; }

    /// <inheritdoc/>
    public int Version => 1;

    /// <inheritdoc/>
    public PixelFormat? Format => (_bitmap?.ColorType ?? _pixels?.ColorType)?.ToAvalonia();

    /// <inheritdoc/>
    public AlphaFormat? AlphaFormat => (_bitmap?.AlphaType ?? _pixels?.AlphaType)?.ToAlphaFormat();

    /// <inheritdoc/>
    public void Draw(DrawingContextImpl context, SKRect sourceRect, SKRect destRect, SKSamplingOptions samplingOptions, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Canvas.DrawImage(_image, sourceRect, destRect, samplingOptions, paint);
    }

    /// <inheritdoc/>
    public void Save(Stream stream, BitmapEncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var format = options is JpegBitmapEncoderOptions ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        var quality = (options as JpegBitmapEncoderOptions)?.Quality ?? DefaultQuality;
        using var data = _image.Encode(format, quality) ?? throw new InvalidOperationException("Failed to encode image.");
        data.SaveTo(stream);
    }

    /// <inheritdoc/>
    public ILockedFramebuffer Lock()
    {
        if (_bitmap is null && _pixels is null)
        {
            throw new NotSupportedException("Bitmap pixel buffer is not accessible.");
        }

        var format = Format ?? throw new NotSupportedException("Unsupported pixel format.");
        var address = _bitmap?.GetPixels() ?? _pixels!.GetPixels();
        var rowBytes = _bitmap?.RowBytes ?? _pixels!.RowBytes;
        return new LockedFramebuffer(address, PixelSize, rowBytes, Dpi, format, AlphaFormat ?? Avalonia.Platform.AlphaFormat.Premul, null);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _pixels?.Dispose();
        if (_customDispose is not null)
        {
            _customDispose();
        }
        else
        {
            _image.Dispose();
        }

        _bitmap?.Dispose();
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Skia.Rendering;
using SkiaSharp;

namespace HyperPdf.Skia.Bitmaps;

/// <summary>Writeable bitmap implementation backed by an <see cref = "SKBitmap"/>.</summary>
internal sealed class WriteableBitmapImpl : IWriteableBitmapImpl, IDrawableBitmapImpl
{
    /// <summary>The default image encoder quality.</summary>
    private const int DefaultQuality = 100;

    /// <summary>Synchronization lock for pixel buffer updates.</summary>
    private readonly Lock _sync = new();

    /// <summary>The underlying mutable bitmap.</summary>
    private readonly SKBitmap _bitmap;

    /// <summary>The current immutable snapshot image.</summary>
    private SKImage? _image;

    /// <summary>Whether the current image snapshot is valid.</summary>
    private bool _imageValid;

    /// <summary>The pixel content version.</summary>
    private int _version = 1;

    /// <summary>The unlock callback, created once rather than on every lock.</summary>
    private Action? _onUnlock;

    /// <summary>Initializes a new instance of the <see cref = "WriteableBitmapImpl"/> class.</summary>
    /// <param name = "size">The pixel dimensions.</param>
    /// <param name = "dpi">The DPI.</param>
    /// <param name = "format">The pixel format.</param>
    /// <param name = "alphaFormat">The alpha format.</param>
    /// <exception cref = "InvalidOperationException">Thrown when <c>!_bitmap.TryAllocPixels(info)</c>.</exception>
    public WriteableBitmapImpl(PixelSize size, Vector dpi, PixelFormat format, AlphaFormat alphaFormat)
    {
        var info = new SKImageInfo(size.Width, size.Height, format.ToSkColorType(), alphaFormat.ToSkAlphaType());
        _bitmap = new();
        if (!_bitmap.TryAllocPixels(info))
        {
            _bitmap.Dispose();
            throw new InvalidOperationException("Failed to allocate writeable bitmap memory.");
        }

        PixelSize = size;
        Dpi = dpi;
    }

    /// <summary>Initializes a new instance of the <see cref = "WriteableBitmapImpl"/> class from a stream.</summary>
    /// <param name = "stream">The encoded image stream.</param>
    /// <exception cref = "ArgumentException">Thrown when <c>SKBitmap.Decode(data)</c> is <see langword="null"/>.</exception>
    public WriteableBitmapImpl(Stream stream)
    {
        using var managedStream = new SKManagedStream(stream);
        using var data = SKData.Create(managedStream);
        _bitmap = Lockable(SKBitmap.Decode(data) ?? throw new ArgumentException("Unable to decode bitmap.", nameof(stream)));
        PixelSize = new(_bitmap.Width, _bitmap.Height);
        Dpi = SkiaPlatform.DefaultDpi;
    }

    /// <summary>Initializes a new instance of the <see cref = "WriteableBitmapImpl"/> class scaled to width or height.</summary>
    /// <param name = "stream">The image stream.</param>
    /// <param name = "targetDimension">The desired dimension.</param>
    /// <param name = "isWidth">Whether targeting width.</param>
    /// <param name = "interpolationMode">The interpolation mode.</param>
    public WriteableBitmapImpl(Stream stream, int targetDimension, bool isWidth, BitmapInterpolationMode interpolationMode)
    {
        using var managedStream = new SKManagedStream(stream);
        using var data = SKData.Create(managedStream);
        using var codec = SKCodec.Create(data);
        var scale = isWidth ? ((float)targetDimension / codec.Info.Width) : ((float)targetDimension / codec.Info.Height);
        var scaled = codec.GetScaledDimensions(scale);
        var nearest = new SKImageInfo(scaled.Width, scaled.Height);
        using var decoded = Lockable(SKBitmap.Decode(codec, nearest));
        var finalWidth = isWidth ? targetDimension : (int)Math.Round(decoded.Width * ((double)targetDimension / decoded.Height));
        var finalHeight = isWidth ? (int)Math.Round(decoded.Height * ((double)targetDimension / decoded.Width)) : targetDimension;
        var info = new SKImageInfo(finalWidth, finalHeight, decoded.ColorType, decoded.AlphaType);
        _bitmap = new(info);
        using var pixels = _bitmap.PeekPixels();
        _ = decoded.ScalePixels(pixels, interpolationMode.ToSKSamplingOptions());
        PixelSize = new(finalWidth, finalHeight);
        Dpi = SkiaPlatform.DefaultDpi;
    }

    /// <inheritdoc/>
    public Vector Dpi { get; }

    /// <inheritdoc/>
    public PixelSize PixelSize { get; }

    /// <inheritdoc/>
    public int Version => Volatile.Read(ref _version);

    /// <inheritdoc/>
    public PixelFormat? Format => _bitmap.ColorType.ToAvalonia();

    /// <inheritdoc/>
    public AlphaFormat? AlphaFormat => _bitmap.AlphaType.ToAlphaFormat();

    /// <inheritdoc/>
    public void Draw(DrawingContextImpl context, SKRect sourceRect, SKRect destRect, SKSamplingOptions samplingOptions, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(context);
        var image = GetOrCreateImage();
        context.Canvas.DrawImage(image, sourceRect, destRect, samplingOptions, paint);
    }

    /// <inheritdoc/>
    public void Save(Stream stream, BitmapEncoderOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var image = GetOrCreateImage();
        var format = options is JpegBitmapEncoderOptions ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        var quality = (options as JpegBitmapEncoderOptions)?.Quality ?? DefaultQuality;
        using var data = image.Encode(format, quality) ?? throw new InvalidOperationException("Failed to encode writeable bitmap.");
        data.SaveTo(stream);
    }

    /// <inheritdoc/>
    public ILockedFramebuffer Lock()
    {
        var format = Format ?? throw new NotSupportedException($"Unsupported pixel format {_bitmap.ColorType}.");
        return new LockedFramebuffer(_bitmap.GetPixels(), PixelSize, _bitmap.RowBytes, Dpi, format, AlphaFormat ?? Avalonia.Platform.AlphaFormat.Premul, _onUnlock ??= OnUnlock);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_sync)
        {
            _image?.Dispose();
            _image = null;
            _bitmap.Dispose();
        }
    }

    /// <summary>
    /// Converts a decoded bitmap that Avalonia cannot lock, such as a greyscale scan, to the platform's colour type, so
    /// every decoded writeable bitmap can be read and written.
    /// </summary>
    /// <param name="decoded">The decoded bitmap; disposed when it is replaced.</param>
    /// <returns>A bitmap in a lockable pixel format.</returns>
    /// <exception cref="InvalidOperationException">The pixels could not be converted.</exception>
    private static SKBitmap Lockable(SKBitmap decoded)
    {
        if (decoded.ColorType.ToAvalonia() is not null)
        {
            return decoded;
        }

        using (decoded)
        {
            return decoded.Copy(SKImageInfo.PlatformColorType) ?? throw new InvalidOperationException($"Unable to convert {decoded.ColorType} pixels.");
        }
    }

    /// <summary>Gets the current immutable snapshot image or creates a new one.</summary>
    /// <returns>The Skia image.</returns>
    private SKImage GetOrCreateImage()
    {
        lock (_sync)
        {
            if (_imageValid && _image is not null)
            {
                return _image;
            }

            ObjectDisposedException.ThrowIf(_bitmap.Handle == IntPtr.Zero, this);
            _image?.Dispose();

            // Wrapping the pixels shares them; FromBitmap copies a mutable bitmap, doubling each tile's memory. Writers
            // finish before a bitmap is shown, as Avalonia's own backend also assumes, and each unlock replaces the snapshot.
            _image = SKImage.FromPixels(_bitmap.Info, _bitmap.GetPixels(), _bitmap.RowBytes);
            _imageValid = true;
            return _image;
        }
    }

    /// <summary>Callback invoked when the framebuffer lock is released.</summary>
    private void OnUnlock()
    {
        lock (_sync)
        {
            _imageValid = false;
            _ = Interlocked.Increment(ref _version);
        }
    }
}

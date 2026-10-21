// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;
using SkiaSharp;

namespace HyperPdf.Rendering.Skia;

/// <summary>
/// A software tile backed by a Skia bitmap, for a render head that draws with Skia directly and has no bitmap type of its own;
/// HyperPDF renders straight into its pixels.
/// </summary>
[DebuggerDisplay("SkiaBitmapRenderSurface: {Width} x {Height}")]
public sealed class SkiaBitmapRenderSurface : IRenderSurface
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The image over the bitmap's pixels, once made.</summary>
    private SKImage? _image;

    /// <summary>Initializes a new instance of the <see cref="SkiaBitmapRenderSurface"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public SkiaBitmapRenderSurface(int width, int height)
    {
        Width = width;
        Height = height;
        Bitmap = new(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public long ByteSize => (long)Width * Height * BytesPerPixel;

    /// <summary>Gets the bitmap, to draw with Skia.</summary>
    public SKBitmap Bitmap { get; }

    /// <summary>Gets an image that shares the bitmap's pixels, made on first use, for drawing with a chosen sampling.</summary>
    public SKImage Image => Volatile.Read(ref _image) ?? CreateImage();

    /// <inheritdoc/>
    public bool Write<TState>(in TState state, SurfaceWriter<TState> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer(new(Bitmap.GetPixelSpan(), Width, Height, Bitmap.RowBytes), state);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Interlocked.Exchange(ref _image, null)?.Dispose();
        Bitmap.Dispose();
    }

    /// <summary>Makes the image over the bitmap's pixels; a second thread that lost the race frees its copy.</summary>
    /// <returns>The image.</returns>
    private SKImage CreateImage()
    {
        var created = SKImage.FromPixels(Bitmap.Info, Bitmap.GetPixels(), Bitmap.RowBytes);
        if (Interlocked.CompareExchange(ref _image, created, null) is { } existing)
        {
            created.Dispose();
            return existing;
        }

        return created;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <inheritdoc/>
    public bool Write<TState>(in TState state, SurfaceWriter<TState> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer(new(Bitmap.GetPixelSpan(), Width, Height, Bitmap.RowBytes), state);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Bitmap.Dispose();
}

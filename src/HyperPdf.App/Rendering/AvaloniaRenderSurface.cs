// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.Rendering;

/// <summary>A tile backed by an Avalonia <see cref="WriteableBitmap"/>; HyperPDF renders straight into its pixels.</summary>
[DebuggerDisplay("AvaloniaRenderSurface: {Width} x {Height}")]
internal sealed class AvaloniaRenderSurface : IRenderSurface
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The bitmap DPI.</summary>
    private const double Dpi = 96;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaRenderSurface"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal AvaloniaRenderSurface(int width, int height)
    {
        Width = width;
        Height = height;
        Bitmap = new(new PixelSize(width, height), new Vector(Dpi, Dpi), PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public long ByteSize => (long)Width * Height * BytesPerPixel;

    /// <summary>Gets the bitmap.</summary>
    internal WriteableBitmap Bitmap { get; }

    /// <inheritdoc/>
    public unsafe bool Write<TState>(in TState state, SurfaceWriter<TState> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        using var buffer = Bitmap.Lock();
        var pixels = new Span<byte>((void*)buffer.Address, buffer.RowBytes * buffer.Size.Height);
        return writer(new(pixels, buffer.Size.Width, buffer.Size.Height, buffer.RowBytes), state);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Bitmap.Dispose();
}

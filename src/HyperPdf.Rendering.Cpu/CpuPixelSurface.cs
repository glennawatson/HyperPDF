// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>A tile held as premultiplied BGRA bytes in a pooled array.</summary>
[DebuggerDisplay("CpuPixelSurface: {Width} x {Height}")]
public sealed class CpuPixelSurface : IRenderSurface
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The pooled pixel array, or <see langword="null"/> once disposed.</summary>
    private byte[]? _pixels;

    /// <summary>Initializes a new instance of the <see cref="CpuPixelSurface"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    public CpuPixelSurface(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = ArrayPool<byte>.Shared.Rent(Stride * height);

        // A pooled array holds whatever an earlier tile left in it, which could be another page's content, so a new surface starts clear.
        _pixels.AsSpan(0, Stride * height).Clear();
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public long ByteSize => (long)Stride * Height;

    /// <summary>Gets the number of bytes per scan line.</summary>
    public int Stride => Width * BytesPerPixel;

    /// <summary>Gets the shape of the pixels.</summary>
    public PixelGrid Grid => new(Width, Height, Stride);

    /// <summary>Gets the pixels, which are valid until the surface is disposed.</summary>
    public ReadOnlySpan<byte> Pixels => (Volatile.Read(ref _pixels) ?? throw new ObjectDisposedException(nameof(CpuPixelSurface))).AsSpan(0, Stride * Height);

    /// <inheritdoc/>
    public bool Write<TState>(in TState state, SurfaceWriter<TState> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var pixels = Volatile.Read(ref _pixels) ?? throw new ObjectDisposedException(nameof(CpuPixelSurface));
        return writer(new(pixels.AsSpan(0, Stride * Height), Width, Height, Stride), state);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _pixels, null) is { } pixels)
        {
            ArrayPool<byte>.Shared.Return(pixels);
        }
    }
}

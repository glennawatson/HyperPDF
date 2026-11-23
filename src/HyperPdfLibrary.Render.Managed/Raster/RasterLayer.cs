// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// A rectangle of pixels that is drawn into: the tile being rendered, or the off-screen layer of a transparency group. Each pixel is
/// four bytes, blue first, with the colour already multiplied by its alpha.
/// </summary>
[DebuggerDisplay("RasterLayer: {Width}x{Height}")]
internal sealed unsafe class RasterLayer : IDisposable
{
    /// <summary>The bytes of one pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The pixels the layer owns, from the pool, or empty when it draws into the caller's pixels.</summary>
    private uint[] _owned = [];

    /// <summary>The caller's first pixel, or null when the layer owns its pixels.</summary>
    private byte* _borrowed;

    /// <summary>The bytes from one line of the caller's pixels to the next.</summary>
    private int _stride;

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_owned.Length > 0)
        {
            ArrayPool<uint>.Shared.Return(_owned);
            _owned = [];
        }

        _borrowed = null;
        Width = 0;
        Height = 0;
    }

    /// <summary>Points the layer at pixels the caller owns and keeps fixed while the layer is in use.</summary>
    /// <param name="pixels">The first pixel.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="stride">The bytes from one line to the next.</param>
    internal void Borrow(byte* pixels, int width, int height, int stride)
    {
        Dispose();
        _borrowed = pixels;
        _stride = stride;
        Width = width;
        Height = height;
    }

    /// <summary>Stops using the caller's pixels, which are no longer fixed once the caller's drawing call ends.</summary>
    internal void Release() => _borrowed = null;

    /// <summary>Gives the layer its own pixels of a size, keeping the memory it has when that is large enough. The pixels are not cleared.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal void Resize(int width, int height)
    {
        _borrowed = null;
        if (_owned.Length < width * height)
        {
            Dispose();
            _owned = ArrayPool<uint>.Shared.Rent(width * height);
        }

        Width = width;
        Height = height;
    }

    /// <summary>Gets the pixels of one line.</summary>
    /// <param name="y">The line.</param>
    /// <returns>The pixels, one value each with blue in the lowest byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<uint> Row(int y) => _borrowed is null ? _owned.AsSpan(y * Width, Width) : new(_borrowed + ((nint)y * _stride), Width);

    /// <summary>Sets every pixel to one value.</summary>
    /// <param name="pixel">The value.</param>
    internal void Fill(uint pixel)
    {
        // An owned layer's rows follow one another, so it is filled in one run; a borrowed one may have a gap after each row.
        if (_borrowed is null)
        {
            _owned.AsSpan(0, Width * Height).Fill(pixel);
            return;
        }

        for (var y = 0; y < Height; y++)
        {
            Row(y).Fill(pixel);
        }
    }

    /// <summary>Copies the pixels of another layer of the same size.</summary>
    /// <param name="source">The layer to copy.</param>
    internal void CopyFrom(RasterLayer source)
    {
        for (var y = 0; y < Height; y++)
        {
            source.Row(y).CopyTo(Row(y));
        }
    }
}

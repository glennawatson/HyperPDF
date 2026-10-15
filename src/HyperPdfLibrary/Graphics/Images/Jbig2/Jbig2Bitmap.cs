// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// A 1-bit JBIG2 bitmap in a pooled buffer: rows of <c>(width + 7) / 8</c> bytes, most significant bit first, 1 for
/// black. Padding bits after the last pixel of a row stay 0, which the decoders rely on.
/// </summary>
[DebuggerDisplay("Jbig2Bitmap: {Width}x{Height}")]
internal sealed class Jbig2Bitmap : IDisposable
{
    /// <summary>A byte with every pixel black.</summary>
    private const byte AllBlack = 0xFF;

    /// <summary>The cache this bitmap returns its rows to.</summary>
    private readonly ScratchPools _scratchPools;

    /// <summary>The rows, or <see langword="null"/> after disposal.</summary>
    private byte[]? _buffer;

    /// <summary>The workspace that takes this bitmap's object back when it is disposed, or <see langword="null"/>.</summary>
    private Jbig2Workspace? _owner;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Bitmap"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="buffer">The cleared buffer.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    private Jbig2Bitmap(int width, int height, byte[] buffer, ScratchPools scratchPools)
    {
        Width = width;
        Height = height;
        Stride = Jbig2Bits.Stride(width);
        _buffer = buffer;
        _scratchPools = scratchPools;
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; private set; }

    /// <summary>Gets the bytes in a row.</summary>
    internal int Stride { get; private set; }

    /// <summary>Gets the rows.</summary>
    internal Span<byte> Data => Buffer.AsSpan(0, Stride * Height);

    /// <summary>Gets a read-only view of the bitmap.</summary>
    internal Jbig2BitmapView View => new(Data, Width, Height);

    /// <summary>Gets the scratch cache that owns this bitmap's buffer.</summary>
    internal ScratchPools ScratchPools => _scratchPools;

    /// <summary>Gets the buffer, which must not be used after disposal.</summary>
    private byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(Jbig2Bitmap));

    /// <summary>Returns the buffer to the pool and, for a bitmap taken from a workspace, the object to the workspace.</summary>
    public void Dispose()
    {
        // A bitmap belongs to one decode on one thread, so plain field access is enough.
        if (_buffer is null)
        {
            return;
        }

        Release();
        var owner = _owner;
        _owner = null;
        owner?.Bitmaps.Give(this);
    }

    /// <summary>Creates a white bitmap from a workspace's idle objects.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="workspace">The workspace that keeps the object when the bitmap is disposed.</param>
    /// <returns>The bitmap, or null when the size is invalid.</returns>
    internal static Jbig2Bitmap? Rent(int width, int height, Jbig2Workspace workspace)
    {
        if (workspace.Bitmaps.Take() is not { } bitmap)
        {
            bitmap = Create(width, height, workspace.ScratchPools);
            bitmap?._owner = workspace;
            return bitmap;
        }

        if (!bitmap.TryReshape(width, height))
        {
            workspace.Bitmaps.Give(bitmap);
            return null;
        }

        bitmap._owner = workspace;
        return bitmap;
    }

    /// <summary>Creates a white bitmap.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The bitmap, or <see langword="null"/> when a side is not positive or the area is over <see cref="Jbig2Limits.MaxRegionPixels"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Jbig2Bitmap? Create(int width, int height) => Create(width, height, new());

    /// <summary>Creates a white bitmap using a caller-owned scratch cache.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The bitmap, or null when the size is invalid.</returns>
    internal static Jbig2Bitmap? Create(int width, int height, ScratchPools scratchPools)
    {
        if (width <= 0 || height <= 0 || (long)width * height > Jbig2Limits.MaxRegionPixels)
        {
            return null;
        }

        var length = Jbig2Bits.Stride(width) * height;
        var buffer = scratchPools.Bytes.Rent(length);
        buffer.AsSpan(0, length).Clear();
        return new(width, height, buffer, scratchPools);
    }

    /// <summary>Returns the buffer to the pool but keeps the object, which takes a new buffer when it is next resized.</summary>
    internal void Release()
    {
        if (_buffer is null)
        {
            return;
        }

        _scratchPools.Bytes.Return(_buffer);
        _buffer = null;
    }

    /// <summary>Gives the bitmap a new size and clears it, keeping the buffer when it is large enough.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns><see langword="false"/> when a side is not positive or the area is over <see cref="Jbig2Limits.MaxRegionPixels"/>.</returns>
    internal bool TryReshape(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > Jbig2Limits.MaxRegionPixels)
        {
            return false;
        }

        var length = Jbig2Bits.Stride(width) * height;
        if (_buffer is null)
        {
            _buffer = _scratchPools.Bytes.Rent(length);
        }
        else if (_buffer.Length < length)
        {
            var pool = _scratchPools.Bytes;
            pool.Return(_buffer);
            _buffer = pool.Rent(length);
        }

        Width = width;
        Height = height;
        Stride = Jbig2Bits.Stride(width);
        Data.Clear();
        return true;
    }

    /// <summary>Gets the bytes of a row.</summary>
    /// <param name="y">The row, inside the bitmap.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<byte> Row(int y) => Buffer.AsSpan(y * Stride, Stride);

    /// <summary>Gets a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel, or 0 outside the bitmap.</returns>
    internal int GetPixel(int x, int y) => (uint)y < (uint)Height ? Jbig2Bits.Get(Row(y), x, Width) : 0;

    /// <summary>Sets every pixel, keeping the padding bits of each row clear.</summary>
    /// <param name="black">Whether the pixels are black.</param>
    internal void Fill(bool black)
    {
        var data = Data;
        if (!black)
        {
            data.Clear();
            return;
        }

        data.Fill(AllBlack);
        var padding = (Stride << Jbig2Bits.ByteShift) - Width;
        if (padding == 0)
        {
            return;
        }

        var last = (byte)(AllBlack << padding);
        for (var y = 0; y < Height; y++)
        {
            data[(y * Stride) + Stride - 1] = last;
        }
    }

    /// <summary>Copies one row over another.</summary>
    /// <param name="from">The source row.</param>
    /// <param name="to">The target row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CopyRow(int from, int to) => Row(from).CopyTo(Row(to));
}

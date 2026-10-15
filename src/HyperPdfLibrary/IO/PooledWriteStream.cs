// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.IO;

/// <summary>
/// A write-only stream that collects bytes in a rented array, for writers that need a <see cref="Stream"/> and hand back
/// one finished array. Growing returns the smaller array to the pool, so only the final copy from <see cref="ToArray"/> is
/// allocated.
/// </summary>
[DebuggerDisplay("PooledWriteStream: {Length} bytes")]
internal sealed class PooledWriteStream : Stream
{
    /// <summary>The capacity of the first rented array when none is asked for.</summary>
    private const int DefaultCapacity = 4096;

    /// <summary>The factor by which a full array grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array; null while disposed.</summary>
    private byte[]? _buffer;

    /// <summary>The bytes written.</summary>
    private int _length;

    /// <summary>Initializes a new instance of the <see cref="PooledWriteStream"/> class.</summary>
    internal PooledWriteStream()
        : this(DefaultCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PooledWriteStream"/> class.</summary>
    /// <param name="capacity">The number of bytes expected, which sizes the first rented array.</param>
    internal PooledWriteStream(int capacity) => _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(capacity, 1));

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => _buffer is not null;

    /// <inheritdoc/>
    public override long Length => _length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _length;
        set => throw new NotSupportedException();
    }

    /// <summary>Gets the bytes written so far; valid until the next write or disposal.</summary>
    internal ReadOnlySpan<byte> WrittenSpan
    {
        get
        {
            ObjectDisposedException.ThrowIf(_buffer is null, this);
            return _buffer.AsSpan(0, _length);
        }
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        if (_buffer.Length - _length < buffer.Length)
        {
            Grow(buffer.Length);
        }

        buffer.CopyTo(_buffer.AsSpan(_length));
        _length += buffer.Length;
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        ObjectDisposedException.ThrowIf(_buffer is null, this);
        if (_length == _buffer.Length)
        {
            Grow(1);
        }

        _buffer[_length] = value;
        _length++;
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    /// <summary>Empties the stream and rents storage again when it was disposed.</summary>
    internal void Reset()
    {
        _buffer ??= ArrayPool<byte>.Shared.Rent(DefaultCapacity);
        _length = 0;
    }

    /// <summary>Copies the bytes written into a new array.</summary>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] ToArray() => WrittenSpan.ToArray();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>Replaces the rented array with a larger one that has room for more bytes.</summary>
    /// <param name="extra">The number of bytes about to be written.</param>
    private void Grow(int extra)
    {
        var larger = ArrayPool<byte>.Shared.Rent(Math.Max(_buffer!.Length * GrowthFactor, _length + extra));
        _buffer.AsSpan(0, _length).CopyTo(larger);
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = larger;
    }
}

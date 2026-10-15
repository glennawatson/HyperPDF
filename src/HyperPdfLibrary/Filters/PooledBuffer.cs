// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// A growable byte buffer rented from the shared pool. Decoding reuses pooled arrays, so a steady stream of decodes does
/// not allocate. Dispose returns the array.
/// </summary>
[DebuggerDisplay("PooledBuffer: {Length} bytes")]
public ref struct PooledBuffer
{
    /// <summary>The smallest array rented.</summary>
    private const int MinimumCapacity = 256;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The rented array.</summary>
    private byte[]? _array;

    /// <summary>Initializes a new instance of the <see cref="PooledBuffer"/> struct.</summary>
    /// <param name="capacity">The initial capacity.</param>
    public PooledBuffer(int capacity) => _array = ArrayPool<byte>.Shared.Rent(Math.Max(capacity, MinimumCapacity));

    /// <summary>Gets or sets the number of bytes written.</summary>
    public int Length { get; set; }

    /// <summary>Gets the bytes written.</summary>
    public readonly ReadOnlySpan<byte> WrittenSpan => _array is null ? [] : _array.AsSpan(0, Length);

    /// <summary>Gets the rented array, which may be longer than <see cref="Length"/>.</summary>
    internal readonly byte[]? Array => _array;

    /// <summary>Gets space to write at least a number of bytes.</summary>
    /// <param name="sizeHint">The bytes needed.</param>
    /// <returns>The free space after the written bytes.</returns>
    /// <exception cref="InvalidDataException">The data would be larger than the supported limit.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetSpan(int sizeHint)
    {
        if (_array is null || _array.Length - Length < sizeHint)
        {
            Grow(sizeHint);
        }

        return _array.AsSpan(Length);
    }

    /// <summary>Records bytes written into the span from <see cref="GetSpan"/>.</summary>
    /// <param name="count">The number of bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => Length += count;

    /// <summary>Appends bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    public void Write(scoped ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(GetSpan(bytes.Length));
        Length += bytes.Length;
    }

    /// <summary>Appends one byte.</summary>
    /// <param name="value">The byte.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteByte(byte value)
    {
        if (_array is null || Length == _array.Length)
        {
            Grow(1);
        }

        _array![Length] = value;
        Length++;
    }

    /// <summary>Copies the written bytes into a new array of exactly their length.</summary>
    /// <returns>The array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte[] ToArray() => WrittenSpan.ToArray();

    /// <summary>Swaps contents with another buffer.</summary>
    /// <param name="other">The other buffer.</param>
    public void Swap(ref PooledBuffer other)
    {
        var array = _array;
        _array = other._array;
        other._array = array;
        var length = Length;
        Length = other.Length;
        other.Length = length;
    }

    /// <summary>Returns the array to the pool.</summary>
    public void Dispose()
    {
        var array = _array;
        _array = null;
        Length = 0;
        if (array is not null)
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    /// <summary>Hands the rented array to the caller, who must return it to <see cref="ArrayPool{T}.Shared"/>; the buffer is empty afterwards.</summary>
    /// <param name="length">Receives the number of bytes written.</param>
    /// <returns>The array, or <see langword="null"/> when nothing was ever rented.</returns>
    internal byte[]? TakeArray(out int length)
    {
        var array = _array;
        length = Length;
        _array = null;
        Length = 0;
        return array;
    }

    /// <summary>Rents a larger array and copies the written bytes into it.</summary>
    /// <param name="sizeHint">The free space needed.</param>
    /// <exception cref="InvalidDataException">The data would be larger than the supported limit.</exception>
    private void Grow(int sizeHint)
    {
        var required = (long)Length + Math.Max(sizeHint, 1);
        if (required > PdfLimits.MaxDecodedLength)
        {
            throw new InvalidDataException("The decoded data is larger than the supported limit.");
        }

        var doubled = (long)(_array?.Length ?? 0) * GrowthFactor;
        var capacity = (int)Math.Min(PdfLimits.MaxDecodedLength, Math.Max(required, Math.Max(MinimumCapacity, doubled)));
        var larger = ArrayPool<byte>.Shared.Rent(capacity);
        if (_array is not null)
        {
            _array.AsSpan(0, Length).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(_array);
        }

        _array = larger;
    }
}

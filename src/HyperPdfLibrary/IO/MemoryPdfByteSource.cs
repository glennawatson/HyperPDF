// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HyperPdfLibrary.IO;

/// <summary>
/// A file held in memory. A source made over caller bytes keeps them, not a copy, and they must not change; windows point
/// straight into them and stay readable after disposal, as nothing is released. A source made by reading a file holds the
/// bytes in a pooled array that disposal returns once the last window is released, and later reads throw
/// <see cref="ObjectDisposedException"/>.
/// </summary>
[DebuggerDisplay("MemoryPdfByteSource: {Length} bytes")]
public sealed class MemoryPdfByteSource : PdfByteSource
{
    /// <summary>Set in <see cref="_state"/> once the source is disposed; the bits below it count the windows in use.</summary>
    private const int DisposedBit = 1 << 30;

    /// <summary>The bytes.</summary>
    private readonly ReadOnlyMemory<byte> _memory;

    /// <summary>The backing array when the memory covers a whole array, else <see langword="null"/>.</summary>
    private readonly byte[]? _array;

    /// <summary>Whether the bytes are a pooled array this source returns.</summary>
    private readonly bool _isPooled;

    /// <summary>The pooled array holding a file this source read, or <see langword="null"/> when the bytes belong to the caller or it was returned.</summary>
    private byte[]? _pooled;

    /// <summary>The windows in use, with <see cref="DisposedBit"/> once disposed; only used with a pooled array.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="MemoryPdfByteSource"/> class over an array.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    public MemoryPdfByteSource(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _memory = bytes;
        _array = bytes;
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryPdfByteSource"/> class over memory.</summary>
    /// <param name="memory">The file's bytes.</param>
    public MemoryPdfByteSource(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _array = MemoryMarshal.TryGetArray(memory, out var segment) && segment.Offset == 0 && segment.Array?.Length == segment.Count
            ? segment.Array
            : null;
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryPdfByteSource"/> class over a pooled array that holds a file.</summary>
    /// <param name="pooled">The array rented from the shared pool; the source returns it.</param>
    /// <param name="length">The file length.</param>
    private MemoryPdfByteSource(byte[] pooled, int length)
    {
        _pooled = pooled;
        _isPooled = true;
        _memory = new(pooled, 0, length);
    }

    /// <inheritdoc/>
    public override long Length => _memory.Length;

    /// <inheritdoc/>
    internal override byte[]? WholeArray => _array;

    /// <summary>Reads a file into a pooled array.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The source; the caller disposes it, which returns the array to the pool.</returns>
    /// <exception cref="IOException">The file cannot be read, or is larger than an array can hold.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    /// <exception cref="EndOfStreamException">The file ended before its length.</exception>
    internal static MemoryPdfByteSource ReadFile(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.SequentialScan);
        var length = FileLength(handle);
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var count = 0;
            for (var read = 0; read < length; read += count)
            {
                count = RandomAccess.Read(handle, rented.AsSpan(read, length - read), read);
                if (count == 0)
                {
                    throw new EndOfStreamException("The file ended before its length.");
                }
            }

            return new(rented, length);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }

    /// <summary>Reads a file into a pooled array with async I/O.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The source; the caller disposes it, which returns the array to the pool.</returns>
    /// <exception cref="IOException">The file cannot be read, or is larger than an array can hold.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    /// <exception cref="EndOfStreamException">The file ended before its length.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static async ValueTask<MemoryPdfByteSource> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = FileLength(handle);
        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            var count = 0;
            for (var read = 0; read < length; read += count)
            {
                count = await RandomAccess.ReadAsync(handle, rented.AsMemory(read, length - read), read, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("The file ended before its length.");
                }
            }

            return new(rented, length);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rented);
            throw;
        }
    }

    /// <summary>Releases the window count a lease took.</summary>
    internal void ReleaseLease()
    {
        if (Interlocked.Decrement(ref _state) == DisposedBit)
        {
            ReturnPooled();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _isPooled && (Interlocked.Or(ref _state, DisposedBit) & ~DisposedBit) == 0)
        {
            ReturnPooled();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    private protected override int ReadCore(long offset, Span<byte> destination)
    {
        if (!_isPooled)
        {
            _memory.Span.Slice((int)offset, destination.Length).CopyTo(destination);
            return destination.Length;
        }

        AddLease();
        try
        {
            _memory.Span.Slice((int)offset, destination.Length).CopyTo(destination);
            return destination.Length;
        }
        finally
        {
            ReleaseLease();
        }
    }

    /// <inheritdoc/>
    private protected override PdfByteLease LeaseCore(long offset, int length)
    {
        if (!_isPooled)
        {
            return new(_memory.Span.Slice((int)offset, length));
        }

        AddLease();
        return new(_memory.Span.Slice((int)offset, length), this);
    }

    /// <summary>Gets the length of a file as an array size.</summary>
    /// <param name="handle">The open file.</param>
    /// <returns>The length.</returns>
    /// <exception cref="IOException">The file is larger than an array can hold.</exception>
    private static int FileLength(SafeFileHandle handle)
    {
        var length = RandomAccess.GetLength(handle);
        return length > Array.MaxLength ? throw new IOException("The file is too large to hold in memory.") : (int)length;
    }

    /// <summary>Counts a window in use, or throws when the source has been disposed.</summary>
    /// <exception cref="ObjectDisposedException">The source was disposed.</exception>
    private void AddLease()
    {
        var state = Volatile.Read(ref _state);
        ObjectDisposedException.ThrowIf((state & DisposedBit) != 0, this);
        var seen = Interlocked.CompareExchange(ref _state, state + 1, state);
        while (seen != state)
        {
            state = seen;
            ObjectDisposedException.ThrowIf((state & DisposedBit) != 0, this);
            seen = Interlocked.CompareExchange(ref _state, state + 1, state);
        }
    }

    /// <summary>Returns the pooled array once the source is disposed and no window is in use.</summary>
    private void ReturnPooled()
    {
        var pooled = Interlocked.Exchange(ref _pooled, null);
        if (pooled is not null)
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
    }
}

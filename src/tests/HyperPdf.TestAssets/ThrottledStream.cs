// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// A read-only seekable stream over bytes that makes every asynchronous read wait, like a slow disk or network share, and
/// counts the reads. Synchronous reads are counted apart so a test can show that a document read ahead with async I/O.
/// </summary>
public sealed class ThrottledStream : Stream
{
    /// <summary>
    /// An event that is never set, waited on to hold a blocking read for the latency. One event serves every stream and
    /// read, because its wait object is created on the first blocking wait and not again.
    /// </summary>
    private static readonly ManualResetEventSlim Idle = new(false);

    /// <summary>The bytes.</summary>
    private readonly byte[] _bytes;

    /// <summary>The wait before each asynchronous read completes.</summary>
    private readonly TimeSpan _latency;

    /// <summary>The position.</summary>
    private long _position;

    /// <summary>The asynchronous reads started.</summary>
    private int _asyncReads;

    /// <summary>The synchronous reads made.</summary>
    private int _syncReads;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="ThrottledStream"/> class.</summary>
    /// <param name="bytes">The bytes to serve.</param>
    /// <param name="latency">The wait before each asynchronous read completes.</param>
    public ThrottledStream(byte[] bytes, TimeSpan latency)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = bytes;
        _latency = latency;
    }

    /// <summary>Gets the asynchronous reads started.</summary>
    public int AsyncReads => Volatile.Read(ref _asyncReads);

    /// <summary>Gets the synchronous reads made.</summary>
    public int SyncReads => Volatile.Read(ref _syncReads);

    /// <summary>Gets a value indicating whether the stream has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _bytes.Length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        _ = Interlocked.Increment(ref _syncReads);

        // A blocking read on a slow device holds its thread for as long as the device takes. The event is never set,
        // so every wait runs to its timeout.
        _ = Idle.Wait(_latency);
        return Copy(buffer);
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _ = Interlocked.Increment(ref _asyncReads);
        await Task.Delay(_latency, cancellationToken).ConfigureAwait(false);
        return Copy(buffer.Span);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => _bytes.Length + offset,
        };
        return _position;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _ = Interlocked.Exchange(ref _disposed, 1);
        base.Dispose(disposing);
    }

    /// <summary>Copies bytes from the position and advances it.</summary>
    /// <param name="buffer">The destination.</param>
    /// <returns>The bytes copied.</returns>
    private int Copy(Span<byte> buffer)
    {
        var count = (int)Math.Clamp(_bytes.Length - _position, 0, buffer.Length);
        _bytes.AsSpan((int)_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }
}

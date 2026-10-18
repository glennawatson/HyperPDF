// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET11_0_OR_GREATER
namespace HyperPdfLibrary.Compat;

/// <summary>
/// A read-only stream over unmanaged bytes that notes when a reader asked for more bytes than the data held. A
/// <see cref="System.IO.Compression.DeflateStream"/> asks for more input only while it has not yet seen the final block, so
/// hitting the end of the data means the compressed stream was cut short.
/// </summary>
internal sealed unsafe class EndTrackingStream : Stream
{
    /// <summary>The input bytes, pinned only during the active decode.</summary>
    private byte* _pointer;

    /// <summary>The number of input bytes.</summary>
    private int _length;

    /// <summary>The next input byte.</summary>
    private int _position;

    /// <summary>Whether input is attached for a decode.</summary>
    private bool _active;

    /// <inheritdoc/>
    public override bool CanRead => _active;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => _length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    /// <summary>Gets a value indicating whether a read found no more bytes.</summary>
    internal bool EndReached { get; private set; }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(!_active, this);
        var read = Math.Min(buffer.Length, _length - _position);
        new ReadOnlySpan<byte>(_pointer + _position, read).CopyTo(buffer);
        _position += read;
        EndReached |= read == 0 && !buffer.IsEmpty;
        return read;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <summary>Attaches bytes for one decode while the caller pins them.</summary>
    /// <param name="pointer">The pinned input.</param>
    /// <param name="length">The input length.</param>
    internal void Reset(byte* pointer, int length)
    {
        _pointer = pointer;
        _length = length;
        _position = 0;
        EndReached = false;
        _active = true;
    }

    /// <summary>Detaches input before the caller ends its pin.</summary>
    internal void Release()
    {
        _pointer = null;
        _length = 0;
        _position = 0;
        _active = false;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        Release();
        base.Dispose(disposing);
    }
}
#endif

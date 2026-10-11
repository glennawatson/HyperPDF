// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// A PDF stream: a dictionary and its encoded data. A stream read from a file keeps only the data's offset and length,
/// and reads the bytes from the file each time they are needed, so the file is never held in memory for it.
/// </summary>
[DebuggerDisplay("PdfStream: {Id} {RawLength} bytes")]
public sealed class PdfStream
{
    /// <summary>The buffer holding the encoded data, or <see langword="null"/> when it is read from <see cref="_source"/>.</summary>
    private readonly byte[]? _buffer;

    /// <summary>The file holding the encoded data, or <see langword="null"/> when it is in <see cref="_buffer"/>.</summary>
    private readonly PdfByteSource? _source;

    /// <summary>The offset of the encoded data.</summary>
    private readonly long _offset;

    /// <summary>Initializes a new instance of the <see cref="PdfStream"/> class over part of a buffer.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="buffer">The buffer holding the encoded data, kept, not copied.</param>
    /// <param name="offset">The offset of the data.</param>
    /// <param name="length">The length of the data.</param>
    /// <param name="id">The stream's object id, used to decrypt it.</param>
    /// <param name="isEncrypted">Whether the data must be decrypted before decoding.</param>
    public PdfStream(PdfDictionary dictionary, byte[] buffer, int offset, int length, PdfObjectId id, bool isEncrypted)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)offset + (uint)length, (uint)buffer.Length);
        Dictionary = dictionary;
        _buffer = buffer;
        _offset = offset;
        RawLength = length;
        Id = id;
        IsEncrypted = isEncrypted;
    }

    /// <summary>Initializes a new instance of the <see cref="PdfStream"/> class over data it owns.</summary>
    /// <param name="dictionary">The stream dictionary; its /Length is set.</param>
    /// <param name="data">The encoded data.</param>
    public PdfStream(PdfDictionary dictionary, byte[] data)
        : this(dictionary, data, 0, data?.Length ?? 0, default, false) =>
        dictionary.Set(KnownName.Length, PdfValue.FromInteger(data!.Length));

    /// <summary>Initializes a new instance of the <see cref="PdfStream"/> class over a range of a file.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="source">The file, which must outlive the stream's reads.</param>
    /// <param name="offset">The offset of the data.</param>
    /// <param name="length">The length of the data.</param>
    /// <param name="id">The stream's object id, used to decrypt it.</param>
    /// <param name="isEncrypted">Whether the data must be decrypted before decoding.</param>
    internal PdfStream(PdfDictionary dictionary, PdfByteSource source, long offset, int length, PdfObjectId id, bool isEncrypted)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, source.Length);
        Dictionary = dictionary;
        _source = source;
        _offset = offset;
        RawLength = length;
        Id = id;
        IsEncrypted = isEncrypted;
    }

    /// <summary>Gets the stream dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets the object id, or an invalid id for a stream created in memory.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets the length of the encoded data.</summary>
    public int RawLength { get; }

    /// <summary>Gets a value indicating whether the data must be decrypted before decoding.</summary>
    public bool IsEncrypted { get; }

    /// <summary>
    /// Gets the encoded (and possibly encrypted) data. Dispose the lease when done; for a stream read from a file it may
    /// hold a pooled copy or a reference on the file's mapping.
    /// </summary>
    /// <returns>The lease over the data.</returns>
    /// <exception cref="ObjectDisposedException">The document's file has been released.</exception>
    public PdfByteLease LeaseRawData() =>
        _buffer is not null ? new(_buffer.AsSpan((int)_offset, RawLength)) : _source!.Lease(_offset, RawLength);

    /// <summary>Copies the encoded (and possibly encrypted) data into a new array.</summary>
    /// <returns>The data.</returns>
    /// <exception cref="ObjectDisposedException">The document's file has been released.</exception>
    public byte[] CopyRawData()
    {
        using var raw = LeaseRawData();
        return raw.Span.ToArray();
    }

    /// <summary>Decodes the data through every filter except image codecs, which are left for the image decoder.</summary>
    /// <param name="output">The buffer receiving the decoded data.</param>
    /// <returns>The image codec still to apply, if any.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfImageCodec Decode(ref PooledBuffer output) => Decode(ref output, CancellationToken.None);

    /// <summary>Decodes stream filters while observing the caller's token.</summary>
    /// <param name="output">Receives decoded bytes.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The remaining image codec, if any.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfImageCodec Decode(ref PooledBuffer output, CancellationToken cancellationToken) =>
        PdfStreamDecoder.Decode(this, ref output, cancellationToken);

    /// <summary>Decodes the data into a new array of exactly its length.</summary>
    /// <returns>The decoded bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] DecodeToArray() => DecodeToArray(CancellationToken.None);

    /// <summary>Decodes the data into a new array while observing the caller's token.</summary>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded bytes.</returns>
    public byte[] DecodeToArray(CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = Decode(ref output, cancellationToken);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Gets the data's array when it is held in memory, so a copy can share it.</summary>
    /// <param name="segment">The data.</param>
    /// <returns><see langword="true"/> when the data is in an array.</returns>
    internal bool TryGetArray(out ArraySegment<byte> segment)
    {
        var array = _buffer ?? _source?.WholeArray;
        segment = array is null ? default : new(array, (int)_offset, RawLength);
        return array is not null;
    }
}

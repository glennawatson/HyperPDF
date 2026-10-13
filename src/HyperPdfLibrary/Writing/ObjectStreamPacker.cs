// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Collects objects into an object stream: a header of number and offset pairs, and a body writer holding the objects
/// unencrypted, because the whole stream is encrypted as one. The caller owns the body writer.
/// </summary>
internal ref struct ObjectStreamPacker
{
    /// <summary>The number and offset pairs.</summary>
    private PooledBuffer _header;

    /// <summary>Gets the number of objects collected.</summary>
    internal int Count { get; private set; }

    /// <summary>Adds an object.</summary>
    /// <param name="body">The body writer, which renumbers references and does not encrypt.</param>
    /// <param name="number">The object's number.</param>
    /// <param name="value">The value, which must not be a stream.</param>
    internal void Add(ref PdfObjectWriter body, int number, PdfValue value)
    {
        if (Count > 0)
        {
            _header.WriteByte((byte)' ');
            body.WriteRaw("\n"u8);
        }

        PdfSyntax.WriteInteger(ref _header, number);
        _header.WriteByte((byte)' ');
        PdfSyntax.WriteInteger(ref _header, body.Length);
        body.WriteValue(value);
        Count++;
    }

    /// <summary>Writes the collected objects as a Flate-compressed object stream and starts a new one.</summary>
    /// <param name="body">The body writer, emptied.</param>
    /// <param name="writer">The document writer.</param>
    /// <param name="id">The object stream's id, which it is encrypted for when the writer encrypts.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Flush(ref PdfObjectWriter body, ref PdfObjectWriter writer, PdfObjectId id) => Flush(ref body, ref writer, id, StreamPolicy.Default);

    /// <summary>Writes the collected objects as an object stream, Flate-compressed unless the policy asks for plain streams, and starts a new one.</summary>
    /// <param name="body">The body writer, emptied.</param>
    /// <param name="writer">The document writer.</param>
    /// <param name="id">The object stream's id, which it is encrypted for when the writer encrypts.</param>
    /// <param name="policy">How the stream is compressed.</param>
    internal void Flush(ref PdfObjectWriter body, ref PdfObjectWriter writer, PdfObjectId id, StreamPolicy policy)
    {
        _header.WriteByte((byte)'\n');
        var first = _header.Length;
        _header.Write(body.WrittenSpan);
        var compressed = default(PooledBuffer);
        try
        {
            var plain = policy.Mode == PdfStreamMode.Uncompressed;
            if (!plain)
            {
                FlateFilter.Encode(_header.WrittenSpan, policy.Level, ref compressed);
            }

            var dictionary = new PdfDictionary(null);
            dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.ObjStm));
            dictionary.Set(KnownName.N, PdfValue.FromInteger(Count));
            dictionary.Set(KnownName.First, PdfValue.FromInteger(first));
            writer.WriteObjectHeader(id);
            writer.WriteStream(dictionary, plain ? _header.WrittenSpan : compressed.WrittenSpan, id, !plain);
            writer.WriteRaw("\nendobj\n"u8);
        }
        finally
        {
            compressed.Dispose();
        }

        _header.Length = 0;
        body.Buffer.Length = 0;
        Count = 0;
    }

    /// <summary>Returns the header buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Dispose() => _header.Dispose();
}

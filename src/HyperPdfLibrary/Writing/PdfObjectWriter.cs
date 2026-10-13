// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Serialises PDF values as PDF syntax into a pooled buffer. Values are written byte by byte with no string allocation;
/// the same input always gives the same bytes, except that AES encryption draws a fresh IV. Dispose returns the buffer.
/// </summary>
[DebuggerDisplay("PdfObjectWriter: {Length} bytes")]
public ref struct PdfObjectWriter
{
    /// <summary>The names whose spellings are written.</summary>
    private readonly PdfNameTable _names;

    /// <summary>Encrypts strings and streams, or <see langword="null"/> to write them plain.</summary>
    private readonly PdfSecurityHandler? _security;

    /// <summary>The new number of each old object number, or <see langword="null"/> to keep numbers.</summary>
    private int[]? _renumber;

    /// <summary>The output.</summary>
    private PooledBuffer _buffer;

    /// <summary>Initializes a new instance of the <see cref="PdfObjectWriter"/> struct that writes unencrypted.</summary>
    /// <param name="names">The name table the written names come from.</param>
    public PdfObjectWriter(PdfNameTable names)
        : this(names, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfObjectWriter"/> struct.</summary>
    /// <param name="names">The name table the written names come from.</param>
    /// <param name="security">Encrypts the strings and streams of indirect objects, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
    public PdfObjectWriter(PdfNameTable names, PdfSecurityHandler? security)
    {
        ArgumentNullException.ThrowIfNull(names);
        _names = names;
        _security = security;
    }

    /// <summary>Gets the number of bytes written.</summary>
    public readonly int Length => _buffer.Length;

    /// <summary>Gets the bytes written.</summary>
    public readonly ReadOnlySpan<byte> WrittenSpan => _buffer.WrittenSpan;

    /// <summary>Gets the names whose spellings are written.</summary>
    internal readonly PdfNameTable Names => _names;

    /// <summary>Gets the output buffer.</summary>
    [UnscopedRef]
    internal ref PooledBuffer Buffer => ref _buffer;

    /// <summary>Writes a value without encryption. Streams must be written with <see cref="WriteIndirectObject(PdfObjectId, PdfValue)"/>.</summary>
    /// <param name="value">The value.</param>
    /// <exception cref="PdfException">The value nests too deeply or holds a stream.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteValue(PdfValue value) => WriteValue(value, default, 0);

    /// <summary>Writes a value, encrypting its strings for an object when a security handler is set.</summary>
    /// <param name="value">The value.</param>
    /// <param name="encryptionId">The object the value belongs to, or an invalid id for no encryption.</param>
    /// <exception cref="PdfException">The value nests too deeply or holds a stream.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteValue(PdfValue value, PdfObjectId encryptionId) => WriteValue(value, encryptionId, 0);

    /// <summary>Writes an indirect object, <c>n g obj ... endobj</c>, encrypted when a security handler is set.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value; a stream is written with its data.</param>
    /// <exception cref="PdfException">The value nests too deeply or holds a nested stream.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteIndirectObject(PdfObjectId id, PdfValue value) => WriteIndirectObject(id, value, _security is not null);

    /// <summary>Writes an indirect object, <c>n g obj ... endobj</c>.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value; a stream is written with its data.</param>
    /// <param name="encrypt">Whether to encrypt it; the /Encrypt dictionary itself must not be.</param>
    /// <exception cref="PdfException">The value nests too deeply or holds a nested stream.</exception>
    public void WriteIndirectObject(PdfObjectId id, PdfValue value, bool encrypt)
    {
        WriteObjectHeader(id);
        var encryptionId = encrypt && _security is not null ? id : default;
        if (value.AsStream() is { } stream)
        {
            WriteStream(stream, encryptionId);
        }
        else
        {
            WriteValue(value, encryptionId, 0);
        }

        _buffer.Write("\nendobj\n"u8);
    }

    /// <summary>Writes bytes as they are.</summary>
    /// <param name="bytes">The bytes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteRaw(ReadOnlySpan<byte> bytes) => _buffer.Write(bytes);

    /// <summary>Copies the written bytes into a new array.</summary>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte[] ToArray() => _buffer.ToArray();

    /// <summary>Returns the buffer to the pool.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _buffer.Dispose();

    /// <summary>
    /// Determines whether a stream is left unencrypted in an encrypted document, as <see cref="Filters.PdfStreamDecoder"/> reads
    /// it: cross-reference streams, streams whose /Crypt filter names no filter or Identity, and metadata streams when
    /// metadata is not encrypted. A stream whose /Crypt filter names a real filter is encrypted with that filter.
    /// </summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="security">The security handler.</param>
    /// <returns><see langword="true"/> when the stream is stored in the clear.</returns>
    internal static bool IsCryptExempt(PdfDictionary dictionary, PdfSecurityHandler security) =>
        dictionary.IsName(KnownName.Type, KnownName.XRef) || StreamCrypt.Choose(dictionary, security).Clear;

    /// <summary>Gets a stream's encoded data with any encryption removed.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="raw">The stream's encoded data, from <see cref="PdfStream.LeaseRawData"/>.</param>
    /// <returns>The data; a new array only when it had to be decrypted.</returns>
    internal static ReadOnlySpan<byte> PlainData(PdfStream stream, ReadOnlySpan<byte> raw)
    {
        var source = stream.Dictionary.Owner?.Security;
        if (!stream.IsEncrypted || source is null || IsCryptExempt(stream.Dictionary, source))
        {
            return raw;
        }

        var filter = StreamCrypt.Choose(stream.Dictionary, source).Filter;
        return filter.IsNone ? source.DecryptStream(stream.Id, raw) : source.DecryptStream(stream.Id, raw, filter);
    }

    /// <summary>Renumbers references as they are written.</summary>
    /// <param name="map">
    /// The new number of each old object number, or <see langword="null"/> to keep numbers. A reference whose new number is
    /// zero or less is written as <c>null</c>, and every generation becomes zero.
    /// </param>
    internal void SetRenumbering(int[]? map) => _renumber = map;

    /// <summary>Writes <c>n g obj</c> and a line end.</summary>
    /// <param name="id">The object id.</param>
    internal void WriteObjectHeader(PdfObjectId id)
    {
        PdfSyntax.WriteInteger(ref _buffer, id.Number);
        _buffer.WriteByte((byte)' ');
        PdfSyntax.WriteInteger(ref _buffer, id.Generation);
        _buffer.Write(" obj\n"u8);
    }

    /// <summary>Writes a stream's dictionary and data, re-encrypting the data for its new id when needed.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="encryptionId">The object the stream is written as, or an invalid id for no encryption.</param>
    internal void WriteStream(PdfStream stream, PdfObjectId encryptionId)
    {
        using var raw = stream.LeaseRawData();
        if (BrotliRewriter.UsesBrotli(stream.Dictionary))
        {
            WriteWithoutBrotli(stream, raw.Span, encryptionId);
            return;
        }

        if (CanCopyEncrypted(stream, encryptionId))
        {
            // Already encrypted with the key this id needs: copy the bytes as they are.
            WriteStreamDictionary(stream.Dictionary, stream.RawLength, false, encryptionId);
            WriteStreamData(raw.Span);
            return;
        }

        WriteStream(stream.Dictionary, PlainData(stream, raw.Span), encryptionId, false);
    }

    /// <summary>Writes a stream from unencrypted data.</summary>
    /// <param name="dictionary">The stream dictionary; /Length is replaced.</param>
    /// <param name="data">The encoded, unencrypted data.</param>
    /// <param name="encryptionId">The object the stream is written as, or an invalid id for no encryption.</param>
    /// <param name="addFlate">Whether to add /Filter /FlateDecode, for data this writer compressed.</param>
    internal void WriteStream(PdfDictionary dictionary, ReadOnlySpan<byte> data, PdfObjectId encryptionId, bool addFlate)
    {
        if (encryptionId.IsValid && _security is not null)
        {
            if (IsCryptExempt(dictionary, _security))
            {
                dictionary = MarkMetadataClear(dictionary, addFlate);
            }
            else
            {
                var filter = StreamCrypt.Choose(dictionary, _security).Filter;
                data = filter.IsNone ? _security.EncryptStream(encryptionId, data) : _security.EncryptStream(encryptionId, data, filter);
            }
        }

        WriteStreamDictionary(dictionary, data.Length, addFlate, encryptionId);
        WriteStreamData(data);
    }

    /// <summary>Takes the written bytes out of the writer, leaving it empty.</summary>
    /// <param name="length">The number of bytes written.</param>
    /// <returns>The pooled array holding them, which the caller returns to <see cref="System.Buffers.ArrayPool{T}.Shared"/>.</returns>
    internal byte[]? Detach(out int length)
    {
        var taken = default(PooledBuffer);
        _buffer.Swap(ref taken);
        length = taken.Length;
        return taken.Array;
    }

    /// <summary>Gets the method that encrypts a stream: its crypt filter's, or the document's stream method.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="security">The security handler.</param>
    /// <returns>The method.</returns>
    private static CryptMethod MethodOf(PdfDictionary dictionary, PdfSecurityHandler security)
    {
        var choice = StreamCrypt.Choose(dictionary, security);
        if (choice.Clear)
        {
            return CryptMethod.None;
        }

        return choice.Filter.IsNone ? security.StreamMethod : security.MethodOf(choice.Filter);
    }

    /// <summary>Throws when values nest deeper than the parser accepts.</summary>
    /// <param name="depth">The nesting depth.</param>
    /// <exception cref="PdfException">The depth is too great.</exception>
    private static void CheckDepth(int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            throw new PdfException(PdfError.Format, "The value nests too deeply to write; it may contain itself.");
        }
    }

    /// <summary>
    /// Gives a document metadata stream that is left unencrypted the Identity crypt filter (ISO 32000-2 7.6.6 example), so
    /// readers that do not know /EncryptMetadata still leave it unencrypted. Other streams, and metadata that has filters
    /// of its own, are written as they are.
    /// </summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="addFlate">Whether the writer adds its own /Filter.</param>
    /// <returns>The dictionary to write.</returns>
    private PdfDictionary MarkMetadataClear(PdfDictionary dictionary, bool addFlate)
    {
        if (addFlate || _security is not { EncryptMetadata: false } || !dictionary.IsName(KnownName.Type, KnownName.Metadata)
            || !dictionary.GetRaw(KnownName.Filter).IsNull || !dictionary.GetRaw(KnownName.DecodeParms).IsNull)
        {
            return dictionary;
        }

        var parameters = new PdfDictionary(null);
        parameters.Set(KnownName.Type, PdfValue.FromName(_names.Intern("CryptFilterDecodeParms"u8)));
        parameters.Set(KnownName.Name, PdfValue.FromName(KnownName.Identity));
        var copy = dictionary.Clone();
        copy.Set(KnownName.Filter, PdfValue.FromArray(new(null, [PdfValue.FromName(KnownName.Crypt)])));
        copy.Set(KnownName.DecodeParms, PdfValue.FromArray(new(null, [PdfValue.FromDictionary(parameters)])));
        return copy;
    }

    /// <summary>Writes a BrotliDecode stream with conforming filters instead, since BrotliDecode is not part of ISO 32000-2.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="raw">The stream's encoded data.</param>
    /// <param name="encryptionId">The object the stream is written as, or an invalid id for no encryption.</param>
    private void WriteWithoutBrotli(PdfStream stream, ReadOnlySpan<byte> raw, PdfObjectId encryptionId)
    {
        var rewritten = default(PooledBuffer);
        try
        {
            var dictionary = BrotliRewriter.Rewrite(stream.Dictionary, PlainData(stream, raw), ref rewritten);
            WriteStream(dictionary, rewritten.WrittenSpan, encryptionId, false);
        }
        finally
        {
            rewritten.Dispose();
        }
    }

    /// <summary>Writes a value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="encryptionId">The object for string encryption, or an invalid id.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <exception cref="PdfException">The value nests too deeply or holds a stream.</exception>
    private void WriteValue(PdfValue value, PdfObjectId encryptionId, int depth)
    {
        switch (value.Kind)
        {
            case PdfKind.String:
            {
                WriteStringValue(value.AsStringBytes(), encryptionId);
                break;
            }

            case PdfKind.Array:
            {
                WriteArray(value.AsArray()!, encryptionId, depth + 1);
                break;
            }

            case PdfKind.Dictionary:
            {
                WriteDictionary(value.AsDictionary()!, encryptionId, depth + 1);
                break;
            }

            case PdfKind.Reference:
            {
                WriteReference(value.AsReference());
                break;
            }

            case PdfKind.Stream:
            {
                throw new PdfException(PdfError.Format, "A stream can only be written as an indirect object.");
            }

            default:
            {
                WriteScalar(value);
                break;
            }
        }
    }

    /// <summary>Writes null, a boolean, a number or a name.</summary>
    /// <param name="value">The value.</param>
    private void WriteScalar(PdfValue value)
    {
        switch (value.Kind)
        {
            case PdfKind.Boolean:
            {
                _buffer.Write(value.AsBoolean() ? "true"u8 : "false"u8);
                break;
            }

            case PdfKind.Integer:
            {
                PdfSyntax.WriteInteger(ref _buffer, value.AsInteger());
                break;
            }

            case PdfKind.Real:
            {
                PdfSyntax.WritePreciseNumber(ref _buffer, value.AsNumber());
                break;
            }

            case PdfKind.Name:
            {
                PdfSyntax.WriteName(ref _buffer, _names.GetSpelling(value.AsName()));
                break;
            }

            default:
            {
                _buffer.Write("null"u8);
                break;
            }
        }
    }

    /// <summary>Writes a string, encrypted when an object id and security handler are set.</summary>
    /// <param name="bytes">The plain bytes.</param>
    /// <param name="encryptionId">The object for encryption, or an invalid id.</param>
    private void WriteStringValue(ReadOnlySpan<byte> bytes, PdfObjectId encryptionId)
    {
        if (encryptionId.IsValid && _security is not null)
        {
            bytes = _security.EncryptString(encryptionId, bytes);
        }

        PdfSyntax.WriteString(ref _buffer, bytes);
    }

    /// <summary>Writes a reference, renumbered when a map is set.</summary>
    /// <param name="id">The referenced object.</param>
    private void WriteReference(PdfObjectId id)
    {
        if (_renumber is { } map)
        {
            var number = (uint)id.Number < (uint)map.Length ? map[id.Number] : 0;
            if (number <= 0)
            {
                // The object is missing, and a reference to a missing object means null.
                _buffer.Write("null"u8);
                return;
            }

            id = new(number, 0);
        }

        PdfSyntax.WriteReference(ref _buffer, id.Number, id.Generation);
    }

    /// <summary>Writes an array, its items separated by single spaces.</summary>
    /// <param name="array">The array.</param>
    /// <param name="encryptionId">The object for string encryption, or an invalid id.</param>
    /// <param name="depth">The nesting depth.</param>
    private void WriteArray(PdfArray array, PdfObjectId encryptionId, int depth)
    {
        CheckDepth(depth);
        _buffer.WriteByte((byte)'[');
        var items = array.Items;
        for (var i = 0; i < items.Length; i++)
        {
            if (i > 0)
            {
                _buffer.WriteByte((byte)' ');
            }

            WriteValue(items[i], encryptionId, depth);
        }

        _buffer.WriteByte((byte)']');
    }

    /// <summary>Writes a dictionary, skipping null entries.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="encryptionId">The object for string encryption, or an invalid id.</param>
    /// <param name="depth">The nesting depth.</param>
    private void WriteDictionary(PdfDictionary dictionary, PdfObjectId encryptionId, int depth)
    {
        CheckDepth(depth);
        _buffer.Write("<<"u8);
        _ = WriteEntries(dictionary, encryptionId, depth, KnownName.None);
        _buffer.Write(">>"u8);
    }

    /// <summary>Writes a stream dictionary with its /Length set to the data written.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="length">The data length.</param>
    /// <param name="addFlate">Whether to add /Filter /FlateDecode.</param>
    /// <param name="encryptionId">The object for string encryption, or an invalid id.</param>
    private void WriteStreamDictionary(PdfDictionary dictionary, int length, bool addFlate, PdfObjectId encryptionId)
    {
        _buffer.Write("<<"u8);
        if (WriteEntries(dictionary, encryptionId, 1, KnownName.Length) > 0)
        {
            _buffer.WriteByte((byte)' ');
        }

        _buffer.Write("/Length "u8);
        PdfSyntax.WriteInteger(ref _buffer, length);
        if (addFlate)
        {
            _buffer.Write(" /Filter /FlateDecode"u8);
        }

        _buffer.Write(">>"u8);
    }

    /// <summary>Writes the stream keyword, data and endstream keyword.</summary>
    /// <param name="data">The data.</param>
    private void WriteStreamData(ReadOnlySpan<byte> data)
    {
        _buffer.Write("\nstream\r\n"u8);
        _buffer.Write(data);
        _buffer.Write("\r\nendstream"u8);
    }

    /// <summary>Writes a dictionary's entries, separated by single spaces.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="encryptionId">The object for string encryption, or an invalid id.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="skip">A key to leave out, or <see cref="KnownName.None"/>.</param>
    /// <returns>The number of entries written.</returns>
    private int WriteEntries(PdfDictionary dictionary, PdfObjectId encryptionId, int depth, KnownName skip)
    {
        var written = 0;

        // The /Contents of a signature dictionary (one with a /ByteRange) is never encrypted (ISO 32000-2, 7.6.2).
        var clearContents = encryptionId.IsValid && dictionary.ContainsKey(KnownName.ByteRange);
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            var value = dictionary.GetValueAt(i);

            // A repeated key keeps only its last entry, which is the one readers use; ISO 32000 requires unique keys.
            if (value.IsNull || (skip != KnownName.None && key.Is(skip)) || dictionary.IsShadowed(i))
            {
                continue;
            }

            if (written > 0)
            {
                _buffer.WriteByte((byte)' ');
            }

            PdfSyntax.WriteName(ref _buffer, _names.GetSpelling(key));
            _buffer.WriteByte((byte)' ');
            WriteValue(value, clearContents && key.Is(KnownName.Contents) ? default : encryptionId, depth);
            written++;
        }

        return written;
    }

    /// <summary>
    /// Determines whether a stream's raw bytes are already encrypted for the id it is written as. AES-GCM data is never
    /// copied: it is decrypted, which authenticates it, and encrypted again under a new IV.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="encryptionId">The id it is written as.</param>
    /// <returns><see langword="true"/> when the raw bytes can be copied.</returns>
    private readonly bool CanCopyEncrypted(PdfStream stream, PdfObjectId encryptionId) =>
        stream.IsEncrypted && _security is not null && encryptionId.IsValid
        && ReferenceEquals(stream.Dictionary.Owner?.Security, _security)
        && MethodOf(stream.Dictionary, _security) is var method and not CryptMethod.AesGcm
        && (encryptionId == stream.Id || method == CryptMethod.Aes256);
}

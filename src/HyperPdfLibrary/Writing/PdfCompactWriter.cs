// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Rewrites a whole document compactly: only objects reachable from the trailer are kept, renumbered densely from 1;
/// plain objects can be packed into compressed object streams; uncompressed streams are Flate-compressed when that is
/// smaller, and filtered streams are copied without decoding.
/// </summary>
/// <remarks>
/// An encrypted document stays encrypted with the same handler unless <see cref="PdfCompactOptions.RemoveEncryption"/>
/// is set. The first part of /ID is kept, so the file key is unchanged, and every string and stream is encrypted again
/// for its new object number, as RC4 and AES-128 keys depend on it. The header version is raised to 1.5 when object
/// streams are written and kept otherwise. A document that claims PDF/A-1 always gets a classic table. A signed document
/// is refused, as no signature covers the rewritten bytes; save it incrementally, or remove its signatures with
/// <see cref="Document.PdfDocumentSignatureEditing.RemoveSignatures"/> to write an unsigned copy.
/// </remarks>
public static class PdfCompactWriter
{
    /// <summary>The most objects packed into one object stream.</summary>
    private const int ObjectsPerStream = 100;

    /// <summary>The new number of the catalog, the first object reached from the trailer.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The version written when the document has none.</summary>
    private const string DefaultVersion = "1.4";

    /// <summary>The minor version object streams need, in PDF 1.x.</summary>
    private const int ObjectStreamMinor = 5;

    /// <summary>The position of the minor digit in a version such as "1.4".</summary>
    private const int MinorDigit = 2;

    /// <summary>Gets the version written when object streams need a newer one.</summary>
    private static ReadOnlySpan<byte> ObjectStreamVersion => "1.5"u8;

    /// <summary>Gets the line end and keyword that close an indirect object.</summary>
    private static ReadOnlySpan<byte> EndObject => "\nendobj\n"u8;

    /// <summary>Saves the document into a new array.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    public static byte[] Save(PdfObjectStore store, PdfCompactOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        var writer = CreateWriter(store, options);
        try
        {
            Write(store, options, ref writer, null, CancellationToken.None);
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Saves the document into a pooled buffer.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="output">The buffer receiving the file bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    public static void Save(PdfObjectStore store, PdfCompactOptions options, ref PooledBuffer output)
    {
        ArgumentNullException.ThrowIfNull(store);
        var writer = CreateWriter(store, options);
        try
        {
            Write(store, options, ref writer, null, CancellationToken.None);
            output.Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Saves the document to a stream.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="destination">The stream.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Save(PdfObjectStore store, PdfCompactOptions options, Stream destination) => Save(store, options, destination, null);

    /// <summary>Saves the document to a stream and reports what the user should know.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    public static void Save(PdfObjectStore store, PdfCompactOptions options, Stream destination, PdfSaveReport? report)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(destination);
        var writer = CreateWriter(store, options);
        try
        {
            Write(store, options, ref writer, report, CancellationToken.None);
            destination.Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Saves the document to a stream asynchronously.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task SaveAsync(PdfObjectStore store, PdfCompactOptions options, Stream destination, CancellationToken cancellationToken) =>
        SaveAsync(store, options, destination, null, cancellationToken);

    /// <summary>Saves the document to a stream asynchronously and reports what the user should know.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    public static async Task SaveAsync(PdfObjectStore store, PdfCompactOptions options, Stream destination, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(destination);
        var file = Build(store, options, report, out var length, cancellationToken);

        try
        {
            await destination.WriteAsync(file.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (file is not null)
            {
                ArrayPool<byte>.Shared.Return(file);
            }
        }
    }

    /// <summary>Writes <c>%PDF-x.y</c> and a binary comment.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="version">The original version.</param>
    /// <param name="objectStreams">Whether object streams are written, which need 1.5.</param>
    internal static void WriteHeader(ref PdfObjectWriter writer, string version, bool objectStreams)
    {
        writer.WriteRaw("%PDF-"u8);
        if (version.Length == 0)
        {
            version = DefaultVersion;
        }

        if (objectStreams && IsBefore15(version))
        {
            writer.WriteRaw(ObjectStreamVersion);
        }
        else
        {
            foreach (var c in version)
            {
                // The version holds only digits and a dot.
                writer.Buffer.WriteByte((byte)c);
            }
        }

        writer.WriteRaw("\n"u8);
        writer.WriteRaw(PdfXrefWriter.BinaryMarker);
    }

    /// <summary>
    /// Writes one object under its new number. Metadata streams are written without byte filters, as XMP readers expect
    /// them plain and PDF/A-1 forbids a filtered metadata stream (ISO 19005-1 §6.7.2, Cor.2).
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="graph">The objects.</param>
    /// <param name="graphNumber">The object's number in the collected graph.</param>
    /// <param name="outputNumber">The object number written to the file.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary, which is written unencrypted.</param>
    internal static void WriteObject(ref PdfObjectWriter writer, PdfObjectGraph graph, int graphNumber, int outputNumber, int encryptNumber)
    {
        var id = new PdfObjectId(outputNumber, 0);
        var encrypt = graph.GetOldNumber(graphNumber) != encryptNumber;
        var value = graph.GetValue(graphNumber);
        if (value.AsStream() is not { } stream)
        {
            writer.WriteIndirectObject(id, value, encrypt);
            return;
        }

        if (stream.Dictionary.IsName(KnownName.Type, KnownName.Metadata))
        {
            if (IsUnfiltered(stream.Dictionary) || !TryWritePlain(ref writer, stream, id, encrypt))
            {
                writer.WriteIndirectObject(id, value, encrypt);
            }

            return;
        }

        if (!IsUnfiltered(stream.Dictionary))
        {
            // A damaged Flate or LZW stream is written again from the part that decoded, so the saved file needs no repair.
            if (!TryWriteRepaired(ref writer, stream, id, encrypt))
            {
                writer.WriteIndirectObject(id, value, encrypt);
            }

            return;
        }

        writer.WriteObjectHeader(id);
        WriteCompressed(ref writer, stream, encrypt ? id : default);
        writer.WriteRaw(EndObject);
    }

    /// <summary>Creates the writer, with the document's security handler unless encryption is removed.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <returns>The writer.</returns>
    private static PdfObjectWriter CreateWriter(PdfObjectStore store, PdfCompactOptions options) =>
        new(store.Names, options.RemoveEncryption ? null : store.Security);

    /// <summary>Writes the document into a pooled array, so it can be written across an await.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="report">Receives the claim check, or <see langword="null"/>.</param>
    /// <param name="length">The file length.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The pooled array, which the caller returns.</returns>
    private static byte[]? Build(PdfObjectStore store, PdfCompactOptions options, PdfSaveReport? report, out int length, CancellationToken cancellationToken)
    {
        var writer = CreateWriter(store, options);
        try
        {
            Write(store, options, ref writer, report, cancellationToken);
            return writer.Detach(out length);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>
    /// Writes the whole document. A file that claims PDF/A is checked as written; when it breaks a rule, the claim is
    /// removed from the document and the file is written again.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="writer">The writer, replaced when the file is written again.</param>
    /// <param name="report">Receives the claim check, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Write(PdfObjectStore store, PdfCompactOptions options, ref PdfObjectWriter writer, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        var claimed = PdfAClaimGuard.IsClaimed(store);
        WriteFile(store, options, ref writer, cancellationToken);
        if (!claimed || PdfAClaimGuard.CheckOutput(store, writer.WrittenSpan.ToArray(), cancellationToken) is not { } check)
        {
            return;
        }

        report?.Claim = check;
        if (!check.Removed)
        {
            return;
        }

        writer.Dispose();
        writer = CreateWriter(store, options);
        WriteFile(store, options, ref writer, cancellationToken);
    }

    /// <summary>Writes the whole document once.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WriteFile(PdfObjectStore store, PdfCompactOptions options, ref PdfObjectWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SignaturePresence.ThrowIfSigned(store);
        var objectStreams = UsesObjectStreams(store, options);
        var keepEncrypt = store.Security is not null && !options.RemoveEncryption;
        var graph = PdfObjectGraph.Collect(store, keepEncrypt, cancellationToken);
        WriteHeader(ref writer, store.Version, objectStreams);
        writer.SetRenumbering(graph.Map);
        var encryptNumber = keepEncrypt ? PdfXrefWriter.EncryptNumber(store.Trailer) : 0;
        var streamCount = objectStreams ? PackedStreamCount(graph, encryptNumber, cancellationToken) : 0;

        // Entry 0, the objects, the object streams and, with object streams, the cross-reference stream.
        var rows = new XrefRow[graph.Count + streamCount + 1 + (objectStreams ? 1 : 0)];
        rows[0] = XrefRow.FreeHead;
        if (objectStreams)
        {
            WritePacked(ref writer, graph, encryptNumber, rows, cancellationToken);
        }
        else
        {
            WritePlain(ref writer, graph, encryptNumber, rows, cancellationToken);
        }

        var size = rows.Length;
        var trailer = PdfXrefWriter.CreateTrailer(store.Trailer, size, -1, keepEncrypt);
        PdfXrefWriter.SetFileId(trailer, store.Trailer, writer.WrittenSpan, keepEncrypt);
        var sectionOffset = writer.Length;
        if (objectStreams)
        {
            rows[^1] = new(size - 1, XrefEntryType.InFile, sectionOffset, 0);
            PdfXrefWriter.WriteStream(ref writer, rows, trailer, new(size - 1, 0), sectionOffset);
            return;
        }

        PdfXrefWriter.WriteTable(ref writer, rows);
        PdfXrefWriter.WriteTrailer(ref writer, trailer, sectionOffset);
    }

    /// <summary>
    /// Decides whether to write object streams and a cross-reference stream. A document that claims PDF/A-1 keeps a
    /// classic table, as ISO 19005-1 §6.1.4 forbids cross-reference streams and PDF 1.4 has no object streams.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <returns><see langword="true"/> when they are written.</returns>
    private static bool UsesObjectStreams(PdfObjectStore store, PdfCompactOptions options) =>
        options.UseObjectStreams && PdfAClaimReading.ReadPart(store) != PdfAClaim.Part1;

    /// <summary>Determines whether a version is older than 1.5.</summary>
    /// <param name="version">The version, for example "1.4".</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBefore15(string version) =>
        version[0] < '1' || (version[0] == '1' && (version.Length <= MinorDigit || version[MinorDigit] - '0' < ObjectStreamMinor));

    /// <summary>Determines whether an object can go in an object stream.</summary>
    /// <param name="graph">The objects.</param>
    /// <param name="number">The new number.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary, which must stay a plain object.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    /// <remarks>
    /// The catalog, numbered first, also stays plain: readers check it before the security handler exists, and a
    /// catalog inside an encrypted object stream cannot be read then.
    /// </remarks>
    private static bool IsPackable(PdfObjectGraph graph, int number, int encryptNumber) =>
        number != CatalogNumber && graph.GetValue(number).Kind != PdfKind.Stream && graph.GetOldNumber(number) != encryptNumber;

    /// <summary>Counts the object streams needed.</summary>
    /// <param name="graph">The objects.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of object streams.</returns>
    private static int PackedStreamCount(PdfObjectGraph graph, int encryptNumber, CancellationToken cancellationToken)
    {
        var packed = 0;
        for (var number = 1; number <= graph.Count; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            packed += IsPackable(graph, number, encryptNumber) ? 1 : 0;
        }

        return (packed + ObjectsPerStream - 1) / ObjectsPerStream;
    }

    /// <summary>Writes every object plainly.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="graph">The objects.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary.</param>
    /// <param name="rows">The cross-reference entries, filled in.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WritePlain(ref PdfObjectWriter writer, PdfObjectGraph graph, int encryptNumber, XrefRow[] rows, CancellationToken cancellationToken)
    {
        for (var number = 1; number <= graph.Count; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows[number] = new(number, XrefEntryType.InFile, writer.Length, 0);
            WriteObject(ref writer, graph, number, number, encryptNumber);
        }
    }

    /// <summary>Writes streams and the /Encrypt dictionary plainly and packs everything else into object streams.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="graph">The objects.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary.</param>
    /// <param name="rows">The cross-reference entries, filled in.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WritePacked(ref PdfObjectWriter writer, PdfObjectGraph graph, int encryptNumber, XrefRow[] rows, CancellationToken cancellationToken)
    {
        var body = new PdfObjectWriter(graph.Names);
        var packer = default(ObjectStreamPacker);
        try
        {
            body.SetRenumbering(graph.Map);
            var streamNumber = graph.Count + 1;
            for (var number = 1; number <= graph.Count; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsPackable(graph, number, encryptNumber))
                {
                    rows[number] = new(number, XrefEntryType.InFile, writer.Length, 0);
                    WriteObject(ref writer, graph, number, number, encryptNumber);
                    continue;
                }

                rows[number] = new(number, XrefEntryType.Compressed, streamNumber, packer.Count);
                packer.Add(ref body, number, graph.GetValue(number));
                if (packer.Count < ObjectsPerStream)
                {
                    continue;
                }

                FlushPacker(ref writer, ref body, ref packer, streamNumber, rows);
                streamNumber++;
            }

            if (packer.Count > 0)
            {
                FlushPacker(ref writer, ref body, ref packer, streamNumber, rows);
            }
        }
        finally
        {
            packer.Dispose();
            body.Dispose();
        }
    }

    /// <summary>Writes a full object stream.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="body">The packed objects' writer.</param>
    /// <param name="packer">The packed objects' header.</param>
    /// <param name="streamNumber">The object stream's number.</param>
    /// <param name="rows">The cross-reference entries.</param>
    private static void FlushPacker(ref PdfObjectWriter writer, ref PdfObjectWriter body, ref ObjectStreamPacker packer, int streamNumber, XrefRow[] rows)
    {
        rows[streamNumber] = new(streamNumber, XrefEntryType.InFile, writer.Length, 0);
        packer.Flush(ref body, ref writer, new(streamNumber, 0));
    }

    /// <summary>Writes a stream filtered by byte filters again as clean Flate when its data was truncated or damaged.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <returns><see langword="true"/> when the stream was damaged and has been written; <see langword="false"/> when nothing was written.</returns>
    private static bool TryWriteRepaired(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt)
    {
        if (!IsByteFilterChain(stream.Dictionary))
        {
            return false;
        }

        var decoded = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            var probe = new PdfOpenContext(null, new(new()), CancellationToken.None);
            _ = PdfStreamDecoder.Decode(stream, probe, ref decoded);
            if (!probe.HasRepairs)
            {
                return false;
            }

            var dictionary = stream.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Filter);
            _ = dictionary.Remove(KnownName.DecodeParms);
            FlateFilter.Encode(decoded.WrittenSpan, ref compressed);
            var smaller = compressed.Length < decoded.Length;
            writer.WriteObjectHeader(id);
            writer.WriteStream(dictionary, smaller ? compressed.WrittenSpan : decoded.WrittenSpan, encrypt ? id : default, smaller);
            writer.WriteRaw(EndObject);
            return true;
        }
        finally
        {
            compressed.Dispose();
            decoded.Dispose();
        }
    }

    /// <summary>Writes a stream filtered by byte filters as its decoded data, with no filter.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="id">The stream's new object id.</param>
    /// <param name="encrypt">Whether the stream is encrypted for its new number.</param>
    /// <returns><see langword="true"/> when the stream was written; <see langword="false"/> when a filter is not a byte filter and nothing was written.</returns>
    private static bool TryWritePlain(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId id, bool encrypt)
    {
        if (!IsByteFilterChain(stream.Dictionary))
        {
            return false;
        }

        var decoded = default(PooledBuffer);
        try
        {
            var context = new PdfOpenContext(null, new(new()), CancellationToken.None);
            _ = PdfStreamDecoder.Decode(stream, context, ref decoded);
            var dictionary = stream.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Filter);
            _ = dictionary.Remove(KnownName.DecodeParms);
            _ = dictionary.Remove(KnownName.DL);
            writer.WriteObjectHeader(id);
            writer.WriteStream(dictionary, decoded.WrittenSpan, encrypt ? id : default, false);
            writer.WriteRaw(EndObject);
            return true;
        }
        finally
        {
            decoded.Dispose();
        }
    }

    /// <summary>Determines whether every filter of a stream is a byte filter that decodes to the stream's data (no image codec, no crypt).</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when the filters are Flate, LZW, ASCIIHex, ASCII85 or RunLength only.</returns>
    private static bool IsByteFilterChain(PdfDictionary dictionary)
    {
        var filter = dictionary.Get(KnownName.Filter);
        if (filter.AsArray() is not { } array)
        {
            return IsByteFilter(filter.AsName());
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (!IsByteFilter(array.GetName(i)))
            {
                return false;
            }
        }

        return array.Count > 0;
    }

    /// <summary>Determines whether a filter name is a byte filter.</summary>
    /// <param name="name">The filter name.</param>
    /// <returns><see langword="true"/> for Flate, LZW, ASCIIHex, ASCII85 and RunLength.</returns>
    private static bool IsByteFilter(PdfName name) => name.ToKnownName() is KnownName.FlateDecode or KnownName.Fl or KnownName.LZWDecode or KnownName.LZW
        or KnownName.ASCIIHexDecode or KnownName.AHx or KnownName.ASCII85Decode or KnownName.A85 or KnownName.RunLengthDecode or KnownName.RL;

    /// <summary>Determines whether a stream has no filter, so compressing it is lossless and safe.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns><see langword="true"/> when it has neither /Filter nor /DecodeParms.</returns>
    private static bool IsUnfiltered(PdfDictionary dictionary) =>
        dictionary.GetRaw(KnownName.Filter).IsNull && dictionary.GetRaw(KnownName.DecodeParms).IsNull;

    /// <summary>Writes an unfiltered stream, Flate-compressed when that is smaller.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="encryptionId">The id to encrypt for, or an invalid id.</param>
    private static void WriteCompressed(ref PdfObjectWriter writer, PdfStream stream, PdfObjectId encryptionId)
    {
        using var raw = stream.LeaseRawData();
        var data = PdfObjectWriter.PlainData(stream, raw.Span);
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            var smaller = compressed.Length < data.Length;
            writer.WriteStream(stream.Dictionary, smaller ? compressed.WrittenSpan : data, encryptionId, smaller);
        }
        finally
        {
            compressed.Dispose();
        }
    }
}

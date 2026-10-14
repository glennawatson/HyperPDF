// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Rewrites a whole document compactly: objects reachable from the trailer are renumbered densely from 1 (others are
/// kept after them with <see cref="PdfCompactOptions.KeepUnreferenced"/>); plain objects can be packed into compressed
/// object streams; stream data is handled by <see cref="PdfCompactOptions.StreamMode"/>, by default compressing
/// unfiltered streams with Flate when that is smaller and copying filtered streams without decoding.
/// </summary>
/// <remarks>
/// An encrypted document stays encrypted with the same handler unless <see cref="PdfCompactOptions.RemoveEncryption"/>
/// is set, or encrypted with a new key and passwords when <see cref="PdfCompactOptions.Encryption"/> is set (AES-128, AES-256
/// or AES-GCM; never RC4). Only this writer encrypts a file: encryption covers every string and stream, so an incremental
/// save never adds or changes it. The first part of /ID is kept (a document without one gets a new /ID on its first write), the second part is
/// the MD5 hash of the written bytes, and every string and stream is encrypted again for its new object number, as RC4
/// and AES-128 keys depend on it. The header version is the newest of the document's own header and catalog /Version,
/// <see cref="PdfCompactOptions.MinimumVersion"/> and what the features need (1.5 for object streams, 2.0 for AES-GCM);
/// it is never lowered. With <see cref="PdfCompactOptions.Normalisation"/> the pages' content is normalised in the
/// document in memory first, as one undoable edit. A document that claims PDF/A-1 always gets a classic table. A signed document
/// is refused, as no signature covers the rewritten bytes; save it incrementally, or remove its signatures with
/// <see cref="Document.PdfDocumentSignatureEditing.RemoveSignatures"/> to write an unsigned copy. Before writing, every
/// trap network that no longer matches its page is removed from the document, and one with the wrong flags or position is
/// fixed (see <see cref="PdfSaveReport.TrapNetworks"/>).
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

    /// <summary>Saves the document into a new array.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    public static byte[] Save(PdfObjectStore store, PdfCompactOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        using var plan = PdfEncryptionPlan.Create(store, options);
        var writer = CreateWriter(store, options.RemoveEncryption, plan);
        try
        {
            Write(store, options, plan, ref writer, null, CancellationToken.None);
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
        using var plan = PdfEncryptionPlan.Create(store, options);
        var writer = CreateWriter(store, options.RemoveEncryption, plan);
        try
        {
            Write(store, options, plan, ref writer, null, CancellationToken.None);
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
        using var plan = PdfEncryptionPlan.Create(store, options);
        var writer = CreateWriter(store, options.RemoveEncryption, plan);
        try
        {
            Write(store, options, plan, ref writer, report, CancellationToken.None);
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

    /// <summary>
    /// Saves the document to a stream asynchronously and reports what the user should know. A file that breaks the PDF/A
    /// level it claims is repaired, downloading font data the repair needs, and the repaired file is written instead.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout; a repaired file uses the layout its PDF/A level needs.</param>
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
        using var plan = PdfEncryptionPlan.Create(store, options);
        SignaturePresence.ThrowIfSigned(store);
        PdfTrapNetworkGuard.Apply(store, true, report, cancellationToken);
        PdfRequirementsGuard.Apply(store, report, cancellationToken);
        var claimed = PdfAClaimGuard.IsClaimed(store);
        var file = Build(store, options, plan, out var length, cancellationToken);
        try
        {
            var outcome = claimed ? await PdfAClaimGuard.CheckOutputAsync(store, file.AsSpan(0, length).ToArray(), cancellationToken).ConfigureAwait(false) : null;
            report?.Claim = outcome?.Check;
            if (outcome?.Repaired is { } repaired)
            {
                await destination.WriteAsync(repaired, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (outcome is { Check.Removed: true })
            {
                var rebuilt = Build(store, options, plan, out length, cancellationToken);
                ReturnBuffer(file);
                file = rebuilt;
            }

            await destination.WriteAsync(file.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReturnBuffer(file);
        }
    }

    /// <summary>
    /// Saves the document into a new array without checking its PDF/A claim, for the PDF/A conversion, which validates
    /// its own output and must not change the claim of the document it copies.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="PdfException">The document is signed, or a value cannot be written.</exception>
    internal static byte[] SaveUnchecked(PdfObjectStore store, in PdfCompactOptions options)
    {
        using var plan = PdfEncryptionPlan.Create(store, options);
        var writer = CreateWriter(store, options.RemoveEncryption, plan);
        try
        {
            WriteFile(store, options, plan, ref writer, CancellationToken.None);
            return writer.ToArray();
        }
        finally
        {
            writer.Dispose();
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteObject(ref PdfObjectWriter writer, PdfObjectGraph graph, int graphNumber, int outputNumber, int encryptNumber) =>
        WriteObject(ref writer, graph, graphNumber, outputNumber, encryptNumber, StreamPolicy.Default);

    /// <summary>Writes one object under its new number, handling its stream data by a policy.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="graph">The objects.</param>
    /// <param name="graphNumber">The object's number in the collected graph.</param>
    /// <param name="outputNumber">The object number written to the file.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary, which is written unencrypted.</param>
    /// <param name="policy">What to do with stream data.</param>
    internal static void WriteObject(ref PdfObjectWriter writer, PdfObjectGraph graph, int graphNumber, int outputNumber, int encryptNumber, StreamPolicy policy)
    {
        var id = new PdfObjectId(outputNumber, 0);
        var encrypt = graph.GetOldNumber(graphNumber) != encryptNumber;
        var value = graph.GetValue(graphNumber);
        if (value.AsStream() is not { } stream)
        {
            writer.WriteIndirectObject(id, value, encrypt);
            return;
        }

        PdfStreamWriting.Write(ref writer, value, stream, id, encrypt, policy);
    }

    /// <summary>Creates the writer, with the new encryption if there is any, else the document's security handler unless encryption is removed.</summary>
    /// <param name="store">The document.</param>
    /// <param name="removeEncryption">Whether the file is written decrypted.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <returns>The writer.</returns>
    private static PdfObjectWriter CreateWriter(PdfObjectStore store, bool removeEncryption, PdfEncryptionPlan? plan) =>
        new(store.Names, plan?.Handler ?? (removeEncryption ? null : store.Security));

    /// <summary>Writes the document once, without the PDF/A claim check, into a pooled array for an await.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <param name="length">The file length.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The pooled array, which the caller returns.</returns>
    private static byte[]? Build(PdfObjectStore store, in PdfCompactOptions options, PdfEncryptionPlan? plan, out int length, CancellationToken cancellationToken)
    {
        var writer = CreateWriter(store, options.RemoveEncryption, plan);
        try
        {
            WriteFile(store, options, plan, ref writer, cancellationToken);
            return writer.Detach(out length);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Returns a pooled array from <see cref="Build"/>.</summary>
    /// <param name="file">The array, or <see langword="null"/>.</param>
    private static void ReturnBuffer(byte[]? file)
    {
        if (file is not null)
        {
            ArrayPool<byte>.Shared.Return(file);
        }
    }

    /// <summary>
    /// Writes the whole document. A file that claims PDF/A is checked as written; when it breaks a rule, the repaired
    /// file replaces it, or, when it cannot be repaired, the claim is removed from the document and the file is written
    /// again.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <param name="writer">The writer, replaced when the file is repaired or written again.</param>
    /// <param name="report">Receives the claim check, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void Write(PdfObjectStore store, in PdfCompactOptions options, PdfEncryptionPlan? plan, ref PdfObjectWriter writer, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        SignaturePresence.ThrowIfSigned(store);
        PdfTrapNetworkGuard.Apply(store, true, report, cancellationToken);
        PdfRequirementsGuard.Apply(store, report, cancellationToken);
        var claimed = PdfAClaimGuard.IsClaimed(store);
        WriteFile(store, options, plan, ref writer, cancellationToken);
        if (!claimed || PdfAClaimGuard.CheckOutput(store, writer.WrittenSpan.ToArray(), cancellationToken) is not { } outcome)
        {
            return;
        }

        report?.Claim = outcome.Check;
        if (outcome.Repaired is { } repaired)
        {
            writer.Dispose();
            writer = new(store.Names, null);
            writer.WriteRaw(repaired);
            return;
        }

        if (!outcome.Check.Removed)
        {
            return;
        }

        writer.Dispose();
        writer = CreateWriter(store, options.RemoveEncryption, plan);
        WriteFile(store, options, plan, ref writer, cancellationToken);
    }

    /// <summary>Writes the whole document once.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WriteFile(PdfObjectStore store, in PdfCompactOptions options, PdfEncryptionPlan? plan, ref PdfObjectWriter writer, CancellationToken cancellationToken)
    {
        PrepareDocument(store, options, plan, cancellationToken);
        var objectStreams = UsesObjectStreams(store, options);

        var keepEncrypt = KeepsOwnEncryption(store, options, plan);
        var seal = keepEncrypt ? PdfMacSeal.Create(store) : null;
        var graph = PdfObjectGraph.Collect(store, keepEncrypt, options.KeepUnreferenced, cancellationToken);
        WriteHeader(ref writer, PdfVersionRules.Resolve(store, MinimumVersion(options, plan), objectStreams).ToString(), false);
        writer.SetRenumbering(graph.Map);
        var encryptNumber = keepEncrypt ? PdfXrefWriter.EncryptNumber(store.Trailer) : 0;
        var streamCount = objectStreams ? PackedStreamCount(graph, encryptNumber, cancellationToken) : 0;
        var policy = StreamPolicy.Of(options, StreamEncoder.UsesLzw(options.RecompressEncoding) && PdfAClaimReading.ClaimsPdfAOrPdfUa(store));

        // Entry 0, the objects, the object streams and, with object streams, the cross-reference stream.
        var rows = new XrefRow[graph.Count + streamCount + 1 + (objectStreams ? 1 : 0)];
        rows[0] = XrefRow.FreeHead;
        if (objectStreams)
        {
            WritePacked(ref writer, graph, encryptNumber, rows, policy, cancellationToken);
        }
        else
        {
            WritePlain(ref writer, graph, encryptNumber, rows, policy, cancellationToken);
        }

        var trailer = CreateTrailer(store, rows.Length, keepEncrypt, plan, writer.WrittenSpan);

        // The new file is encrypted and needs a PDF MAC, so its single revision gets a fresh one; without one the save stops.
        seal?.AddTo(trailer);
        WriteSection(ref writer, rows, trailer, new(seal, null, cancellationToken), objectStreams);
    }

    /// <summary>
    /// Determines whether the document's own encryption is kept: it has some, the save does not remove it and no new
    /// encryption replaces it. New encryption leaves the old /Encrypt dictionary and PDF MAC behind.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the old handler and dictionary are written again.</returns>
    private static bool KeepsOwnEncryption(PdfObjectStore store, in PdfCompactOptions options, PdfEncryptionPlan? plan) =>
        store.Security is not null && !options.RemoveEncryption && plan is null;

    /// <summary>Gets the oldest version the file may declare: the options' own, or what the new encryption needs.</summary>
    /// <param name="options">The layout.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <returns>The version.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfVersion MinimumVersion(in PdfCompactOptions options, PdfEncryptionPlan? plan) =>
        PdfVersion.Max(options.MinimumVersion, plan?.MinimumVersion ?? default);

    /// <summary>Checks the document can be written and makes the in-memory changes the file needs: normalised content and the AES-GCM extension.</summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void PrepareDocument(PdfObjectStore store, in PdfCompactOptions options, PdfEncryptionPlan? plan, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SignaturePresence.ThrowIfSigned(store);
        PdfReproducibility.ThrowIfNotReproducible(store, options);
        _ = PdfContentNormaliser.Apply(store, options, cancellationToken);
        if (plan is { UsesAesGcm: true })
        {
            AesGcmExtension.EnsureDeclared(store);
        }
    }

    /// <summary>Makes the trailer of the new file: the old document's, or with the new encryption's dictionary and id.</summary>
    /// <param name="store">The document.</param>
    /// <param name="size">The /Size.</param>
    /// <param name="keepEncrypt">Whether the document's own /Encrypt is kept.</param>
    /// <param name="plan">The new encryption, or <see langword="null"/>.</param>
    /// <param name="written">The bytes written so far, which the second part of /ID hashes.</param>
    /// <returns>The trailer.</returns>
    private static PdfDictionary CreateTrailer(PdfObjectStore store, int size, bool keepEncrypt, PdfEncryptionPlan? plan, ReadOnlySpan<byte> written)
    {
        var trailer = PdfXrefWriter.CreateTrailer(store.Trailer, size, -1, keepEncrypt);
        if (plan is null)
        {
            PdfXrefWriter.SetFileId(trailer, store.Trailer, written, keepEncrypt);
            return trailer;
        }

        // The keys depend on the first part of the id, so the plan's is kept; the /Encrypt dictionary is direct and never encrypted.
        trailer.Set(KnownName.Encrypt, PdfValue.FromDictionary(plan.Encrypt));
        PdfXrefWriter.SetFileId(trailer, plan.FileId, written, true);
        return trailer;
    }

    /// <summary>Writes the cross-reference section and the trailer, and the PDF MAC the trailer holds.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The entries; the last is the cross-reference stream's own when object streams are written.</param>
    /// <param name="trailer">The trailer.</param>
    /// <param name="sealing">The PDF MAC of the file.</param>
    /// <param name="objectStreams">Whether the section is a cross-reference stream.</param>
    private static void WriteSection(ref PdfObjectWriter writer, XrefRow[] rows, PdfDictionary trailer, in PdfMacSealing sealing, bool objectStreams)
    {
        var size = rows.Length;
        var sectionOffset = writer.Length;
        if (objectStreams)
        {
            rows[^1] = new(size - 1, XrefEntryType.InFile, sectionOffset, 0);
            PdfXrefWriter.WriteStream(ref writer, rows, trailer, new(size - 1, 0), sectionOffset, in sealing);
            return;
        }

        PdfXrefWriter.WriteTable(ref writer, rows, trailer, sectionOffset, in sealing);
    }

    /// <summary>
    /// Decides whether to write object streams and a cross-reference stream. A document that claims PDF/A-1 keeps a
    /// classic table, as ISO 19005-1 §6.1.4 forbids cross-reference streams and PDF 1.4 has no object streams.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="options">The layout.</param>
    /// <returns><see langword="true"/> when they are written.</returns>
    private static bool UsesObjectStreams(PdfObjectStore store, in PdfCompactOptions options) =>
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
    /// <param name="policy">What to do with stream data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WritePlain(ref PdfObjectWriter writer, PdfObjectGraph graph, int encryptNumber, XrefRow[] rows, StreamPolicy policy, CancellationToken cancellationToken)
    {
        for (var number = 1; number <= graph.Count; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows[number] = new(number, XrefEntryType.InFile, writer.Length, 0);
            WriteObject(ref writer, graph, number, number, encryptNumber, policy);
        }
    }

    /// <summary>Writes streams and the /Encrypt dictionary plainly and packs everything else into object streams.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="graph">The objects.</param>
    /// <param name="encryptNumber">The old number of the /Encrypt dictionary.</param>
    /// <param name="rows">The cross-reference entries, filled in.</param>
    /// <param name="policy">What to do with stream data.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WritePacked(ref PdfObjectWriter writer, PdfObjectGraph graph, int encryptNumber, XrefRow[] rows, StreamPolicy policy, CancellationToken cancellationToken)
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
                    WriteObject(ref writer, graph, number, number, encryptNumber, policy);
                    continue;
                }

                rows[number] = new(number, XrefEntryType.Compressed, streamNumber, packer.Count);
                packer.Add(ref body, number, graph.GetValue(number));
                if (packer.Count < ObjectsPerStream)
                {
                    continue;
                }

                FlushPacker(ref writer, ref body, ref packer, streamNumber, rows, policy);
                streamNumber++;
            }

            if (packer.Count > 0)
            {
                FlushPacker(ref writer, ref body, ref packer, streamNumber, rows, policy);
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
    /// <param name="policy">How the object stream is compressed.</param>
    private static void FlushPacker(ref PdfObjectWriter writer, ref PdfObjectWriter body, ref ObjectStreamPacker packer, int streamNumber, XrefRow[] rows, StreamPolicy policy)
    {
        rows[streamNumber] = new(streamNumber, XrefEntryType.InFile, writer.Length, 0);
        packer.Flush(ref body, ref writer, new(streamNumber, 0), policy);
    }
}

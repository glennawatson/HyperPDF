// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Saves a document as its original bytes followed by an incremental update holding the changed and added objects and a
/// new cross-reference section. The original bytes are never touched, so digital signatures over them stay valid.
/// </summary>
/// <remarks>
/// The new section is a cross-reference stream when the original's newest section was one, otherwise a classic table.
/// When the original table had to be rebuilt from a damaged file there is no section to chain to, so every live object
/// is written again and the new section stands alone without /Prev. /ID keeps its first part and gets a new second part,
/// the MD5 of the update. Before writing, a trap network on an edited page that no longer matches the page is removed
/// from the document, and one with the wrong flags or position is fixed (see <see cref="PdfSaveReport.TrapNetworks"/>).
/// </remarks>
public static class PdfIncrementalWriter
{
    /// <summary>The entries beyond the objects: the free list head and the cross-reference stream's own entry.</summary>
    private const int ExtraRows = 2;

    /// <summary>The bytes of the original file copied at a time.</summary>
    private const int CopyChunk = 1 << 20;

    /// <summary>Saves the document into a new array.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    public static byte[] Save(PdfObjectStore store)
    {
        var output = default(PooledBuffer);
        try
        {
            Save(store, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Saves the document into a pooled buffer.</summary>
    /// <param name="store">The document.</param>
    /// <param name="output">The buffer receiving the file bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    public static void Save(PdfObjectStore store, ref PooledBuffer output)
    {
        ArgumentNullException.ThrowIfNull(store);
        PdfTrapNetworkGuard.Apply(store, IsStandalone(store), null, CancellationToken.None);
        PdfRequirementsGuard.Apply(store, null, CancellationToken.None);
        PdfUaClaimGuard.Apply(store, IsStandalone(store), null, CancellationToken.None);
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            if (WriteUpdate(store, ref writer, null, CancellationToken.None) is { } repaired)
            {
                output.Write(repaired);
                return;
            }

            CopyOriginal(store.Source, ref output);
            output.Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Saves the document to a stream.</summary>
    /// <param name="store">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Save(PdfObjectStore store, Stream destination) => Save(store, destination, null);

    /// <summary>
    /// Saves the document to a stream and reports what the user should know. When the saved file would break the PDF/A
    /// level it claims, the repaired file is written in full instead of an update.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    public static void Save(PdfObjectStore store, Stream destination, PdfSaveReport? report)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(destination);
        PdfTrapNetworkGuard.Apply(store, IsStandalone(store), report, CancellationToken.None);
        PdfRequirementsGuard.Apply(store, report, CancellationToken.None);
        PdfUaClaimGuard.Apply(store, IsStandalone(store), report, CancellationToken.None);
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            if (WriteUpdate(store, ref writer, report, CancellationToken.None) is { } repaired)
            {
                destination.Write(repaired);
                return;
            }

            CopyOriginal(store.Source, destination);
            destination.Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Saves the document to a stream asynchronously.</summary>
    /// <param name="store">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task SaveAsync(PdfObjectStore store, Stream destination, CancellationToken cancellationToken) =>
        SaveAsync(store, destination, null, cancellationToken);

    /// <summary>
    /// Saves the document to a stream asynchronously and reports what the user should know. When the saved file would
    /// break the PDF/A level it claims, it is repaired, downloading font data the repair needs, and the repaired file is
    /// written in full instead of an update.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="report">Receives the check of the document's PDF/A claim, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException">An argument other than <paramref name="report"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    public static async Task SaveAsync(PdfObjectStore store, Stream destination, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(destination);
        PdfTrapNetworkGuard.Apply(store, IsStandalone(store), report, cancellationToken);
        PdfRequirementsGuard.Apply(store, report, cancellationToken);
        PdfUaClaimGuard.Apply(store, IsStandalone(store), report, cancellationToken);
        var update = BuildUpdate(store, out var length, cancellationToken);
        try
        {
            report?.PdfMacWritten = length > 0 && PdfMacSeal.IsNeeded(store);
            var outcome = length > 0 && PdfAClaimGuard.IsClaimed(store)
                ? await PdfAClaimGuard.CheckOutputAsync(store, SavedFile(store.Source, update.AsSpan(0, length)), cancellationToken).ConfigureAwait(false)
                : null;
            report?.Claim = Rewritten(outcome);
            if (outcome?.Repaired is { } repaired)
            {
                // A repaired file is written in full by another pipeline, which cannot make the PDF MAC.
                PdfMacSeal.ThrowIfNeeded(store, "Repairing the PDF/A claim");
                await destination.WriteAsync(repaired, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (outcome is { Check.Removed: true })
            {
                var rebuilt = BuildUpdate(store, out length, cancellationToken);
                ReturnBuffer(update);
                update = rebuilt;
            }

            await CopyOriginalAsync(store.Source, destination, cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(update.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReturnBuffer(update);
        }
    }

    /// <summary>
    /// Builds the whole signed-file candidate: the original bytes followed by the update, for a signing operation that
    /// then patches the byte range and the signature into it. The update is the plain one; no PDF/A repair rewrites it, as
    /// a rewrite would break the signatures already in the file.
    /// </summary>
    /// <param name="store">The document, with the signature objects already added.</param>
    /// <param name="attachedAuthCode">
    /// The trailer's <c>/AuthCode</c> entry for a PDF MAC attached to the signature (ISO/TS 32004:2024 6.5.2), or
    /// <see langword="null"/> to seal the revision with a standalone PDF MAC as an ordinary save does.
    /// </param>
    /// <param name="updateStart">Receives where the update starts, which is the length of the original file.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The file bytes.</returns>
    /// <exception cref="PdfException">The file is too large to hold in memory, or an edited value cannot be written.</exception>
    internal static byte[] BuildSigningFile(PdfObjectStore store, PdfDictionary? attachedAuthCode, out int updateStart, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        PdfTrapNetworkGuard.Apply(store, IsStandalone(store), null, cancellationToken);
        PdfRequirementsGuard.Apply(store, null, cancellationToken);
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            WriteUpdateOnce(store, ref writer, attachedAuthCode, cancellationToken);
            if (store.Source.Length + writer.Length > Array.MaxLength)
            {
                throw new PdfException(PdfError.File, "The file is too large to sign in memory.");
            }

            updateStart = (int)store.Source.Length;
            var file = new byte[updateStart + writer.Length];
            for (var position = 0; position < updateStart; position += CopyChunk)
            {
                var count = Math.Min(CopyChunk, updateStart - position);
                using var window = store.Source.Lease(position, count);
                window.Span.CopyTo(file.AsSpan(position, count));
            }

            writer.WrittenSpan.CopyTo(file.AsSpan(updateStart));
            return file;
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes the update once, without the PDF/A claim check, into a pooled array for an await.</summary>
    /// <param name="store">The document.</param>
    /// <param name="length">The update's length.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The pooled array, which the caller returns.</returns>
    private static byte[]? BuildUpdate(PdfObjectStore store, out int length, CancellationToken cancellationToken)
    {
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            WriteUpdateOnce(store, ref writer, null, cancellationToken);
            return writer.Detach(out length);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Returns a pooled array from <see cref="BuildUpdate"/>.</summary>
    /// <param name="update">The array, or <see langword="null"/>.</param>
    private static void ReturnBuffer(byte[]? update)
    {
        if (update is not null)
        {
            ArrayPool<byte>.Shared.Return(update);
        }
    }

    /// <summary>Gets the check to report, marking a repair as a full rewrite of the file.</summary>
    /// <param name="outcome">The outcome, or <see langword="null"/>.</param>
    /// <returns>The check, or <see langword="null"/>.</returns>
    private static PdfAClaimCheck? Rewritten(PdfAClaimOutcome? outcome) =>
        outcome is { } found ? found.Check with { Rewritten = found.Repaired is not null } : null;

    /// <summary>
    /// Writes the update. When the saved file claims PDF/A, the original and the update are checked together. When they
    /// break a rule, the repaired file is returned, to be written in full; when it cannot be repaired, the claim is
    /// removed from the document and the update is written again.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="writer">The writer; its first byte follows the original file. It is replaced when the update is written again.</param>
    /// <param name="report">Receives the claim check, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The repaired file to write instead of the original and the update, or <see langword="null"/>.</returns>
    private static byte[]? WriteUpdate(PdfObjectStore store, ref PdfObjectWriter writer, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        WriteUpdateOnce(store, ref writer, null, cancellationToken);
        report?.PdfMacWritten = writer.Length > 0 && PdfMacSeal.IsNeeded(store);
        if (writer.Length == 0 || !PdfAClaimGuard.IsClaimed(store)
            || PdfAClaimGuard.CheckOutput(store, SavedFile(store.Source, writer.WrittenSpan), cancellationToken) is not { } outcome)
        {
            return null;
        }

        report?.Claim = Rewritten(outcome);
        if (outcome.Repaired is not null || !outcome.Check.Removed)
        {
            // A repaired file is written in full by another pipeline, which cannot make the PDF MAC.
            if (outcome.Repaired is not null)
            {
                PdfMacSeal.ThrowIfNeeded(store, "Repairing the PDF/A claim");
            }

            return outcome.Repaired;
        }

        writer.Dispose();
        writer = new(store.Names, store.Security);
        WriteUpdateOnce(store, ref writer, null, cancellationToken);
        return null;
    }

    /// <summary>Joins the original file and an update into the file a save writes.</summary>
    /// <param name="file">The original file.</param>
    /// <param name="update">The update.</param>
    /// <returns>The saved file.</returns>
    private static byte[] SavedFile(PdfByteSource file, ReadOnlySpan<byte> update)
    {
        var output = default(PooledBuffer);
        try
        {
            CopyOriginal(file, ref output);
            output.Write(update);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Writes the update once: the objects, the cross-reference section and the trailer.</summary>
    /// <param name="store">The document.</param>
    /// <param name="writer">The writer; its first byte follows the original file.</param>
    /// <param name="attachedAuthCode">
    /// The trailer's <c>/AuthCode</c> value for a PDF MAC attached to a signature (ISO/TS 32004:2024 6.5.2), or
    /// <see langword="null"/> to seal the revision with a standalone PDF MAC.
    /// </param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WriteUpdateOnce(PdfObjectStore store, ref PdfObjectWriter writer, PdfDictionary? attachedAuthCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StoreRedaction.ThrowIfCompactSaveRequired(store);
        var file = store.Source;

        var standalone = IsStandalone(store);

        // An unchanged document is saved as its original bytes, which keeps signatures and conformance claims as they were.
        if (!standalone && !StoreEditing.HasChanges(store))
        {
            return;
        }

        // One snapshot of the edits, so an edit made during the save cannot split the update.
        var edits = WithExtensionCatalog(store, StoreEditing.GetEditedObjects(store, out var storeSize));

        // A document that needs a PDF MAC gets a fresh one for this revision, or the save stops here, before anything is written.
        var seal = attachedAuthCode is null ? PdfMacSeal.Create(store) : null;
        WriteSeparator(ref writer, file);
        var objects = standalone ? LiveObjects(store, cancellationToken) : edits;
        var rows = new XrefRow[objects.Length + ExtraRows];
        var count = WriteObjects(store, objects, standalone, ref writer, rows, cancellationToken);
        var size = Math.Max(Math.Max(store.Trailer.GetInt32(KnownName.Size), storeSize), objects.Length == 0 ? 0 : objects[^1].Number + 1);
        var trailer = PdfXrefWriter.CreateTrailer(store.Trailer, size, standalone ? -1 : store.StartXref, true);
        PdfXrefWriter.CopyExtraKeys(trailer, store.Trailer);

        // The copied /AuthCode belongs to the previous revision and does not cover this one.
        _ = trailer.Remove(store.Names.Intern("AuthCode"u8));
        seal?.AddTo(trailer);
        if (attachedAuthCode is not null)
        {
            trailer.Set(store.Names.Intern("AuthCode"u8), PdfValue.FromDictionary(attachedAuthCode));
        }

        SetUpdateId(trailer, store, objects.Length > 0, writer.WrittenSpan);

        // Offsets count from the header, as viewers read them.
        var relativeOffset = file.Length + writer.Length - store.OffsetBase;
        if (store.UsesXrefStreams)
        {
            rows[count] = new(size, XrefEntryType.InFile, relativeOffset, 0);
            trailer.Set(KnownName.Size, PdfValue.FromInteger(size + 1));
            PdfXrefWriter.WriteStream(ref writer, rows.AsSpan(0, count + 1), trailer, new(size, 0), relativeOffset, new(seal, file, cancellationToken));
            return;
        }

        PdfXrefWriter.WriteTable(ref writer, rows.AsSpan(0, count), trailer, relativeOffset, new(seal, file, cancellationToken));
    }

    /// <summary>Determines whether the update must describe every object: a rebuilt table has no section to point <c>/Prev</c> at.</summary>
    /// <param name="store">The document.</param>
    /// <returns><see langword="true"/> when it must.</returns>
    private static bool IsStandalone(PdfObjectStore store) => store.WasRepaired || store.StartXref < 0;

    /// <summary>
    /// Adds the catalog to the changed objects when an AES-GCM document does not declare the ISO/TS 32003 extension, or a
    /// document that needs a PDF MAC does not declare the ISO/TS 32004 extension, so the update writes the declaration. The
    /// save repairs add the entries as the catalog is written.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="edits">The changed objects, ascending.</param>
    /// <returns>The changed objects, with the catalog included when it needs a declaration.</returns>
    private static PdfEditedObject[] WithExtensionCatalog(PdfObjectStore store, PdfEditedObject[] edits)
    {
        var root = store.Trailer.GetRaw(KnownName.Root);
        var undeclared = (AesGcmExtension.IsRequired(store) && !AesGcmExtension.IsDeclared(store.Catalog, store.Names))
            || (PdfMacExtension.IsRequired(store) && !PdfMacExtension.IsDeclared(store.Catalog, store.Names));
        if (!undeclared || !root.IsReference)
        {
            return edits;
        }

        var number = root.AsReference().Number;
        var at = 0;
        while (at < edits.Length && edits[at].Number < number)
        {
            at++;
        }

        if (at < edits.Length && edits[at].Number == number)
        {
            return edits;
        }

        var result = new PdfEditedObject[edits.Length + 1];
        edits.AsSpan(0, at).CopyTo(result);
        result[at] = new(number, PdfValue.FromDictionary(store.Catalog), false, StoreEditing.GetGeneration(store, number));
        edits.AsSpan(at).CopyTo(result.AsSpan(at + 1));
        return result;
    }

    /// <summary>Starts the update on a new line when the original file does not end with one.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="file">The original file.</param>
    private static void WriteSeparator(ref PdfObjectWriter writer, PdfByteSource file)
    {
        if (file.ByteAt(file.Length - 1) is not ('\n' or '\r'))
        {
            writer.WriteRaw("\n"u8);
        }
    }

    /// <summary>
    /// Sets the update's /ID. With no objects written the second part would be constant, so it hashes the original file
    /// instead of the update.
    /// </summary>
    /// <param name="trailer">The new trailer.</param>
    /// <param name="store">The document.</param>
    /// <param name="wroteObjects">Whether the update holds objects.</param>
    /// <param name="written">The update's bytes so far.</param>
    private static void SetUpdateId(PdfDictionary trailer, PdfObjectStore store, bool wroteObjects, ReadOnlySpan<byte> written)
    {
        if (wroteObjects)
        {
            PdfXrefWriter.SetFileId(trailer, store.Trailer, written, store.Security is not null);
        }
        else
        {
            PdfXrefWriter.SetFileId(trailer, store.Trailer, store.Source, store.Security is not null);
        }
    }

    /// <summary>Writes the changed objects and fills their cross-reference rows, chaining the free entries of deleted ones.</summary>
    /// <param name="store">The document.</param>
    /// <param name="objects">The objects, ascending.</param>
    /// <param name="standalone">Whether the update stands alone, and so starts with the free list head.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The rows, filled in.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of rows.</returns>
    /// <remarks>
    /// An update that only changes the trailer also starts with the free list head, as a classic section needs at least
    /// one subsection (ISO 32000-2 §7.5.4).
    /// </remarks>
    private static int WriteObjects(PdfObjectStore store, PdfEditedObject[] objects, bool standalone, ref PdfObjectWriter writer, XrefRow[] rows, CancellationToken cancellationToken)
    {
        var count = 0;
        var previousFree = -1;
        if (standalone || objects.Length == 0 || HasDeleted(objects))
        {
            rows[count] = XrefRow.FreeHead;
            previousFree = count;
            count++;
        }

        var encryptNumber = PdfXrefWriter.EncryptNumber(store.Trailer);
        if (objects.Length == 0)
        {
            return count;
        }

        using var repairs = new PdfSaveRepairs(store, false);
        foreach (var item in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Deleted)
            {
                // Each free entry names the next one; the last names zero.
                rows[previousFree] = rows[previousFree] with
                {
                    Location = item.Number
                };
                rows[count] = new(item.Number, XrefEntryType.Free, 0, item.Generation);
                previousFree = count;
                count++;
                continue;
            }

            rows[count] = new(item.Number, XrefEntryType.InFile, store.Source.Length + writer.Length - store.OffsetBase, item.Generation);
            count++;
            writer.WriteIndirectObject(new(item.Number, item.Generation), repairs.Fix(item.Number, item.Value), item.Number != encryptNumber);
        }

        return count;
    }

    /// <summary>Determines whether any of the objects was deleted.</summary>
    /// <param name="objects">The objects.</param>
    /// <returns><see langword="true"/> when one was.</returns>
    private static bool HasDeleted(PdfEditedObject[] objects)
    {
        foreach (var item in objects)
        {
            if (item.Deleted)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lists every object that holds a value, leaving out old cross-reference and object streams.</summary>
    /// <param name="store">The document.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The objects, ascending.</returns>
    private static PdfEditedObject[] LiveObjects(PdfObjectStore store, CancellationToken cancellationToken)
    {
        var objects = new List<PdfEditedObject>();
        for (var number = 1; number < store.Size; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = StoreReading.GetObject(store, new(number, 0));
            if (!value.IsNull && !IsStructureStream(value))
            {
                objects.Add(new(number, value, false, StoreEditing.GetGeneration(store, number)));
            }
        }

        return [.. objects];
    }

    /// <summary>Copies the original file into a pooled buffer a window at a time.</summary>
    /// <param name="source">The original file.</param>
    /// <param name="output">The buffer.</param>
    private static void CopyOriginal(PdfByteSource source, ref PooledBuffer output)
    {
        for (var position = 0L; position < source.Length; position += CopyChunk)
        {
            using var window = source.Lease(position, (int)Math.Min(CopyChunk, source.Length - position));
            output.Write(window.Span);
        }
    }

    /// <summary>Copies the original file to a stream a window at a time.</summary>
    /// <param name="source">The original file.</param>
    /// <param name="destination">The stream.</param>
    private static void CopyOriginal(PdfByteSource source, Stream destination)
    {
        for (var position = 0L; position < source.Length; position += CopyChunk)
        {
            using var window = source.Lease(position, (int)Math.Min(CopyChunk, source.Length - position));
            destination.Write(window.Span);
        }
    }

    /// <summary>Copies the original file to a stream asynchronously, through a pooled buffer.</summary>
    /// <param name="source">The original file.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    private static async Task CopyOriginalAsync(PdfByteSource source, Stream destination, CancellationToken cancellationToken)
    {
        if (source.WholeArray is { } array)
        {
            await destination.WriteAsync(array, cancellationToken).ConfigureAwait(false);
            return;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(CopyChunk);
        try
        {
            for (var position = 0L; position < source.Length; position += CopyChunk)
            {
                var read = await source.ReadAsync(position, buffer.AsMemory(0, CopyChunk), cancellationToken).ConfigureAwait(false);
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Determines whether a value is a cross-reference or object stream, which a new section replaces.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsStructureStream(PdfValue value) =>
        value.AsStream()?.Dictionary is { } dictionary
        && (dictionary.IsName(
        KnownName.Type,
        KnownName.XRef) || dictionary.IsName(
        KnownName.Type,
        KnownName.ObjStm));
}

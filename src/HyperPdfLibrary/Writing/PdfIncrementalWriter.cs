// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
/// the MD5 of the update.
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
        CopyOriginal(store.Source, ref output);
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            WriteUpdate(store, ref writer);
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
    public static void Save(PdfObjectStore store, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(destination);
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            WriteUpdate(store, ref writer);
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
    public static async Task SaveAsync(PdfObjectStore store, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(destination);
        byte[]? update;
        int length;
        using (var scope = PdfCancellation.Enter(cancellationToken))
        {
            update = BuildUpdate(store, out length);
        }

        try
        {
            await CopyOriginalAsync(store.Source, destination, cancellationToken).ConfigureAwait(false);
            await destination.WriteAsync(update.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (update is not null)
            {
                ArrayPool<byte>.Shared.Return(update);
            }
        }
    }

    /// <summary>Writes the update into a pooled array, so it can be written across an await.</summary>
    /// <param name="store">The document.</param>
    /// <param name="length">The update's length.</param>
    /// <returns>The pooled array, which the caller returns.</returns>
    private static byte[]? BuildUpdate(PdfObjectStore store, out int length)
    {
        var writer = new PdfObjectWriter(store.Names, store.Security);
        try
        {
            WriteUpdate(store, ref writer);
            return writer.Detach(out length);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes the update: the objects, the cross-reference section and the trailer.</summary>
    /// <param name="store">The document.</param>
    /// <param name="writer">The writer; its first byte follows the original file.</param>
    private static void WriteUpdate(PdfObjectStore store, ref PdfObjectWriter writer)
    {
        StoreRedaction.ThrowIfCompactSaveRequired(store);
        var file = store.Source;
        if (file.ByteAt(file.Length - 1) is not ('\n' or '\r'))
        {
            writer.WriteRaw("\n"u8);
        }

        // A rebuilt table has no section to point /Prev at, so the update must describe every object.
        var standalone = store.WasRepaired || store.StartXref < 0;

        // One snapshot of the edits, so an edit made during the save cannot split the update.
        var edits = StoreEditing.GetEditedObjects(store, out var storeSize);
        var objects = standalone ? LiveObjects(store) : edits;
        var rows = new XrefRow[objects.Length + ExtraRows];
        var count = WriteObjects(store, objects, standalone, ref writer, rows);
        var size = Math.Max(Math.Max(store.Trailer.GetInt32(KnownName.Size), storeSize), objects.Length == 0 ? 0 : objects[^1].Number + 1);
        var trailer = PdfXrefWriter.CreateTrailer(store.Trailer, size, standalone ? -1 : store.StartXref, true);
        PdfXrefWriter.CopyExtraKeys(trailer, store.Trailer);

        // With nothing written the second id part would be constant, so it hashes the original file instead.
        if (objects.Length == 0)
        {
            PdfXrefWriter.SetFileId(trailer, store.Trailer, file, store.Security is not null);
        }
        else
        {
            PdfXrefWriter.SetFileId(trailer, store.Trailer, writer.WrittenSpan, store.Security is not null);
        }

        // Offsets count from the header, as viewers read them.
        var relativeOffset = file.Length + writer.Length - store.OffsetBase;
        if (store.UsesXrefStreams)
        {
            rows[count] = new(size, XrefEntryType.InFile, relativeOffset, 0);
            trailer.Set(KnownName.Size, PdfValue.FromInteger(size + 1));
            PdfXrefWriter.WriteStream(ref writer, rows.AsSpan(0, count + 1), trailer, new(size, 0), relativeOffset);
            return;
        }

        PdfXrefWriter.WriteTable(ref writer, rows.AsSpan(0, count));
        PdfXrefWriter.WriteTrailer(ref writer, trailer, relativeOffset);
    }

    /// <summary>Writes the changed objects and fills their cross-reference rows, chaining the free entries of deleted ones.</summary>
    /// <param name="store">The document.</param>
    /// <param name="objects">The objects, ascending.</param>
    /// <param name="standalone">Whether the update stands alone, and so starts with the free list head.</param>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The rows, filled in.</param>
    /// <returns>The number of rows.</returns>
    private static int WriteObjects(PdfObjectStore store, PdfEditedObject[] objects, bool standalone, ref PdfObjectWriter writer, XrefRow[] rows)
    {
        var count = 0;
        var previousFree = -1;
        if (standalone || HasDeleted(objects))
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

        var repairs = new PdfSaveRepairs(store, false);
        foreach (var item in objects)
        {
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
    /// <returns>The objects, ascending.</returns>
    private static PdfEditedObject[] LiveObjects(PdfObjectStore store)
    {
        var objects = new List<PdfEditedObject>();
        for (var number = 1; number < store.Size; number++)
        {
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

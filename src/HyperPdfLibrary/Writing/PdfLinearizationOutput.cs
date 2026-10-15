// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>Writes one measured pass of a classic-xref linearized PDF.</summary>
internal static class PdfLinearizationOutput
{
    /// <summary>Writes one complete file using the previous pass's offsets.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="store">The input document.</param>
    /// <param name="plan">The numbered page groups.</param>
    /// <param name="previous">The preceding pass's positions.</param>
    /// <param name="fileId">The /ID array both trailers carry, or a null value to make one from the bytes before the first trailer.</param>
    /// <param name="current">Receives the positions written by this pass; it starts with every position zero.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    internal static void Write(
        ref PdfObjectWriter writer,
        PdfObjectStore store,
        PdfLinearizationPlan plan,
        PdfLinearizationLayout previous,
        PdfValue fileId,
        ref PdfLinearizationLayout current,
        CancellationToken cancellationToken)
    {
        writer.SetRenumbering(plan.Renumbering);
        PdfCompactWriter.WriteHeader(ref writer, store.Version, false);
        WriteHeader(ref writer, plan, previous, ref current);
        current.FirstXrefOffset = writer.Length;
        using var scratch = PdfTrailerScratch.Rent(store);
        var firstTrailer = scratch.Dictionary;
        WriteFirstXref(ref writer, store, plan, previous, fileId, firstTrailer);
        current.FileId = firstTrailer.GetRaw(KnownName.ID);
        WriteHints(ref writer, store, plan, previous, ref current);
        WriteGraphObject(ref writer, store, plan, plan.CatalogGraphNumber, ref current);
        WriteGraphGroup(ref writer, store, plan, plan.OpenDocument, ref current, cancellationToken);
        WritePageGroup(ref writer, store, plan, 0, ref current, cancellationToken);
        for (var page = 1; page < plan.PageCount; page++)
        {
            WritePageGroup(ref writer, store, plan, page, ref current, cancellationToken);
        }

        WriteGraphGroup(ref writer, store, plan, plan.SharedOther, ref current, cancellationToken);
        WriteGraphGroup(ref writer, store, plan, plan.Trailing, ref current, cancellationToken);
        if (!plan.OutlineWithFirstPage)
        {
            WriteGraphGroup(ref writer, store, plan, plan.Outline, ref current, cancellationToken);
        }

        WriteMainXref(ref writer, plan, previous, ref current, firstTrailer);
        current.FileLength = writer.Length;
    }

    /// <summary>Writes the first indirect object with the measured linearization parameters.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="plan">The page ordering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="current">Receives the header's position.</param>
    private static void WriteHeader(ref PdfObjectWriter writer, PdfLinearizationPlan plan, PdfLinearizationLayout previous, ref PdfLinearizationLayout current)
    {
        var number = plan.HeaderNumber;
        current.ObjectOffsets[number] = writer.Length;
        writer.WriteObjectHeader(new(number, 0));
        ref var output = ref writer.Buffer;
        output.Write("<</Linearized 1 /L "u8);
        PdfSyntax.WriteInteger(ref output, previous.FileLength);
        output.Write(" /H ["u8);
        PdfSyntax.WriteInteger(ref output, previous.HintOffset);
        output.WriteByte((byte)' ');
        PdfSyntax.WriteInteger(ref output, previous.HintLength);
        output.Write("] /O "u8);
        PdfSyntax.WriteInteger(ref output, plan.FirstPageNumber);
        output.Write(" /E "u8);
        PdfSyntax.WriteInteger(ref output, previous.PageEnds[0]);
        output.Write(" /N "u8);
        PdfSyntax.WriteInteger(ref output, plan.PageCount);
        output.Write(" /T "u8);
        PdfSyntax.WriteInteger(ref output, previous.MainXrefItemOffset);
        output.Write(">>\nendobj\n"u8);
        current.ObjectLengths[number] = writer.Length - current.ObjectOffsets[number];
    }

    /// <summary>Writes the first cross-reference section, which points to the catalog and first page.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="fileId">The /ID array, or a null value to make one from the bytes written so far.</param>
    /// <param name="trailer">The cleared first trailer, including its file identifier on return.</param>
    private static void WriteFirstXref(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, PdfLinearizationLayout previous, PdfValue fileId, PdfDictionary trailer)
    {
        var rowCount = plan.HintNumber - plan.CatalogNumber + 1;
        var rows = ArrayPool<XrefRow>.Shared.Rent(rowCount);
        try
        {
            for (var number = plan.CatalogNumber; number <= plan.HintNumber; number++)
            {
                rows[number - plan.CatalogNumber] = new(number, XrefEntryType.InFile, previous.ObjectOffsets[number], 0);
            }

            PdfXrefWriter.WriteTable(ref writer, rows.AsSpan(0, rowCount));
        }
        finally
        {
            ArrayPool<XrefRow>.Shared.Return(rows);
        }

        var encrypted = store.Security is not null;
        PdfXrefWriter.FillTrailer(trailer, store.Trailer, plan.HintNumber + 1, previous.MainXrefOffset, encrypted);
        if (fileId.IsNull)
        {
            PdfXrefWriter.SetFileId(trailer, store.Trailer, writer.WrittenSpan, encrypted);
        }
        else
        {
            trailer.Set(KnownName.ID, fileId);
        }

        PdfXrefWriter.WriteTrailer(ref writer, trailer, 0);
    }

    /// <summary>Writes the page and shared-object hint stream with the document's encryption.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="plan">The page ordering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="current">Receives the hint object's position and length.</param>
    private static void WriteHints(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, PdfLinearizationLayout previous, ref PdfLinearizationLayout current)
    {
        var bits = default(PdfHintBitWriter);
        using var scratch = PdfTrailerScratch.Rent(store);
        var dictionary = scratch.Dictionary;
        try
        {
            PdfLinearizationHintWriter.Build(plan, previous, ref bits, out var sharedOffset, out var outlineOffset);
            dictionary.Set(KnownName.S, PdfValue.FromInteger(sharedOffset));
            if (outlineOffset > 0)
            {
                dictionary.Set(KnownName.O, PdfValue.FromInteger(outlineOffset));
            }
            else
            {
                _ = dictionary.Remove(KnownName.O);
            }

            var number = plan.HintNumber;
            current.HintOffset = writer.Length;
            current.ObjectOffsets[number] = writer.Length;
            writer.WriteObjectHeader(new(number, 0));
            writer.WriteStream(dictionary, bits.WrittenSpan, new(number, 0), false);
            writer.WriteRaw("\nendobj\n"u8);
            current.HintLength = writer.Length - current.HintOffset;
            current.ObjectLengths[number] = current.HintLength;
        }
        finally
        {
            bits.Dispose();
        }
    }

    /// <summary>Writes the consecutive objects of one page group.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The page ordering.</param>
    /// <param name="page">The page index.</param>
    /// <param name="current">Receives the page group's range.</param>
    /// <param name="cancellationToken">Cancels the group.</param>
    private static void WritePageGroup(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, int page, ref PdfLinearizationLayout current, CancellationToken cancellationToken)
    {
        current.PageStarts[page] = writer.Length;
        WriteGraphGroup(ref writer, store, plan, plan.PageGroup(page), ref current, cancellationToken);
        current.PageEnds[page] = writer.Length;
    }

    /// <summary>Writes an ordered group of repaired objects.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="group">The graph numbers.</param>
    /// <param name="current">Receives object positions.</param>
    /// <param name="cancellationToken">Cancels the group.</param>
    private static void WriteGraphGroup(
        ref PdfObjectWriter writer,
        PdfObjectStore store,
        PdfLinearizationPlan plan,
        ReadOnlySpan<int> group,
        ref PdfLinearizationLayout current,
        CancellationToken cancellationToken)
    {
        foreach (var graphNumber in group)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteGraphObject(ref writer, store, plan, graphNumber, ref current);
        }
    }

    /// <summary>Writes one repaired object under its linearized number.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="graphNumber">The graph object to write.</param>
    /// <param name="current">Receives the object's position and length.</param>
    private static void WriteGraphObject(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, int graphNumber, ref PdfLinearizationLayout current)
    {
        var number = plan[graphNumber];
        current.ObjectOffsets[number] = writer.Length;
        PdfCompactWriter.WriteObject(ref writer, plan.Graph, graphNumber, number, PdfXrefWriter.EncryptNumber(store.Trailer));
        current.ObjectLengths[number] = writer.Length - current.ObjectOffsets[number];
    }

    /// <summary>Writes the main xref table, trailer and final pointer to the first xref section.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="current">Receives the xref positions.</param>
    /// <param name="firstTrailer">The first section's file identifier.</param>
    private static void WriteMainXref(
        ref PdfObjectWriter writer,
        PdfLinearizationPlan plan,
        PdfLinearizationLayout previous,
        ref PdfLinearizationLayout current,
        PdfDictionary firstTrailer)
    {
        var rows = ArrayPool<XrefRow>.Shared.Rent(plan.CatalogNumber);
        try
        {
            rows[0] = XrefRow.FreeHead;
            for (var number = 1; number < plan.CatalogNumber; number++)
            {
                rows[number] = new(number, XrefEntryType.InFile, previous.ObjectOffsets[number], 0);
            }

            current.MainXrefOffset = writer.Length;
            PdfXrefWriter.WriteTable(ref writer, rows.AsSpan(0, plan.CatalogNumber));
        }
        finally
        {
            ArrayPool<XrefRow>.Shared.Return(rows);
        }

        current.MainXrefItemOffset = FirstEntryOffset(writer.WrittenSpan, current.MainXrefOffset);
        _ = firstTrailer.Remove(KnownName.Prev);
        writer.WriteRaw("trailer\n"u8);
        writer.WriteValue(PdfValue.FromDictionary(firstTrailer));
        writer.WriteRaw("\nstartxref\n"u8);
        PdfSyntax.WriteInteger(ref writer.Buffer, previous.FirstXrefOffset);
        writer.WriteRaw("\n%%EOF\n"u8);
    }

    /// <summary>Finds the first object-zero entry after the xref subsection header.</summary>
    /// <param name="bytes">The written file.</param>
    /// <param name="sectionOffset">The xref section offset.</param>
    /// <returns>The first entry offset.</returns>
    private static long FirstEntryOffset(ReadOnlySpan<byte> bytes, long sectionOffset)
    {
        var section = bytes[(int)sectionOffset..];
        var headerEnd = section.IndexOf((byte)'\n') + 1;
        var runEnd = section[headerEnd..].IndexOf((byte)'\n') + 1;
        return sectionOffset + headerEnd + runEnd;
    }
}

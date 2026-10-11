// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The positions written by this pass.</returns>
    internal static PdfLinearizationLayout Write(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, PdfLinearizationLayout previous, CancellationToken cancellationToken)
    {
        var current = new PdfLinearizationLayout(plan.HintNumber, plan.PageGroups.Length);
        writer.SetRenumbering(plan.Renumbering);
        PdfCompactWriter.WriteHeader(ref writer, store.Version, false);
        WriteHeader(ref writer, store, plan, previous, current);
        current.FirstXrefOffset = writer.Length;
        var firstTrailer = WriteFirstXref(ref writer, store, plan, previous);
        WriteHints(ref writer, plan, previous, current);
        WriteGraphObject(ref writer, store, plan, plan.CatalogGraphNumber, current);
        WriteGraphGroup(ref writer, store, plan, plan.OpenDocument, current, cancellationToken);
        WritePageGroup(ref writer, store, plan, 0, current, cancellationToken);
        for (var page = 1; page < plan.PageGroups.Length; page++)
        {
            WritePageGroup(ref writer, store, plan, page, current, cancellationToken);
        }

        WriteGraphGroup(ref writer, store, plan, plan.SharedOther, current, cancellationToken);
        WriteGraphGroup(ref writer, store, plan, plan.Trailing, current, cancellationToken);
        if (!plan.OutlineWithFirstPage)
        {
            WriteGraphGroup(ref writer, store, plan, plan.Outline, current, cancellationToken);
        }

        WriteMainXref(ref writer, store, plan, previous, current, firstTrailer);
        current.FileLength = writer.Length;
        return current;
    }

    /// <summary>Writes the first indirect object with the measured linearization parameters.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The page ordering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="current">Receives the header's position.</param>
    private static void WriteHeader(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, PdfLinearizationLayout previous, PdfLinearizationLayout current)
    {
        var header = new PdfDictionary(null);
        header.Set(store.Names.Intern("Linearized"u8), PdfValue.FromInteger(1));
        header.Set(KnownName.L, PdfValue.FromInteger(previous.FileLength));
        header.Set(KnownName.H, PdfValue.FromArray(new(null, [PdfValue.FromInteger(previous.HintOffset), PdfValue.FromInteger(previous.HintLength)])));
        header.Set(KnownName.O, PdfValue.FromInteger(plan.FirstPageNumber));
        header.Set(KnownName.E, PdfValue.FromInteger(previous.PageEnds[0]));
        header.Set(KnownName.N, PdfValue.FromInteger(plan.PageGroups.Length));
        header.Set(KnownName.T, PdfValue.FromInteger(previous.MainXrefItemOffset));
        var number = plan.HeaderNumber;
        current.ObjectOffsets[number] = writer.Length;
        writer.WriteIndirectObject(new(number, 0), PdfValue.FromDictionary(header), false);
        current.ObjectLengths[number] = writer.Length - current.ObjectOffsets[number];
    }

    /// <summary>Writes the first cross-reference section, which points to the catalog and first page.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <returns>The first trailer, including its file identifier.</returns>
    private static PdfDictionary WriteFirstXref(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, PdfLinearizationLayout previous)
    {
        var rows = new XrefRow[plan.HintNumber - plan.CatalogNumber + 1];
        for (var number = plan.CatalogNumber; number <= plan.HintNumber; number++)
        {
            rows[number - plan.CatalogNumber] = new(number, XrefEntryType.InFile, previous.ObjectOffsets[number], 0);
        }

        PdfXrefWriter.WriteTable(ref writer, rows);
        var encrypted = store.Security is not null;
        var trailer = PdfXrefWriter.CreateTrailer(store.Trailer, plan.HintNumber + 1, previous.MainXrefOffset, encrypted);
        PdfXrefWriter.SetFileId(trailer, store.Trailer, writer.WrittenSpan, encrypted);
        PdfXrefWriter.WriteTrailer(ref writer, trailer, 0);
        return trailer;
    }

    /// <summary>Writes the page and shared-object hint stream with the document's encryption.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="plan">The page ordering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="current">Receives the hint object's position and length.</param>
    private static void WriteHints(ref PdfObjectWriter writer, PdfLinearizationPlan plan, PdfLinearizationLayout previous, PdfLinearizationLayout current)
    {
        var data = PdfLinearizationHintWriter.Build(plan, previous, out var sharedOffset, out var outlineOffset);
        var dictionary = new PdfDictionary(null);
        dictionary.Set(KnownName.S, PdfValue.FromInteger(sharedOffset));
        if (outlineOffset > 0)
        {
            dictionary.Set(KnownName.O, PdfValue.FromInteger(outlineOffset));
        }

        var number = plan.HintNumber;
        current.HintOffset = writer.Length;
        current.ObjectOffsets[number] = writer.Length;
        writer.WriteObjectHeader(new(number, 0));
        writer.WriteStream(dictionary, data, new(number, 0), false);
        writer.WriteRaw("\nendobj\n"u8);
        current.HintLength = writer.Length - current.HintOffset;
        current.ObjectLengths[number] = current.HintLength;
    }

    /// <summary>Writes the consecutive objects of one page group.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The page ordering.</param>
    /// <param name="page">The page index.</param>
    /// <param name="current">Receives the page group's range.</param>
    /// <param name="cancellationToken">Cancels the group.</param>
    private static void WritePageGroup(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, int page, PdfLinearizationLayout current, CancellationToken cancellationToken)
    {
        current.PageStarts[page] = writer.Length;
        WriteGraphGroup(ref writer, store, plan, plan.PageGroups[page], current, cancellationToken);
        current.PageEnds[page] = writer.Length;
    }

    /// <summary>Writes an ordered group of repaired objects.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="group">The graph numbers.</param>
    /// <param name="current">Receives object positions.</param>
    /// <param name="cancellationToken">Cancels the group.</param>
    private static void WriteGraphGroup(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, int[] group, PdfLinearizationLayout current, CancellationToken cancellationToken)
    {
        foreach (var graphNumber in group)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteGraphObject(ref writer, store, plan, graphNumber, current);
        }
    }

    /// <summary>Writes one repaired object under its linearized number.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="graphNumber">The graph object to write.</param>
    /// <param name="current">Receives the object's position and length.</param>
    private static void WriteGraphObject(ref PdfObjectWriter writer, PdfObjectStore store, PdfLinearizationPlan plan, int graphNumber, PdfLinearizationLayout current)
    {
        var number = plan[graphNumber];
        current.ObjectOffsets[number] = writer.Length;
        PdfCompactWriter.WriteObject(ref writer, plan.Graph, graphNumber, number, PdfXrefWriter.EncryptNumber(store.Trailer));
        current.ObjectLengths[number] = writer.Length - current.ObjectOffsets[number];
    }

    /// <summary>Writes the main xref table, trailer and final pointer to the first xref section.</summary>
    /// <param name="writer">The destination.</param>
    /// <param name="store">The original file.</param>
    /// <param name="plan">The object numbering.</param>
    /// <param name="previous">The preceding pass.</param>
    /// <param name="current">Receives the xref positions.</param>
    /// <param name="firstTrailer">The first section's file identifier.</param>
    private static void WriteMainXref(
        ref PdfObjectWriter writer,
        PdfObjectStore store,
        PdfLinearizationPlan plan,
        PdfLinearizationLayout previous,
        PdfLinearizationLayout current,
        PdfDictionary firstTrailer)
    {
        var rows = new XrefRow[plan.CatalogNumber];
        rows[0] = XrefRow.FreeHead;
        for (var number = 1; number < plan.CatalogNumber; number++)
        {
            rows[number] = new(number, XrefEntryType.InFile, previous.ObjectOffsets[number], 0);
        }

        current.MainXrefOffset = writer.Length;
        PdfXrefWriter.WriteTable(ref writer, rows);
        current.MainXrefItemOffset = FirstEntryOffset(writer.WrittenSpan, current.MainXrefOffset);
        var trailer = PdfXrefWriter.CreateTrailer(store.Trailer, plan.HintNumber + 1, -1, store.Security is not null);
        trailer.Set(KnownName.ID, firstTrailer.GetRaw(KnownName.ID));
        writer.WriteRaw("trailer\n"u8);
        writer.WriteValue(PdfValue.FromDictionary(trailer));
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

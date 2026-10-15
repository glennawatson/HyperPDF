// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>Writes cross-reference sections, as classic tables or compressed streams, and their trailers.</summary>
internal static class PdfXrefWriter
{
    /// <summary>The digits of a classic entry's offset.</summary>
    private const int OffsetDigits = 10;

    /// <summary>The digits of a classic entry's generation.</summary>
    private const int GenerationDigits = 5;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The number of fields in a cross-reference stream entry.</summary>
    private const int StreamFields = 3;

    /// <summary>The width of the type field.</summary>
    private const int TypeWidth = 1;

    /// <summary>Gets the binary comment written after the header so tools treat the file as binary.</summary>
    internal static ReadOnlySpan<byte> BinaryMarker => [0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A];

    /// <summary>Gets the trailer keys an update does not copy: the ones the writer sets itself and a cross-reference stream's own.</summary>
    private static ReadOnlySpan<int> StructuralKeys =>
    [
        (int)KnownName.Encrypt,
        (int)KnownName.Size,
        (int)KnownName.Filter,
        (int)KnownName.Index,
        (int)KnownName.Length,
        (int)KnownName.Prev,
        (int)KnownName.W,
        (int)KnownName.XRefStm,
        (int)KnownName.ID,
        (int)KnownName.DecodeParms,
        (int)KnownName.Type,
    ];

    /// <summary>Writes a classic table, grouping consecutive numbers into subsections.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The entries, in ascending number order.</param>
    internal static void WriteTable(ref PdfObjectWriter writer, ReadOnlySpan<XrefRow> rows)
    {
        ref var output = ref writer.Buffer;
        output.Write("xref\n"u8);
        var start = 0;
        while (start < rows.Length)
        {
            var end = RunEnd(rows, start);
            PdfSyntax.WriteInteger(ref output, rows[start].Number);
            output.WriteByte((byte)' ');
            PdfSyntax.WriteInteger(ref output, end - start);
            output.WriteByte((byte)'\n');
            for (var i = start; i < end; i++)
            {
                WriteTableEntry(ref output, rows[i]);
            }

            start = end;
        }
    }

    /// <summary>
    /// Writes a classic table, the trailer and the <c>startxref</c> pointer, and gives the revision its PDF MAC. The trailer is
    /// written again until the byte range in the <c>/AuthCode</c> matches the file.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The entries, in ascending number order.</param>
    /// <param name="trailer">The trailer dictionary, which holds the <c>/AuthCode</c> when the revision has a PDF MAC.</param>
    /// <param name="startXref">The offset of the table.</param>
    /// <param name="sealing">The PDF MAC of the revision.</param>
    internal static void WriteTable(ref PdfObjectWriter writer, ReadOnlySpan<XrefRow> rows, PdfDictionary trailer, long startXref, in PdfMacSealing sealing)
    {
        var start = writer.Length;
        do
        {
            writer.Buffer.Length = start;
            WriteTable(ref writer, rows);
            WriteTrailer(ref writer, trailer, startXref);
        }
        while (sealing.Seal is { } seal && !seal.TryComplete(ref writer, sealing.Prefix, start, sealing.CancellationToken));
    }

    /// <summary>Writes the trailer after a classic table, and the <c>startxref</c> pointer.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="trailer">The trailer dictionary.</param>
    /// <param name="startXref">The offset of the table.</param>
    internal static void WriteTrailer(ref PdfObjectWriter writer, PdfDictionary trailer, long startXref)
    {
        writer.WriteRaw("trailer\n"u8);
        writer.WriteValue(PdfValue.FromDictionary(trailer));
        writer.WriteRaw("\n"u8);
        WriteStartXref(ref writer, startXref);
    }

    /// <summary>Writes a Flate-compressed cross-reference stream holding the trailer, and the <c>startxref</c> pointer.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The entries in ascending number order, including the stream's own.</param>
    /// <param name="trailer">The trailer entries; /Type, /W and /Index are added.</param>
    /// <param name="id">The stream's object id.</param>
    /// <param name="offset">The stream's file offset.</param>
    internal static void WriteStream(ref PdfObjectWriter writer, ReadOnlySpan<XrefRow> rows, PdfDictionary trailer, PdfObjectId id, long offset)
    {
        Span<int> widths = stackalloc int[StreamFields];
        MeasureWidths(rows, widths);
        trailer.Set(KnownName.Type, PdfValue.FromName(KnownName.XRef));
        trailer.Set(KnownName.W, PdfValue.FromArray(new(null, [PdfValue.FromInteger(widths[0]), PdfValue.FromInteger(widths[1]), PdfValue.FromInteger(widths[StreamFields - 1])])));
        trailer.Set(KnownName.Index, PdfValue.FromArray(IndexArray(rows)));
        var raw = default(PooledBuffer);
        var compressed = default(PooledBuffer);
        try
        {
            foreach (var row in rows)
            {
                WriteStreamEntry(ref raw, row, widths);
            }

            FlateFilter.Encode(raw.WrittenSpan, ref compressed);
            writer.WriteObjectHeader(id);

            // Cross-reference streams are never encrypted.
            writer.WriteStream(trailer, compressed.WrittenSpan, default, true);
            writer.WriteRaw("\nendobj\n"u8);
        }
        finally
        {
            raw.Dispose();
            compressed.Dispose();
        }

        WriteStartXref(ref writer, offset);
    }

    /// <summary>
    /// Writes a Flate-compressed cross-reference stream holding the trailer and the <c>startxref</c> pointer, and gives the
    /// revision its PDF MAC. The stream is written again until the byte range in the <c>/AuthCode</c> matches the file.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="rows">The entries in ascending number order, including the stream's own.</param>
    /// <param name="trailer">The trailer entries; /Type, /W and /Index are added.</param>
    /// <param name="id">The stream's object id.</param>
    /// <param name="offset">The stream's file offset.</param>
    /// <param name="sealing">The PDF MAC of the revision.</param>
    internal static void WriteStream(ref PdfObjectWriter writer, ReadOnlySpan<XrefRow> rows, PdfDictionary trailer, PdfObjectId id, long offset, in PdfMacSealing sealing)
    {
        var start = writer.Length;
        do
        {
            writer.Buffer.Length = start;
            WriteStream(ref writer, rows, trailer, id, offset);
        }
        while (sealing.Seal is { } seal && !seal.TryComplete(ref writer, sealing.Prefix, start, sealing.CancellationToken));
    }

    /// <summary>Creates a trailer with /Size and /Prev and the /Root, /Info and /Encrypt of another trailer.</summary>
    /// <param name="source">The trailer to copy from.</param>
    /// <param name="size">The /Size.</param>
    /// <param name="previous">The /Prev offset, or a negative number for none.</param>
    /// <param name="keepEncrypt">Whether to keep /Encrypt.</param>
    /// <returns>The trailer.</returns>
    internal static PdfDictionary CreateTrailer(PdfDictionary source, int size, long previous, bool keepEncrypt)
    {
        var trailer = new PdfDictionary(null);
        FillTrailer(trailer, source, size, previous, keepEncrypt);
        return trailer;
    }

    /// <summary>Fills cleared trailer storage with the document's structural entries.</summary>
    /// <param name="trailer">The cleared destination.</param>
    /// <param name="source">The original trailer.</param>
    /// <param name="size">One more than the highest output object number.</param>
    /// <param name="previous">The previous section offset, or a negative number for none.</param>
    /// <param name="keepEncrypt">Whether the encryption dictionary is kept.</param>
    internal static void FillTrailer(PdfDictionary trailer, PdfDictionary source, int size, long previous, bool keepEncrypt)
    {
        trailer.Set(KnownName.Size, PdfValue.FromInteger(size));
        if (previous >= 0)
        {
            trailer.Set(KnownName.Prev, PdfValue.FromInteger(previous));
        }

        trailer.Set(KnownName.Root, source.GetRaw(KnownName.Root));
        trailer.Set(KnownName.Info, source.GetRaw(KnownName.Info));
        if (keepEncrypt)
        {
            trailer.Set(KnownName.Encrypt, source.GetRaw(KnownName.Encrypt));
        }
    }

    /// <summary>Copies the trailer keys that describe the document and that an update must not lose, such as custom entries.</summary>
    /// <param name="trailer">The new trailer.</param>
    /// <param name="source">The original trailer.</param>
    /// <remarks>
    /// Keys the writer sets itself, and the keys of a cross-reference stream's dictionary, are skipped.
    /// </remarks>
    internal static void CopyExtraKeys(PdfDictionary trailer, PdfDictionary source)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (!IsStructuralKey(key) && !trailer.ContainsKey(key))
            {
                trailer.Set(key, source.GetValueAt(i));
            }
        }
    }

    /// <summary>
    /// Sets /ID: the first part is kept, as encryption keys depend on it, and the second part is the MD5 of the bytes
    /// written, so it changes with every different save yet the same input gives the same file. An encrypted document
    /// without a first part gets no /ID, as its key was derived from an empty one.
    /// </summary>
    /// <param name="trailer">The new trailer.</param>
    /// <param name="source">The original trailer.</param>
    /// <param name="content">The bytes the second part is the hash of.</param>
    /// <param name="encrypted">Whether the output is encrypted.</param>
    internal static void SetFileId(PdfDictionary trailer, PdfDictionary source, ReadOnlySpan<byte> content, bool encrypted)
    {
        var hash = new byte[Md5.HashLength];
        _ = Md5.HashData(content, hash);
        SetFileIdHash(trailer, source, hash, encrypted);
    }

    /// <summary>Sets /ID with the second part the MD5 of a whole file, read a chunk at a time.</summary>
    /// <param name="trailer">The new trailer.</param>
    /// <param name="source">The original trailer.</param>
    /// <param name="content">The file the second part is the hash of.</param>
    /// <param name="encrypted">Whether the output is encrypted.</param>
    internal static void SetFileId(PdfDictionary trailer, PdfDictionary source, PdfByteSource content, bool encrypted)
    {
        var hash = new byte[Md5.HashLength];
        _ = Md5.HashData(content, hash);
        SetFileIdHash(trailer, source, hash, encrypted);
    }

    /// <summary>Gets the number of the /Encrypt dictionary when it is an indirect object.</summary>
    /// <param name="trailer">The trailer.</param>
    /// <returns>The object number, or zero.</returns>
    internal static int EncryptNumber(PdfDictionary trailer)
    {
        var encrypt = trailer.GetRaw(KnownName.Encrypt);
        return encrypt.IsReference ? encrypt.AsReference().Number : 0;
    }

    /// <summary>Determines whether a file ends with a line end, so an update can follow it directly.</summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> when the last byte is CR or LF.</returns>
    internal static bool EndsWithLineEnd(ReadOnlySpan<byte> file) => !file.IsEmpty && file[^1] is (byte)'\n' or (byte)'\r';

    /// <summary>Determines whether a trailer key is one the writer sets itself or belongs to a cross-reference stream.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the key is not copied.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsStructuralKey(PdfName key) => StructuralKeys.Contains(key.Id);

    /// <summary>Sets /ID from a hash, keeping the original first part.</summary>
    /// <param name="trailer">The new trailer.</param>
    /// <param name="source">The original trailer.</param>
    /// <param name="hash">The second part.</param>
    /// <param name="encrypted">Whether the output is encrypted.</param>
    private static void SetFileIdHash(PdfDictionary trailer, PdfDictionary source, byte[] hash, bool encrypted)
    {
        var first = source.GetArray(KnownName.ID)?.Get(0) ?? default;
        var hasFirst = first.Kind == PdfKind.String;
        if (encrypted && !hasFirst)
        {
            return;
        }

        var second = PdfValue.FromString(hash);
        trailer.Set(KnownName.ID, PdfValue.FromArray(new(null, [hasFirst ? first : second, second])));
    }

    /// <summary>Writes <c>startxref</c>, the offset and <c>%%EOF</c>.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="offset">The offset of the cross-reference section.</param>
    private static void WriteStartXref(ref PdfObjectWriter writer, long offset)
    {
        writer.WriteRaw("startxref\n"u8);
        PdfSyntax.WriteInteger(ref writer.Buffer, offset);
        writer.WriteRaw("\n%%EOF\n"u8);
    }

    /// <summary>Finds the end of a run of consecutive object numbers.</summary>
    /// <param name="rows">The entries.</param>
    /// <param name="start">The first entry of the run.</param>
    /// <returns>The index after the run.</returns>
    private static int RunEnd(ReadOnlySpan<XrefRow> rows, int start)
    {
        var end = start + 1;
        while (end < rows.Length && rows[end].Number == rows[end - 1].Number + 1)
        {
            end++;
        }

        return end;
    }

    /// <summary>Writes one 20-byte classic entry.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="row">The entry.</param>
    private static void WriteTableEntry(ref PooledBuffer output, XrefRow row)
    {
        PdfSyntax.WritePadded(ref output, row.Location, OffsetDigits);
        output.WriteByte((byte)' ');
        PdfSyntax.WritePadded(ref output, row.Detail, GenerationDigits);
        output.Write(row.Type == XrefEntryType.Free ? " f\r\n"u8 : " n\r\n"u8);
    }

    /// <summary>Works out the narrowest field widths that hold every entry.</summary>
    /// <param name="rows">The entries.</param>
    /// <param name="widths">The three widths.</param>
    private static void MeasureWidths(ReadOnlySpan<XrefRow> rows, Span<int> widths)
    {
        long location = 0;
        long detail = 0;
        foreach (var row in rows)
        {
            location = Math.Max(location, row.Location);
            detail = Math.Max(detail, row.Detail);
        }

        widths[0] = TypeWidth;
        widths[1] = BytesFor(location);
        widths[StreamFields - 1] = BytesFor(detail);
    }

    /// <summary>Gets the bytes a non-negative number needs, at least one.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The byte count.</returns>
    private static int BytesFor(long value)
    {
        var bytes = 1;
        while ((value >>= ByteBits) != 0)
        {
            bytes++;
        }

        return bytes;
    }

    /// <summary>Builds the /Index array of first number and count pairs.</summary>
    /// <param name="rows">The entries.</param>
    /// <returns>The array.</returns>
    private static PdfArray IndexArray(ReadOnlySpan<XrefRow> rows)
    {
        var index = new PdfArray(null);
        var start = 0;
        while (start < rows.Length)
        {
            var end = RunEnd(rows, start);
            index.Add(PdfValue.FromInteger(rows[start].Number));
            index.Add(PdfValue.FromInteger(end - start));
            start = end;
        }

        return index;
    }

    /// <summary>Writes one binary cross-reference stream entry, big-endian.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="row">The entry.</param>
    /// <param name="widths">The field widths.</param>
    private static void WriteStreamEntry(ref PooledBuffer output, XrefRow row, scoped ReadOnlySpan<int> widths)
    {
        var span = output.GetSpan(widths[0] + widths[1] + widths[StreamFields - 1]);
        span[0] = (byte)row.Type;
        WriteBigEndian(span.Slice(widths[0], widths[1]), row.Location);
        WriteBigEndian(span.Slice(widths[0] + widths[1], widths[StreamFields - 1]), row.Detail);
        output.Advance(widths[0] + widths[1] + widths[StreamFields - 1]);
    }

    /// <summary>Writes a number big-endian into a field.</summary>
    /// <param name="field">The field.</param>
    /// <param name="value">The number.</param>
    private static void WriteBigEndian(Span<byte> field, long value)
    {
        for (var i = field.Length - 1; i >= 0; i--)
        {
            field[i] = (byte)value;
            value >>= ByteBits;
        }
    }
}

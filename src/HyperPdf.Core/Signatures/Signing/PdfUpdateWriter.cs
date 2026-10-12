// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Writes incremental updates: new or redefined objects appended after the original bytes, which stay untouched.</summary>
internal static class PdfUpdateWriter
{
    /// <summary>The bytes of a cross-reference stream row's offset field.</summary>
    private const int OffsetBytes = 5;

    /// <summary>The bytes of a cross-reference stream row's index field.</summary>
    private const int IndexBytes = 3;

    /// <summary>The bytes of a cross-reference stream row: type, offset and index.</summary>
    private const int RowLength = 1 + OffsetBytes + IndexBytes;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Writes the objects, a cross-reference table and a trailer pointing back at the previous one.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="objects">The objects by number.</param>
    /// <param name="root">The catalog's number.</param>
    /// <param name="size">The new object count.</param>
    /// <returns>The update's bytes.</returns>
    internal static byte[] Serialize(PdfStructure structure, SortedDictionary<int, string> objects, int root, int size)
    {
        var start = structure.File.Length;
        var update = new StringBuilder("\n");
        var offsets = new SortedDictionary<int, int>();
        foreach (var (number, body) in objects)
        {
            offsets[number] = start + update.Length;
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n");
        }

        if (structure.Repaired)
        {
            AppendFullCrossReferenceStream(structure, offsets, root, size, start, update);
            return Encoding.Latin1.GetBytes(update.ToString());
        }

        var xref = start + update.Length;
        _ = update.Append("xref\n0 1\n0000000000 65535 f \n");
        foreach (var (number, offset) in offsets)
        {
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 1\n{offset:D10} 00000 n \n");
        }

        _ = update.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {size} /Root {root} 0 R /Prev {structure.StartXref}");
        CopyTrailerEntry(structure.Trailer, "Info"u8, update);
        CopyTrailerEntry(structure.Trailer, "ID"u8, update);
        _ = update.Append(CultureInfo.InvariantCulture, $" >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(update.ToString());
    }

    /// <summary>
    /// Ends the update of a repaired file with a cross-reference stream listing every object, old and new, and no
    /// <c>/Prev</c>, so readers never fall back to the damaged sections.
    /// </summary>
    /// <param name="structure">The repaired structure.</param>
    /// <param name="offsets">The new objects' offsets.</param>
    /// <param name="root">The catalog's number.</param>
    /// <param name="size">The object count before the stream.</param>
    /// <param name="start">The update's offset in the file.</param>
    /// <param name="update">The update being written.</param>
    private static void AppendFullCrossReferenceStream(PdfStructure structure, SortedDictionary<int, int> offsets, int root, int size, int start, StringBuilder update)
    {
        var stream = size;
        var xref = start + update.Length;
        var rows = new byte[(size + 1) * RowLength];
        for (var number = 0; number <= size; number++)
        {
            var row = rows.AsSpan(number * RowLength, RowLength);
            if (number == stream)
            {
                WriteRow(row, XrefEntry.InUse, xref, 0);
            }
            else if (offsets.TryGetValue(number, out var offset))
            {
                WriteRow(row, XrefEntry.InUse, offset, 0);
            }
            else if (structure.Entries.TryGetValue(number, out var entry) && entry.Type is XrefEntry.InUse or XrefEntry.Compressed)
            {
                WriteRow(row, entry.Type, entry.Location, entry.Index);
            }
        }

        _ = update.Append(CultureInfo.InvariantCulture, $"{stream} 0 obj\n<< /Type /XRef /Size {size + 1} /W [1 {OffsetBytes} {IndexBytes}] /Root {root} 0 R /Length {rows.Length}");
        CopyTrailerEntry(structure.Trailer, "Info"u8, update);
        CopyTrailerEntry(structure.Trailer, "ID"u8, update);
        _ = update.Append(" >>\nstream\n").Append(Encoding.Latin1.GetString(rows)).Append(CultureInfo.InvariantCulture, $"\nendstream\nendobj\nstartxref\n{xref}\n%%EOF\n");
    }

    /// <summary>Writes one cross-reference stream row, big-endian.</summary>
    /// <param name="row">The row's bytes.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="location">The offset, or the object stream's number.</param>
    /// <param name="index">The index within the object stream.</param>
    private static void WriteRow(Span<byte> row, int type, long location, int index)
    {
        row[0] = (byte)type;
        for (var i = 0; i < OffsetBytes; i++)
        {
            row[OffsetBytes - i] = (byte)(location >> (ByteBits * i));
        }

        for (var i = 0; i < IndexBytes; i++)
        {
            row[RowLength - 1 - i] = (byte)(index >> (ByteBits * i));
        }
    }

    /// <summary>Copies a trailer entry, such as the document id, into the new trailer.</summary>
    /// <param name="trailer">The previous trailer.</param>
    /// <param name="key">The key.</param>
    /// <param name="update">The update being written.</param>
    private static void CopyTrailerEntry(byte[] trailer, ReadOnlySpan<byte> key, StringBuilder update)
    {
        var at = PdfSyntax.FindKey(trailer, 0, key);
        if (at < 0)
        {
            return;
        }

        _ = update.Append(CultureInfo.InvariantCulture, $" /{Encoding.ASCII.GetString(key)} {Encoding.Latin1.GetString(trailer.AsSpan(at, PdfSyntax.ValueEnd(trailer, at) - at))}");
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Structure;

/// <summary>
/// Reads a file's cross-reference sections: classic tables, cross-reference streams and hybrids, following /Prev and
/// /XRefStm from the newest section back. Classic entries in the standard 20-byte layout are read by position without
/// tokenising.
/// </summary>
internal static class XrefReader
{
    /// <summary>The length of a standard classic entry.</summary>
    private const int EntryLength = 20;

    /// <summary>The shortest a classic entry can be: digits, generation, type and a one-byte line end.</summary>
    private const int MinEntryLength = 18;

    /// <summary>The length of the line end that ends a standard classic entry.</summary>
    private const int EntryLineEnd = 2;

    /// <summary>The digits of a classic entry's offset.</summary>
    private const int OffsetDigits = 10;

    /// <summary>The position of a classic entry's generation.</summary>
    private const int GenerationStart = 11;

    /// <summary>The digits of a classic entry's generation.</summary>
    private const int GenerationDigits = 5;

    /// <summary>The position of a classic entry's type letter.</summary>
    private const int TypePosition = 17;

    /// <summary>The number of fields in a cross-reference stream entry.</summary>
    private const int StreamFields = 3;

    /// <summary>The index of the third field.</summary>
    private const int ThirdField = 2;

    /// <summary>The numbers per /Index subsection: first object and count.</summary>
    private const int IndexPair = 2;

    /// <summary>The widest field read from a cross-reference stream.</summary>
    private const int MaxFieldWidth = 8;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The base of decimal digits.</summary>
    private const int Ten = 10;

    /// <summary>The mask for checking cancellation every 256 xref stream entries.</summary>
    private const int StreamCancellationMask = 0xFF;

    /// <summary>The bytes read after <c>startxref</c> or at a section's start to find its first token.</summary>
    private const int TokenWindow = 1024;

    /// <summary>Reads the cross-reference sections.</summary>
    /// <param name="file">The file.</param>
    /// <param name="store">The objects being read, which parse the trailers and streams.</param>
    /// <param name="table">The table, filled newest first.</param>
    /// <returns><see langword="true"/> when the sections were read and a trailer with /Root was found.</returns>
    internal static bool TryRead(PdfByteSource file, PdfObjectStore store, XrefTable table)
    {
        if (!TryReadStartXref(file, out var start))
        {
            return false;
        }

        table.StartXref = start;
        table.OffsetBase = store.HeaderOffset;
        var walk = default(XrefWalk);
        try
        {
            walk.Push(start);
            while (walk.TryPop(out var offset) && walk.Visited < PdfLimits.MaxXrefSections)
            {
                PdfOpenContext.ThrowIfCancelled(store.Context);
                if (!walk.TryVisit(offset) || offset < 0 || offset >= file.Length)
                {
                    continue;
                }

                var trailer = ReadSectionAt(file, offset, store, table, walk.Visited == 1, out var isStream);
                if (trailer is null)
                {
                    return false;
                }

                table.UsesXrefStreams |= isStream && walk.Visited == 1;
                CommitFreeEntries(table, trailer);
                MergeTrailer(table, trailer);
                QueueLinked(ref walk, trailer, store);
            }
        }
        finally
        {
            walk.Release();
        }

        return table.Trailer?.ContainsKey(KnownName.Root) == true;
    }

    /// <summary>
    /// Makes a section's free entries final, so they hide the object in older sections. A hybrid file's classic table frees
    /// the objects its stream holds, so its free entries are dropped.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="trailer">The section's trailer.</param>
    private static void CommitFreeEntries(XrefTable table, PdfDictionary trailer)
    {
        if (trailer.ContainsKey(KnownName.XRefStm))
        {
            table.DiscardFree();
            return;
        }

        table.CommitFree();
    }

    /// <summary>Reads the offset after the last <c>startxref</c>, searching back from the end of the file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="start">The offset of the newest section.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryReadStartXref(PdfByteSource file, out long start)
    {
        start = -1;
        var marker = PdfByteSearch.LastIndexOf(file, PdfKeywords.StartXref, file.Length);
        if (marker < 0)
        {
            return false;
        }

        var after = marker + PdfKeywords.StartXref.Length;
        using var window = file.Lease(after, (int)Math.Min(TokenWindow, file.Length - after));
        var lexer = new PdfLexer(window.Span);
        if (lexer.Next() != PdfTokenKind.Number || !PdfNumber.TryParse(lexer.Lexeme, out var value))
        {
            return false;
        }

        start = value.AsInteger();
        return true;
    }

    /// <summary>Keeps the newest trailer, filling keys it lacks from older ones.</summary>
    /// <param name="table">The table.</param>
    /// <param name="trailer">A section's trailer.</param>
    private static void MergeTrailer(XrefTable table, PdfDictionary trailer)
    {
        if (table.Trailer is null)
        {
            // The section's trailer was parsed for this table alone, so the table keeps it and adds to it.
            table.Trailer = trailer;
            return;
        }

        for (var i = 0; i < trailer.Count; i++)
        {
            var key = trailer.GetKeyAt(i);
            if (!table.Trailer.ContainsKey(key) && !key.Is(KnownName.Prev) && !key.Is(KnownName.XRefStm))
            {
                table.Trailer.Add(key, trailer.GetValueAt(i));
            }
        }
    }

    /// <summary>Queues the sections a trailer links to: /Prev, and /XRefStm unless cross-reference streams are ignored.</summary>
    /// <param name="pending">The queue.</param>
    /// <param name="trailer">The section's trailer.</param>
    /// <param name="store">The objects being read.</param>
    private static void QueueLinked(ref XrefWalk pending, PdfDictionary trailer, PdfObjectStore store)
    {
        PushOffset(ref pending, trailer.Get(KnownName.Prev));
        PushOffset(ref pending, store.Context is { IgnoreXrefStreams: true } ? default : trailer.Get(KnownName.XRefStm));
    }

    /// <summary>Queues a section offset; pushed last means read first, so /XRefStm is read before /Prev.</summary>
    /// <param name="pending">The queue.</param>
    /// <param name="value">The offset value.</param>
    private static void PushOffset(ref XrefWalk pending, PdfValue value)
    {
        if (value.Kind == PdfKind.Integer)
        {
            pending.Push(value.AsInteger());
        }
    }

    /// <summary>
    /// Reads one section at an offset relative to the header, falling back to the offset as an absolute position. The
    /// first section decides which of the two the whole file uses.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The section's offset as written in the file.</param>
    /// <param name="store">The objects being read.</param>
    /// <param name="table">The table.</param>
    /// <param name="first">Whether this is the newest section.</param>
    /// <param name="isStream">Whether the section is a cross-reference stream.</param>
    /// <returns>The section's trailer, or <see langword="null"/> when there is no section at either position.</returns>
    private static PdfDictionary? ReadSectionAt(PdfByteSource file, long offset, PdfObjectStore store, XrefTable table, bool first, out bool isStream)
    {
        isStream = false;
        var shifted = offset + table.OffsetBase;
        if (table.OffsetBase != 0 && shifted < file.Length)
        {
            var trailer = ReadSection(file, shifted, store, table, out isStream);
            if (trailer is not null)
            {
                return trailer;
            }

            table.DiscardFree();
            table.OffsetBase = first ? 0 : table.OffsetBase;
        }

        return ReadSection(file, offset, store, table, out isStream);
    }

    /// <summary>Reads one section.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The section's offset.</param>
    /// <param name="store">The objects being read.</param>
    /// <param name="table">The table.</param>
    /// <param name="isStream">Whether the section is a cross-reference stream.</param>
    /// <returns>The section's trailer, or <see langword="null"/> when there is no section at the offset.</returns>
    private static PdfDictionary? ReadSection(PdfByteSource file, long offset, PdfObjectStore store, XrefTable table, out bool isStream)
    {
        PdfTokenKind kind;
        bool isTable;
        long tableStart;
        using (var window = file.Lease(offset, (int)Math.Min(TokenWindow, file.Length - offset)))
        {
            var lexer = new PdfLexer(window.Span);
            kind = lexer.Next();
            isTable = kind == PdfTokenKind.Keyword && lexer.Keyword == PdfKeyword.Xref;
            tableStart = offset + lexer.Position;
        }

        isStream = kind == PdfTokenKind.Number;
        if (isTable)
        {
            return ReadTable(file, tableStart, store, table);
        }

        return isStream && store.Context is not { IgnoreXrefStreams: true } ? ReadStream(offset, store, table) : null;
    }

    /// <summary>
    /// Reads a classic table and its trailer. The entries up to the <c>trailer</c> keyword are read from one window; the
    /// trailer is parsed from its own.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="position">The offset after the <c>xref</c> keyword.</param>
    /// <param name="store">The objects being read.</param>
    /// <param name="table">The table.</param>
    /// <returns>The trailer, or <see langword="null"/> when damaged.</returns>
    private static PdfDictionary? ReadTable(PdfByteSource file, long position, PdfObjectStore store, XrefTable table)
    {
        var limit = Math.Min(file.Length, position + PdfLimits.MaxXrefWindow);
        var trailer = PdfByteSearch.IndexOf(file, PdfKeywords.Trailer, position, limit);
        using var window = file.Lease(position, (int)((trailer < 0 ? limit : trailer) - position));
        return ReadEntries(window.Span, table) && trailer >= 0 ? StoreParsing.ParseValueAt(store, trailer + PdfKeywords.Trailer.Length).AsDictionary() : null;
    }

    /// <summary>Reads the subsections of a classic table.</summary>
    /// <param name="entries">The bytes from after <c>xref</c> up to <c>trailer</c>.</param>
    /// <param name="table">The table.</param>
    /// <returns><see langword="true"/> when every subsection was read.</returns>
    private static bool ReadEntries(ReadOnlySpan<byte> entries, XrefTable table)
    {
        var lexer = new PdfLexer(entries);
        for (var kind = lexer.Next(); kind != PdfTokenKind.EndOfData; kind = lexer.Next())
        {
            if (kind != PdfTokenKind.Number
        || !PdfNumber.TryParse(
        lexer.Lexeme,
        out var startValue) || lexer.Next() != PdfTokenKind.Number
        || !PdfNumber.TryParse(
        lexer.Lexeme,
        out var countValue) || !ReadSubsection(
        ref lexer,
        startValue.AsInt32(),
        countValue.AsInt32(),
        table))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the entries of one subsection.</summary>
    /// <param name="lexer">The lexer, after the subsection header.</param>
    /// <param name="start">The first object number.</param>
    /// <param name="count">The number of entries.</param>
    /// <param name="table">The table.</param>
    /// <returns><see langword="true"/> when the entries were read.</returns>
    private static bool ReadSubsection(ref PdfLexer lexer, int start, int count, XrefTable table)
    {
        // The entries must fit in the rest of the file before the table grows to hold them.
        if (start < 0 || count < 0 || (long)start + count > PdfLimits.MaxObjectNumber || (long)count * MinEntryLength > lexer.Data.Length - lexer.Position)
        {
            return false;
        }

        table.EnsureSize(start + count);
        for (var i = 0; i < count; i++)
        {
            lexer.SkipWhitespace();
            if (TryReadFixedEntry(lexer.Data, lexer.Position, out var entry))
            {
                lexer.Position += EntryLength - EntryLineEnd;
            }
            else if (!TryReadEntryTokens(ref lexer, out entry))
            {
                return false;
            }

            if (!entry.InUse)
            {
                table.NoteFree(start + i, entry.Generation);
            }
            else if (entry.Offset > 0)
            {
                table.SetIfMissing(start + i, XrefEntryType.InFile, entry.Offset, entry.Generation);
            }
        }

        return true;
    }

    /// <summary>Reads an entry in the standard "oooooooooo ggggg n" layout by position.</summary>
    /// <param name="data">The file.</param>
    /// <param name="position">The entry's offset.</param>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> when the entry has the standard layout.</returns>
    private static bool TryReadFixedEntry(ReadOnlySpan<byte> data, int position, out ClassicEntry entry)
    {
        entry = default;
        if (position + TypePosition >= data.Length || data[position + OffsetDigits] != ' ' || data[position + TypePosition - 1] != ' ')
        {
            return false;
        }

        var type = data[position + TypePosition];
        if ((type != 'n'
        && type != 'f')
        || !TryParseDigits(
        data.Slice(
        position,
        OffsetDigits),
        out var offset) || !TryParseDigits(
        data.Slice(
        position + GenerationStart,
        GenerationDigits),
        out var generation))
        {
            return false;
        }

        entry = new(offset, (int)generation, type == 'n');
        return true;
    }

    /// <summary>Reads an entry token by token, for tables that do not keep the standard layout.</summary>
    /// <param name="lexer">The lexer.</param>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> when an entry was read.</returns>
    private static bool TryReadEntryTokens(ref PdfLexer lexer, out ClassicEntry entry)
    {
        entry = default;
        if (lexer.Next() != PdfTokenKind.Number
        || !PdfNumber.TryParse(
        lexer.Lexeme,
        out var offset) || lexer.Next() != PdfTokenKind.Number
        || !PdfNumber.TryParse(
        lexer.Lexeme,
        out var generation) || lexer.Next() != PdfTokenKind.Keyword)
        {
            return false;
        }

        entry = new(offset.AsInteger(), generation.AsInt32(), lexer.Lexeme.Length == 1 && lexer.Lexeme[0] == 'n');
        return true;
    }

    /// <summary>Parses a run of decimal digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when every byte is a digit.</returns>
    private static bool TryParseDigits(ReadOnlySpan<byte> digits, out long value)
    {
        value = 0;
        foreach (var c in digits)
        {
            var digit = (uint)(c - '0');
            if (digit >= Ten)
            {
                return false;
            }

            value = (value * Ten) + digit;
        }

        return true;
    }

    /// <summary>Reads a cross-reference stream.</summary>
    /// <param name="offset">The stream object's offset.</param>
    /// <param name="store">The objects being read.</param>
    /// <param name="table">The table.</param>
    /// <returns>The stream dictionary, which is the section's trailer, or <see langword="null"/> when damaged.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static PdfDictionary? ReadStream(long offset, PdfObjectStore store, XrefTable table)
    {
        if (!StoreParsing.TryParseObjectAt(store, offset, out _, out var value) || value.AsStream() is not { } stream)
        {
            return null;
        }

        var dictionary = stream.Dictionary;
        Span<int> widths = stackalloc int[StreamFields];
        ReadWidths(dictionary.GetArray(KnownName.W), widths);
        var size = dictionary.Get(KnownName.Size).AsInteger();
        var index = dictionary.GetArray(KnownName.Index);
        ValidateStreamHeader(size, index);

        var output = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref output);
            ReadStreamSubsections(output.WrittenSpan, widths, size, index, table, store.Context is not { Recovery: false }, store.Context?.CancellationToken ?? CancellationToken.None);

            return dictionary;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Validates the xref stream's total object slots and subsection shape.</summary>
    /// <param name="size">The number of object slots.</param>
    /// <param name="index">The optional subsection array.</param>
    /// <exception cref="PdfException">The header exceeds the parser limits.</exception>
    private static void ValidateStreamHeader(long size, PdfArray? index)
    {
        if (size is < 0 or > PdfLimits.MaxObjectNumber || (index is not null && index.Count % IndexPair != 0))
        {
            throw new PdfException(PdfError.Format, "The xref stream size or index is invalid.");
        }
    }

    /// <summary>Reads each subsection after bounding its count and object number.</summary>
    /// <param name="data">The decoded stream bytes.</param>
    /// <param name="widths">The field widths.</param>
    /// <param name="size">The number of object slots.</param>
    /// <param name="index">The optional subsection array.</param>
    /// <param name="table">The cross-reference table.</param>
    /// <param name="recovery">Whether invalid object ranges may be skipped.</param>
    /// <param name="cancellationToken">Cancels entry parsing.</param>
    /// <exception cref="PdfException">A subsection extends beyond the object slots.</exception>
    private static void ReadStreamSubsections(ReadOnlySpan<byte> data, ReadOnlySpan<int> widths, long size, PdfArray? index, XrefTable table, bool recovery, CancellationToken cancellationToken)
    {
        var position = 0;
        var subsections = index is null ? 1 : index.Count / IndexPair;
        for (var s = 0; s < subsections; s++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            position = ReadStreamEntries(data, position, widths, ReadStreamSubsection(index, s, size, recovery), table, cancellationToken);
        }
    }

    /// <summary>Reads one validated object range from the subsection array.</summary>
    /// <param name="index">The optional subsection array.</param>
    /// <param name="section">The subsection index.</param>
    /// <param name="size">The number of object slots.</param>
    /// <param name="recovery">Whether invalid object ranges may be skipped.</param>
    /// <returns>The bounded object range.</returns>
    /// <exception cref="PdfException">The subsection extends beyond the object slots.</exception>
    private static Subsection ReadStreamSubsection(PdfArray? index, int section, long size, bool recovery)
    {
        var first = index?.Get(section * IndexPair).AsInteger() ?? 0;
        var count = index?.Get((section * IndexPair) + 1).AsInteger() ?? size;
        ValidateStreamRange(first, count);

        if (first > size || count > size - first)
        {
            if (!recovery)
            {
                throw new PdfException(PdfError.Format, "The xref stream index range exceeds its size.");
            }

            return new(0, (int)count, true);
        }

        return new((int)first, (int)count, false);
    }

    /// <summary>Rejects negative ranges and counts beyond the parser's object limit.</summary>
    /// <param name="first">The first object number.</param>
    /// <param name="count">The number of entries.</param>
    /// <exception cref="PdfException">The range cannot be consumed safely.</exception>
    private static void ValidateStreamRange(long first, long count)
    {
        if (first < 0 || count < 0 || count > PdfLimits.MaxObjectNumber)
        {
            throw new PdfException(PdfError.Format, "The xref stream index range exceeds its size.");
        }
    }

    /// <summary>Reads the /W field widths after validating their encoded range.</summary>
    /// <param name="array">The /W array.</param>
    /// <param name="widths">The three widths.</param>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static void ReadWidths(PdfArray? array, Span<int> widths)
    {
        if (array is null || array.Count != StreamFields)
        {
            throw new PdfException(PdfError.Format, "The xref stream must contain three field widths.");
        }

        for (var i = 0; i < StreamFields; i++)
        {
            var width = array.Get(i).AsInteger();
            if (width is < 0 or > MaxFieldWidth)
            {
                throw new PdfException(PdfError.Format, "The xref stream field width is invalid.");
            }

            widths[i] = (int)width;
        }
    }

    /// <summary>Reads the entries of one cross-reference stream subsection.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <param name="position">The offset of the first entry.</param>
    /// <param name="widths">The field widths.</param>
    /// <param name="subsection">The first object number and entry count.</param>
    /// <param name="table">The table.</param>
    /// <param name="cancellationToken">Cancels entry parsing.</param>
    /// <returns>The offset after the entries.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static int ReadStreamEntries(ReadOnlySpan<byte> data, int position, ReadOnlySpan<int> widths, Subsection subsection, XrefTable table, CancellationToken cancellationToken)
    {
        var entryLength = widths[0] + widths[1] + widths[ThirdField];
        var count = StreamEntryCount(data, position, entryLength, subsection);

        if (subsection.Skip)
        {
            // Consume the skipped range's bytes so later valid subsections keep their entry alignment.
            return position + (count * entryLength);
        }

        table.EnsureSize(subsection.First + count);
        for (var i = 0; i < count; i++)
        {
            if ((i & StreamCancellationMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var entry = data.Slice(position, entryLength);
            position += entryLength;
            var type = widths[0] == 0 ? 1 : ReadField(entry[..widths[0]]);
            var location = ReadField(entry.Slice(widths[0], widths[1]));
            var detail = ReadField(entry.Slice(widths[0] + widths[1], widths[ThirdField]));
            CommitStreamEntry(table, subsection.First + i, type, location, detail);
        }

        return position;
    }

    /// <summary>Checks that announced entries fit before growing the object table.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <param name="position">The subsection byte offset.</param>
    /// <param name="entryLength">The bytes per entry.</param>
    /// <param name="subsection">The object range.</param>
    /// <returns>The number of entries.</returns>
    /// <exception cref="PdfException">The entries exceed the data or the object limit.</exception>
    private static int StreamEntryCount(ReadOnlySpan<byte> data, int position, int entryLength, Subsection subsection)
    {
        if (entryLength == 0 || subsection.First < 0 || subsection.Count < 0
            || subsection.Count > (data.Length - position) / entryLength
            || (long)subsection.First + subsection.Count > PdfLimits.MaxObjectNumber)
        {
            throw new PdfException(PdfError.Format, "The xref stream entries exceed its data or object limit.");
        }

        return subsection.Count;
    }

    /// <summary>Stores an entry after validating its unsigned fields against their signed destinations.</summary>
    /// <param name="table">The cross-reference table.</param>
    /// <param name="number">The object number.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="location">The file offset or object stream number.</param>
    /// <param name="detail">The generation or object stream index.</param>
    /// <exception cref="PdfException">A field exceeds its signed destination.</exception>
    private static void CommitStreamEntry(XrefTable table, int number, long type, long location, long detail)
    {
        if (location < 0 || detail is < 0 or > int.MaxValue)
        {
            throw new PdfException(PdfError.Format, "The xref stream entry offset or detail exceeds its signed width.");
        }

        if (type is (long)XrefEntryType.InFile or (long)XrefEntryType.Compressed)
        {
            table.SetIfMissing(number, (XrefEntryType)type, location, (int)detail);
        }
        else if (type == (long)XrefEntryType.Free)
        {
            table.NoteFree(number, (int)detail);
        }
    }

    /// <summary>Reads a big-endian field.</summary>
    /// <param name="bytes">The field's bytes.</param>
    /// <returns>The value.</returns>
    private static long ReadField(ReadOnlySpan<byte> bytes)
    {
        var value = 0L;
        foreach (var b in bytes)
        {
            value = (value << ByteBits) | b;
        }

        return value;
    }

    /// <summary>A classic table entry.</summary>
    /// <param name="Offset">The object's offset.</param>
    /// <param name="Generation">The generation.</param>
    /// <param name="InUse">Whether the entry is in use.</param>
    private readonly record struct ClassicEntry(long Offset, int Generation, bool InUse);

    /// <summary>A cross-reference stream subsection.</summary>
    /// <param name="First">The first object number.</param>
    /// <param name="Count">The number of entries.</param>
    /// <param name="Skip">Whether the range lies outside the document's object slots.</param>
    private readonly record struct Subsection(int First, int Count, bool Skip);
}

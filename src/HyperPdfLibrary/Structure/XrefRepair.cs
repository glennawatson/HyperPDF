// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Structure;

/// <summary>
/// Rebuilds the cross-reference table of a damaged file by scanning for object headers with vectorised searches. Later
/// copies of an object win, as later incremental updates would; objects in object streams fill the gaps. The file is
/// scanned in windows, so it is never read into memory whole.
/// </summary>
internal static class XrefRepair
{
    /// <summary>The base of decimal digits.</summary>
    private const int Ten = 10;

    /// <summary>The widest object or generation number read backwards.</summary>
    private const int MaxDigits = 10;

    /// <summary>How far into an object its /Type /ObjStm is looked for.</summary>
    private const int HeaderWindow = 256;

    /// <summary>How far before an <c>obj</c> keyword its "number generation" header is read.</summary>
    private const int HeaderLookback = 256;

    /// <summary>Gets the keyword after an object header.</summary>
    private static ReadOnlySpan<byte> ObjKeyword => "obj"u8;

    /// <summary>Gets the keyword that starts a stream's data.</summary>
    private static ReadOnlySpan<byte> StreamKeyword => "stream"u8;

    /// <summary>Scans the file and builds a table.</summary>
    /// <param name="file">The file.</param>
    /// <param name="store">The objects being read, which parse trailers and object streams.</param>
    /// <returns>The table; its trailer is <see langword="null"/> when none was found.</returns>
    internal static XrefTable Rebuild(PdfByteSource file, PdfObjectStore store)
    {
        var table = new XrefTable { Repaired = true, };
        ScanObjects(file, table, store.Context);
        table.Trailer = FindTrailer(file, store, table);

        // An encrypted file's object streams cannot be read until the key is known; opening re-runs this after authenticating.
        if (store.Security is not null || table.Trailer?.ContainsKey(KnownName.Encrypt) != true)
        {
            IndexCompressed(table, store);
        }

        return table;
    }

    /// <summary>
    /// Adds the objects packed in the table's object streams, in file order. A later object stream wins over an earlier one,
    /// and a plain object at the same or a later position wins over a compressed one.
    /// </summary>
    /// <param name="table">The rebuilt table.</param>
    /// <param name="store">The objects being read.</param>
    internal static void IndexCompressed(XrefTable table, PdfObjectStore store)
    {
        foreach (var offset in table.ObjectStreams)
        {
            PdfOpenContext.ThrowIfCancelled(store.Context);
            if (!StoreParsing.TryParseObjectAt(store, offset, out var id, out var value) || value.AsStream() is not { } stream)
            {
                continue;
            }

            var numbers = ObjectStreamIndex.Read(stream).Numbers;
            for (var i = 0; i < numbers.Length; i++)
            {
                AddCompressed(table, numbers[i], id.Number, i, offset);
            }
        }
    }

    /// <summary>Adds one compressed object unless a plain copy at the same or a later position exists.</summary>
    /// <param name="table">The table.</param>
    /// <param name="number">The object number.</param>
    /// <param name="container">The object stream's number.</param>
    /// <param name="index">The index in the object stream.</param>
    /// <param name="containerOffset">The object stream's file offset.</param>
    private static void AddCompressed(XrefTable table, int number, int container, int index, long containerOffset)
    {
        if (number <= 0 || (table.GetType(number) == XrefEntryType.InFile && table.GetLocation(number) >= containerOffset))
        {
            return;
        }

        table.Set(number, XrefEntryType.Compressed, container, index);
    }

    /// <summary>Indexes every object header, skipping stream bodies so text inside them is not mistaken for objects.</summary>
    /// <param name="file">The file.</param>
    /// <param name="table">The table receiving the objects and the object stream offsets.</param>
    /// <param name="context">The cancellation context, or <see langword="null"/>.</param>
    private static void ScanObjects(PdfByteSource file, XrefTable table, PdfOpenContext? context)
    {
        var position = 0L;
        while (true)
        {
            PdfOpenContext.ThrowIfCancelled(context);
            var keyword = PdfByteSearch.IndexOf(file, ObjKeyword, position);
            if (keyword < 0)
            {
                break;
            }

            position = keyword + ObjKeyword.Length;
            var after = file.ByteAt(position);
            if (!TryReadHeaderBefore(file, keyword, out var number, out var generation, out var start) || (after >= 0 && !PdfCharacters.EndsToken((byte)after)))
            {
                continue;
            }

            table.Set(number, XrefEntryType.InFile, start, generation);
            if (PdfByteSearch.IndexOf(file, "ObjStm"u8, start, start + HeaderWindow) >= 0)
            {
                table.AddObjectStream(start);
            }

            position = SkipStreamBody(file, position);
        }
    }

    /// <summary>Reads the "number generation" header before an <c>obj</c> keyword.</summary>
    /// <param name="file">The file.</param>
    /// <param name="keyword">The keyword's offset.</param>
    /// <param name="number">The object number.</param>
    /// <param name="generation">The generation.</param>
    /// <param name="start">The offset of the object number.</param>
    /// <returns><see langword="true"/> when a header precedes the keyword.</returns>
    private static bool TryReadHeaderBefore(PdfByteSource file, long keyword, out int number, out int generation, out long start)
    {
        var windowStart = Math.Max(0, keyword - HeaderLookback);
        using var window = file.Lease(windowStart, (int)(keyword - windowStart));
        var found = TryReadHeaderBackwards(window.Span, windowStart == 0, out number, out generation, out var relative);
        start = windowStart + relative;
        return found;
    }

    /// <summary>Finds where an object's stream body ends, when the object is a stream.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The offset after the object's <c>obj</c> keyword.</param>
    /// <returns>The offset after <c>endstream</c>, or <paramref name="from"/> when the object has no stream.</returns>
    private static long SkipStreamBody(PdfByteSource file, long from)
    {
        // The dictionary ends before the next "obj", which is at the latest this object's "endobj".
        var next = PdfByteSearch.IndexOf(file, ObjKeyword, from);
        var keyword = FindStreamKeyword(file, from, next < 0 ? file.Length : next);
        if (keyword < 0)
        {
            return from;
        }

        var bodyStart = SkipLineEnd(file, keyword + StreamKeyword.Length);
        var end = FindStreamEnd(file, bodyStart, ReadDirectLength(file, from, keyword));
        return end < 0 ? from : end;
    }

    /// <summary>Finds the <c>stream</c> keyword that follows a dictionary.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The start of the region searched.</param>
    /// <param name="regionEnd">The end of the region searched.</param>
    /// <returns>The offset of the keyword, or -1.</returns>
    private static long FindStreamKeyword(PdfByteSource file, long from, long regionEnd)
    {
        var offset = from;
        while (offset < regionEnd)
        {
            var at = PdfByteSearch.IndexOf(file, StreamKeyword, offset, regionEnd);
            if (at < 0)
            {
                return -1;
            }

            var after = at + StreamKeyword.Length;
            if (at > from && PdfCharacters.EndsToken((byte)file.ByteAt(at - 1)) && (after >= regionEnd || file.ByteAt(after) is '\r' or '\n' or ' '))
            {
                return at;
            }

            offset = after;
        }

        return -1;
    }

    /// <summary>Skips an optional space and one line end.</summary>
    /// <param name="file">The file.</param>
    /// <param name="position">The offset after the keyword.</param>
    /// <returns>The offset of the data.</returns>
    private static long SkipLineEnd(PdfByteSource file, long position)
    {
        position = PdfByteSearch.SkipAny(file, position, PdfByteSearch.Space);
        if (file.ByteAt(position) == '\r')
        {
            position++;
        }

        if (file.ByteAt(position) == '\n')
        {
            position++;
        }

        return position;
    }

    /// <summary>Finds the end of a stream body: the declared length when <c>endstream</c> follows it, else the next <c>endstream</c>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="bodyStart">The offset of the data.</param>
    /// <param name="length">The direct /Length, or -1.</param>
    /// <returns>The offset after <c>endstream</c>, or -1 when there is none.</returns>
    private static long FindStreamEnd(PdfByteSource file, long bodyStart, long length)
    {
        if (length >= 0 && bodyStart + length <= file.Length)
        {
            var at = PdfByteSearch.SkipAny(file, bodyStart + length, PdfCharacters.Whitespace);
            if (PdfByteSearch.StartsWith(file, at, PdfKeywords.EndStream))
            {
                return at + PdfKeywords.EndStream.Length;
            }
        }

        var found = PdfByteSearch.IndexOf(file, PdfKeywords.EndStream, bodyStart);
        return found < 0 ? -1 : found + PdfKeywords.EndStream.Length;
    }

    /// <summary>Reads a direct integer /Length from a stream dictionary.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The start of the dictionary.</param>
    /// <param name="end">The end of the dictionary.</param>
    /// <returns>The length, or -1 when it is missing, an indirect reference, or the dictionary is too large to read.</returns>
    private static long ReadDirectLength(PdfByteSource file, long from, long end)
    {
        if (end - from > PdfLimits.MaxObjectWindow)
        {
            return -1;
        }

        using var window = file.Lease(from, (int)(end - from));
        return ReadDirectLength(window.Span);
    }

    /// <summary>Reads a direct integer /Length from a stream dictionary's bytes.</summary>
    /// <param name="dictionary">The dictionary's bytes.</param>
    /// <returns>The length, or -1 when it is missing or an indirect reference.</returns>
    private static long ReadDirectLength(ReadOnlySpan<byte> dictionary)
    {
        var offset = 0;
        while (offset < dictionary.Length)
        {
            var found = dictionary[offset..].IndexOf("/Length"u8);
            if (found < 0)
            {
                return -1;
            }

            var at = offset + found + "/Length"u8.Length;
            if (at < dictionary.Length && (PdfCharacters.IsWhitespace(dictionary[at]) || dictionary[at] is >= (byte)'0' and <= (byte)'9'))
            {
                return ParseDirectInteger(dictionary[at..]);
            }

            offset = at;
        }

        return -1;
    }

    /// <summary>Parses an integer that is not the start of "n g R".</summary>
    /// <param name="text">The bytes after the key.</param>
    /// <returns>The integer, or -1.</returns>
    private static long ParseDirectInteger(ReadOnlySpan<byte> text)
    {
        var start = text.IndexOfAnyExcept(PdfCharacters.Whitespace);
        if (start < 0)
        {
            return -1;
        }

        var digits = text[start..];
        var length = digits.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
        if (length < 0)
        {
            length = digits.Length;
        }

        if (length == 0 || length > MaxDigits)
        {
            return -1;
        }

        var rest = digits[length..];
        var next = rest.IndexOfAnyExcept(PdfCharacters.Whitespace);
        return next >= 0 && rest[next] is >= (byte)'0' and <= (byte)'9' ? -1 : long.Parse(digits[..length], NumberStyles.None, CultureInfo.InvariantCulture);
    }

    /// <summary>Reads "number generation" backwards from the end of a window that ends at an <c>obj</c> keyword.</summary>
    /// <param name="data">The bytes before the keyword.</param>
    /// <param name="atFileStart">Whether the window starts at the start of the file, which ends a token.</param>
    /// <param name="number">The object number.</param>
    /// <param name="generation">The generation.</param>
    /// <param name="start">The offset of the object number in the window.</param>
    /// <returns><see langword="true"/> when a header precedes the keyword.</returns>
    private static bool TryReadHeaderBackwards(ReadOnlySpan<byte> data, bool atFileStart, out int number, out int generation, out int start)
    {
        number = 0;
        start = 0;
        var at = SkipSpaceBackwards(data, data.Length - 1);
        if (!TryReadNumberBackwards(data, ref at, out generation))
        {
            return false;
        }

        var afterNumber = at;
        at = SkipSpaceBackwards(data, at);
        if (at == afterNumber || !TryReadNumberBackwards(data, ref at, out number) || number == 0)
        {
            return false;
        }

        start = at + 1;
        return at < 0 ? atFileStart : PdfCharacters.EndsToken(data[at]);
    }

    /// <summary>Moves backwards over white space.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="at">The offset to start at.</param>
    /// <returns>The offset of the first byte that is not white space, or -1.</returns>
    private static int SkipSpaceBackwards(ReadOnlySpan<byte> data, int at)
    {
        while (at >= 0 && PdfCharacters.IsWhitespace(data[at]))
        {
            at--;
        }

        return at;
    }

    /// <summary>Reads a decimal number backwards.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="at">The offset of the last digit; moved to the byte before the first digit.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when at least one digit was read.</returns>
    private static bool TryReadNumberBackwards(ReadOnlySpan<byte> data, ref int at, out int value)
    {
        value = 0;
        var scale = 1L;
        var digits = 0;
        while (at >= 0 && data[at] is >= (byte)'0' and <= (byte)'9' && digits < MaxDigits)
        {
            value = (int)Math.Min(PdfLimits.MaxObjectNumber + 1L, value + ((data[at] - '0') * scale));
            scale *= Ten;
            digits++;
            at--;
        }

        return digits > 0 && value <= PdfLimits.MaxObjectNumber;
    }

    /// <summary>Finds the trailer: the last <c>trailer</c> dictionary or cross-reference stream with /Root, or a made-up one.</summary>
    /// <param name="file">The file.</param>
    /// <param name="store">The objects being read.</param>
    /// <param name="table">The rebuilt table.</param>
    /// <returns>The trailer, or <see langword="null"/>.</returns>
    private static PdfDictionary? FindTrailer(PdfByteSource file, PdfObjectStore store, XrefTable table)
    {
        var end = file.Length;
        while (end > 0)
        {
            PdfOpenContext.ThrowIfCancelled(store.Context);
            var found = PdfByteSearch.LastIndexOf(file, PdfKeywords.Trailer, end);
            if (found < 0)
            {
                break;
            }

            if (StoreParsing.ParseValueAt(store, found + PdfKeywords.Trailer.Length).AsDictionary() is { } trailer && trailer.ContainsKey(KnownName.Root))
            {
                return trailer;
            }

            end = found;
        }

        var made = FindRootObject(store, table);
        if (made is not null)
        {
            PdfOpenContext.Report(store.Context, PdfDiagnosticCode.TrailerRebuilt, "The trailer was missing and was made by scanning the file.", 0, -1);
        }

        return made;
    }

    /// <summary>Makes a trailer from the newest catalog object or cross-reference stream found.</summary>
    /// <param name="store">The objects being read.</param>
    /// <param name="table">The rebuilt table.</param>
    /// <returns>The trailer, or <see langword="null"/>.</returns>
    private static PdfDictionary? FindRootObject(PdfObjectStore store, XrefTable table)
    {
        for (var number = table.Size - 1; number > 0; number--)
        {
            PdfOpenContext.ThrowIfCancelled(store.Context);
            if (table.GetType(number) != XrefEntryType.InFile || !StoreParsing.TryParseObjectAt(store, table.GetLocation(number), out var id, out var value))
            {
                continue;
            }

            var dictionary = value.AsDictionary();
            if (dictionary is null)
            {
                continue;
            }

            if (dictionary.IsName(KnownName.Type, KnownName.XRef) && dictionary.ContainsKey(KnownName.Root))
            {
                return dictionary;
            }

            if (!dictionary.IsName(KnownName.Type, KnownName.Catalog))
            {
                continue;
            }

            var trailer = new PdfDictionary(store);
            trailer.Add(KnownName.Root, PdfValue.FromReference(id));
            return trailer;
        }

        return null;
    }
}

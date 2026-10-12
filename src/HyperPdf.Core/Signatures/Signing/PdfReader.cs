// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// Reads a PDF's cross-reference sections (classic tables and compressed cross-reference streams, following
/// <c>/Prev</c> and <c>/XRefStm</c>) and finds objects, including those packed into object streams.
/// </summary>
internal static class PdfReader
{
    /// <summary>The PNG predictors start here.</summary>
    private const int PngPredictors = 10;

    /// <summary>PNG filter: none.</summary>
    private const int PngNone = 0;

    /// <summary>PNG filter: sub.</summary>
    private const int PngSub = 1;

    /// <summary>PNG filter: up.</summary>
    private const int PngUp = 2;

    /// <summary>PNG filter: average.</summary>
    private const int PngAverage = 3;

    /// <summary>PNG filter: Paeth.</summary>
    private const int PngPaeth = 4;

    /// <summary>The two brackets around an array.</summary>
    private const int Brackets = 2;

    /// <summary>The length of a CR LF line ending.</summary>
    private const int CarriageReturnLineFeed = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The cross-reference stream field count.</summary>
    private const int XrefFields = 3;

    /// <summary>The most cross-reference sections followed, guarding against loops in damaged files.</summary>
    private const int MaxSections = 512;

    /// <summary>Reads a file's structure.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The structure.</returns>
    /// <exception cref="InvalidDataException">The file is too damaged to read.</exception>
    internal static PdfStructure Read(ReadOnlyMemory<byte> file)
    {
        try
        {
            var structure = ReadCrossReferences(file);
            if (PdfRepair.IsConsistent(structure))
            {
                return structure;
            }
        }
        catch (InvalidDataException)
        {
            // Rebuilt below.
        }

        return PdfRepair.Rebuild(file);
    }

    /// <summary>Makes a structure over a file's bytes, using the array behind them when they start it.</summary>
    /// <param name="file">The file.</param>
    /// <param name="entries">The object locations.</param>
    /// <param name="trailer">The trailer dictionary.</param>
    /// <param name="startXref">The offset of the newest cross-reference section, or -1.</param>
    /// <param name="repaired">Whether the locations were rebuilt by scanning a damaged file.</param>
    /// <returns>The structure.</returns>
    internal static PdfStructure Structure(ReadOnlyMemory<byte> file, Dictionary<int, XrefEntry> entries, byte[] trailer, long startXref, bool repaired)
    {
        // A view that does not start its array, which callers here never pass, is copied once.
        var buffer = MemoryMarshal.TryGetArray(file, out var segment) && segment.Offset == 0 ? segment.Array! : file.ToArray();
        return new(buffer, file.Length, entries, trailer, startXref) { Repaired = repaired };
    }

    /// <summary>Gets the decoded bytes of an object stream.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="number">The object stream's number.</param>
    /// <returns>The decoded bytes.</returns>
    internal static byte[] GetObjectStream(PdfStructure structure, int number)
    {
        ArgumentNullException.ThrowIfNull(structure);
        if (structure.ObjectStreams.TryGetValue(number, out var cached))
        {
            return cached;
        }

        var dictionary = SkipObjectHeader(structure.File.Span, (int)structure.Entries[number].Location);
        var decoded = DecodeStream(structure.File.Span, dictionary, structure);
        structure.ObjectStreams[number] = decoded;
        return decoded;
    }

    /// <summary>Gets an object's value, such as its dictionary or array.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The value's bytes.</returns>
    /// <exception cref="InvalidDataException">The object is missing.</exception>
    internal static ReadOnlyMemory<byte> GetObject(PdfStructure structure, int number)
    {
        ArgumentNullException.ThrowIfNull(structure);
        if (!structure.Entries.TryGetValue(number, out var entry) || entry.Type == 0)
        {
            throw new InvalidDataException($"Object {number} is missing.");
        }

        if (entry.Type == 1)
        {
            var file = structure.File;
            var value = SkipObjectHeader(file.Span, (int)entry.Location);
            return file.Slice(value, PdfSyntax.ValueEnd(file.Span, value) - value);
        }

        var stream = GetObjectStream(structure, (int)entry.Location);
        var container = structure.File.Span;
        var dictionary = SkipObjectHeader(container, (int)structure.Entries[(int)entry.Location].Location);
        _ = PdfSyntax.ReadLong(container, PdfSyntax.FindKey(container, dictionary, "First"u8), out var first);
        var header = 0;
        long objectOffset = -1;
        for (var i = 0; i <= entry.Index && header >= 0; i++)
        {
            header = PdfSyntax.ReadLong(stream, PdfSyntax.SkipSpace(stream, header), out _);
            header = header < 0 ? -1 : PdfSyntax.ReadLong(stream, PdfSyntax.SkipSpace(stream, header), out objectOffset);
        }

        if (header < 0 || objectOffset < 0)
        {
            throw new InvalidDataException($"Object {number} is missing from its object stream.");
        }

        var start = PdfSyntax.SkipSpace(stream, (int)(first + objectOffset));
        return stream.AsMemory(start, PdfSyntax.ValueEnd(stream, start) - start);
    }

    /// <summary>Reads a value that may be a reference, returning the referenced object's value or the value itself.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="data">The bytes holding the value.</param>
    /// <param name="index">The value's first byte.</param>
    /// <returns>The resolved value.</returns>
    internal static ReadOnlyMemory<byte> Resolve(PdfStructure structure, ReadOnlyMemory<byte> data, int index) =>
        PdfSyntax.TryReadReference(data.Span, index, out var number)
            ? GetObject(structure, number)
            : data.Slice(index, PdfSyntax.ValueEnd(data.Span, index) - index);

    /// <summary>Reads the structure from the file's cross-reference sections.</summary>
    /// <param name="memory">The file.</param>
    /// <returns>The structure.</returns>
    /// <exception cref="InvalidDataException">The file's cross-reference information is damaged.</exception>
    private static PdfStructure ReadCrossReferences(ReadOnlyMemory<byte> memory)
    {
        var file = memory.Span;
        var marker = file.LastIndexOf("startxref"u8);
        if (marker < 0 || PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, marker + "startxref"u8.Length), out var start) < 0)
        {
            throw new InvalidDataException("The file has no cross-reference information.");
        }

        var entries = new Dictionary<int, XrefEntry>();
        byte[]? trailer = null;
        var pending = new Stack<long>();
        var visited = new HashSet<long>();
        pending.Push(start);
        while (pending.Count > 0 && visited.Count < MaxSections)
        {
            var offset = pending.Pop();
            if (!visited.Add(offset) || offset < 0 || offset >= file.Length)
            {
                continue;
            }

            var dictionary = ReadSection(file, (int)offset, entries);
            trailer ??= dictionary;
            PushReference(file, dictionary, "Prev"u8, pending);
            PushReference(file, dictionary, "XRefStm"u8, pending);
        }

        return trailer is null ? throw new InvalidDataException("The file's cross-reference information is damaged.") : Structure(memory, entries, trailer, start, false);
    }

    /// <summary>Queues the section a trailer key points at.</summary>
    /// <param name="file">The file.</param>
    /// <param name="dictionary">The trailer.</param>
    /// <param name="key">The key.</param>
    /// <param name="pending">The sections still to read.</param>
    private static void PushReference(ReadOnlySpan<byte> file, byte[] dictionary, ReadOnlySpan<byte> key, Stack<long> pending)
    {
        var value = PdfSyntax.FindKey(dictionary, 0, key);
        if (value >= 0 && PdfSyntax.ReadLong(dictionary, value, out var offset) >= 0 && offset < file.Length)
        {
            pending.Push(offset);
        }
    }

    /// <summary>Reads one cross-reference section, adding entries not already defined by a newer section.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The section's offset.</param>
    /// <param name="entries">The entries so far.</param>
    /// <returns>The section's trailer dictionary.</returns>
    private static byte[] ReadSection(ReadOnlySpan<byte> file, int offset, Dictionary<int, XrefEntry> entries)
    {
        var index = PdfSyntax.SkipSpace(file, offset);
        return file[index..].StartsWith("xref"u8) ? ReadTable(file, index + "xref"u8.Length, entries) : ReadStream(file, index, entries);
    }

    /// <summary>Reads a classic cross-reference table and its trailer.</summary>
    /// <param name="file">The file.</param>
    /// <param name="index">The first byte after <c>xref</c>.</param>
    /// <param name="entries">The entries so far.</param>
    /// <returns>The trailer dictionary.</returns>
    /// <exception cref="InvalidDataException">The table is damaged.</exception>
    private static byte[] ReadTable(ReadOnlySpan<byte> file, int index, Dictionary<int, XrefEntry> entries)
    {
        while (true)
        {
            index = PdfSyntax.SkipSpace(file, index);
            if (file[index..].StartsWith("trailer"u8))
            {
                var dictionary = PdfSyntax.SkipSpace(file, index + "trailer"u8.Length);
                return file[dictionary..PdfSyntax.ValueEnd(file, dictionary)].ToArray();
            }

            index = PdfSyntax.ReadLong(file, index, out var first);
            index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out var count);
            if (index < 0)
            {
                throw new InvalidDataException("The cross-reference table is damaged.");
            }

            for (var i = 0; i < count; i++)
            {
                index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out var location);
                index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out _);
                index = PdfSyntax.SkipSpace(file, index);
                if (index < 0 || index >= file.Length)
                {
                    throw new InvalidDataException("The cross-reference table is damaged.");
                }

                _ = entries.TryAdd((int)(first + i), new(file[index] == (byte)'n' ? 1 : 0, location, 0));
                index++;
            }
        }
    }

    /// <summary>Reads a cross-reference stream.</summary>
    /// <param name="file">The file.</param>
    /// <param name="index">The stream object's first byte.</param>
    /// <param name="entries">The entries so far.</param>
    /// <returns>The stream's dictionary, which serves as the trailer.</returns>
    private static byte[] ReadStream(ReadOnlySpan<byte> file, int index, Dictionary<int, XrefEntry> entries)
    {
        var dictionary = SkipObjectHeader(file, index);
        var trailer = file[dictionary..PdfSyntax.ValueEnd(file, dictionary)].ToArray();
        Span<int> widths = stackalloc int[XrefFields];
        var cursor = PdfSyntax.FindKey(file, dictionary, "W"u8) + 1;
        for (var i = 0; i < XrefFields; i++)
        {
            cursor = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, cursor), out var width);
            widths[i] = (int)width;
        }

        _ = PdfSyntax.ReadLong(file, PdfSyntax.FindKey(file, dictionary, "Size"u8), out var size);
        var data = DecodeStream(file, dictionary, null);
        var indexArray = PdfSyntax.FindKey(file, dictionary, "Index"u8);
        if (indexArray < 0)
        {
            _ = ReadRows(data, widths, 0, 0, size, entries);
            return trailer;
        }

        var ranges = file.Slice(indexArray + 1, PdfSyntax.ValueEnd(file, indexArray) - indexArray - Brackets);
        var rangeCursor = 0;
        var row = 0;
        while (true)
        {
            rangeCursor = PdfSyntax.ReadLong(ranges, PdfSyntax.SkipSpace(ranges, rangeCursor), out var first);
            if (rangeCursor < 0)
            {
                return trailer;
            }

            rangeCursor = PdfSyntax.ReadLong(ranges, PdfSyntax.SkipSpace(ranges, rangeCursor), out var count);
            row = ReadRows(data, widths, row, first, count, entries);
        }
    }

    /// <summary>Reads rows of a cross-reference stream for a run of object numbers.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <param name="widths">The field widths.</param>
    /// <param name="row">The first row.</param>
    /// <param name="first">The first object number.</param>
    /// <param name="count">The number of objects.</param>
    /// <param name="entries">The entries so far.</param>
    /// <returns>The next row.</returns>
    private static int ReadRows(byte[] data, ReadOnlySpan<int> widths, int row, long first, long count, Dictionary<int, XrefEntry> entries)
    {
        var rowLength = widths[0] + widths[1] + widths[XrefFields - 1];
        for (var i = 0; i < count && (row + 1) * rowLength <= data.Length; i++, row++)
        {
            var fields = data.AsSpan(row * rowLength, rowLength);
            var type = widths[0] == 0 ? 1 : (int)ReadField(fields[..widths[0]]);
            var second = ReadField(fields.Slice(widths[0], widths[1]));
            var third = ReadField(fields[(widths[0] + widths[1])..]);
            _ = entries.TryAdd((int)(first + i), new(type, second, (int)third));
        }

        return row;
    }

    /// <summary>Reads a big-endian field of a cross-reference stream row.</summary>
    /// <param name="field">The field bytes.</param>
    /// <returns>The value.</returns>
    private static long ReadField(ReadOnlySpan<byte> field)
    {
        long value = 0;
        foreach (var b in field)
        {
            value = (value << ByteBits) | b;
        }

        return value;
    }

    /// <summary>Skips an object's <c>N G obj</c> header.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The object's offset.</param>
    /// <returns>The value's first byte.</returns>
    /// <exception cref="InvalidDataException">The header is missing.</exception>
    private static int SkipObjectHeader(ReadOnlySpan<byte> file, int offset)
    {
        var index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, offset), out _);
        index = index < 0 ? -1 : PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out _);
        index = index < 0 ? -1 : PdfSyntax.SkipSpace(file, index);
        if (index < 0 || index >= file.Length || !file[index..].StartsWith("obj"u8))
        {
            throw new InvalidDataException($"No object at offset {offset}.");
        }

        return PdfSyntax.SkipSpace(file, index + "obj"u8.Length);
    }

    /// <summary>Decodes a stream: its raw bytes, inflated when Flate encoded, with PNG predictors undone.</summary>
    /// <param name="file">The file.</param>
    /// <param name="dictionary">The stream dictionary's first byte.</param>
    /// <param name="structure">The structure, for an indirect <c>/Length</c>; <see langword="null"/> for cross-reference streams.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="NotSupportedException">The stream uses a filter other than Flate.</exception>
    /// <exception cref="InvalidDataException">The stream is damaged.</exception>
    private static byte[] DecodeStream(ReadOnlySpan<byte> file, int dictionary, PdfStructure? structure)
    {
        var end = PdfSyntax.ValueEnd(file, dictionary);
        var index = PdfSyntax.SkipSpace(file, end);
        if (!file[index..].StartsWith("stream"u8))
        {
            throw new InvalidDataException("A stream is damaged.");
        }

        index += "stream"u8.Length;
        index += file[index] == (byte)'\r' ? CarriageReturnLineFeed : 1;
        var length = ReadLength(file, dictionary, structure);
        if (length < 0 || index + length > file.Length)
        {
            length = file[index..].IndexOf("endstream"u8);
        }

        var raw = file.Slice(index, (int)length);
        var filter = PdfSyntax.FindKey(file, dictionary, "Filter"u8);
        if (filter < 0)
        {
            return raw.ToArray();
        }

        var filterText = file.Slice(filter, PdfSyntax.ValueEnd(file, filter) - filter);
        if (filterText.IndexOf("FlateDecode"u8) < 0 || filterText.Count((byte)'/') > 1)
        {
            throw new NotSupportedException("This PDF stores its structure in a way the signer does not support yet.");
        }

        using var input = new MemoryStream(raw.ToArray());
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        var decoded = output.ToArray();
        return Unpredict(file, dictionary, decoded);
    }

    /// <summary>Reads a stream's length, resolving an indirect one when possible.</summary>
    /// <param name="file">The file.</param>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="structure">The structure, or <see langword="null"/>.</param>
    /// <returns>The length, or -1 when unknown.</returns>
    private static long ReadLength(ReadOnlySpan<byte> file, int dictionary, PdfStructure? structure)
    {
        var value = PdfSyntax.FindKey(file, dictionary, "Length"u8);
        if (value < 0)
        {
            return -1;
        }

        if (structure is not null && PdfSyntax.TryReadReference(file, value, out var number))
        {
            var resolved = GetObject(structure, number);
            return PdfSyntax.ReadLong(resolved.Span, 0, out var indirect) < 0 ? -1 : indirect;
        }

        return PdfSyntax.ReadLong(file, value, out var length) < 0 ? -1 : length;
    }

    /// <summary>Undoes PNG predictors given in <c>/DecodeParms</c>.</summary>
    /// <param name="file">The file.</param>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="data">The inflated bytes.</param>
    /// <returns>The original bytes.</returns>
    private static byte[] Unpredict(ReadOnlySpan<byte> file, int dictionary, byte[] data)
    {
        var columns = GetPngColumns(file, dictionary);
        if (columns <= 0)
        {
            return data;
        }

        var rows = data.Length / (columns + 1);
        var output = new byte[rows * columns];
        for (var row = 0; row < rows; row++)
        {
            var above = row > 0 ? output.AsSpan((row - 1) * columns, columns) : default;
            UnpredictRow(data[row * (columns + 1)], data.AsSpan((row * (columns + 1)) + 1, columns), output.AsSpan(row * columns, columns), above);
        }

        return output;
    }

    /// <summary>Gets the row width of a PNG-predicted stream.</summary>
    /// <param name="file">The file.</param>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <returns>The columns, or 0 when the stream is not PNG-predicted.</returns>
    private static int GetPngColumns(ReadOnlySpan<byte> file, int dictionary)
    {
        var parameters = PdfSyntax.FindKey(file, dictionary, "DecodeParms"u8);
        if (parameters < 0 || file[parameters] != (byte)'<')
        {
            return 0;
        }

        var predictorAt = PdfSyntax.FindKey(file, parameters, "Predictor"u8);
        if (predictorAt < 0 || PdfSyntax.ReadLong(file, predictorAt, out var predictor) < 0 || predictor < PngPredictors)
        {
            return 0;
        }

        var columnsAt = PdfSyntax.FindKey(file, parameters, "Columns"u8);
        return columnsAt >= 0 && PdfSyntax.ReadLong(file, columnsAt, out var columns) >= 0 ? (int)columns : 1;
    }

    /// <summary>Undoes one row's PNG filter.</summary>
    /// <param name="filterType">The row's filter.</param>
    /// <param name="source">The filtered row.</param>
    /// <param name="target">The row to fill.</param>
    /// <param name="above">The previous decoded row, or empty for the first row.</param>
    private static void UnpredictRow(byte filterType, ReadOnlySpan<byte> source, Span<byte> target, ReadOnlySpan<byte> above)
    {
        for (var i = 0; i < target.Length; i++)
        {
            var left = i > 0 ? target[i - 1] : 0;
            var up = above.IsEmpty ? 0 : above[i];
            var upLeft = above.IsEmpty || i == 0 ? 0 : above[i - 1];
            var predicted = filterType switch
            {
                PngSub => left,
                PngUp => up,
                PngAverage => (left + up) >> 1,
                PngPaeth => Paeth(left, up, upLeft),
                _ => PngNone,
            };
            target[i] = (byte)(source[i] + predicted);
        }
    }

    /// <summary>The PNG Paeth predictor.</summary>
    /// <param name="left">The byte to the left.</param>
    /// <param name="up">The byte above.</param>
    /// <param name="upLeft">The byte above and to the left.</param>
    /// <returns>The prediction.</returns>
    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpLeft = Math.Abs(estimate - upLeft);
        if (toLeft <= toUp && toLeft <= toUpLeft)
        {
            return left;
        }

        return toUp <= toUpLeft ? up : upLeft;
    }
}

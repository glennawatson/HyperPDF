// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>
/// Builds a two page linearized file: the linearization dictionary and first-page cross-reference section at the start
/// (with /Prev pointing at the main section), a hint stream, the first page's objects, then the rest and the main section.
/// The last startxref points at the first-page section, as linearized writers do.
/// </summary>
internal static class LinearizedDocuments
{
    /// <summary>The number of pages.</summary>
    internal const int PageCount = 2;

    /// <summary>The object number of the linearization dictionary.</summary>
    private const int LinearizationNumber = 7;

    /// <summary>The object number of the hint stream.</summary>
    private const int HintNumber = 8;

    /// <summary>The object number of an optional second hint stream.</summary>
    private const int SecondaryHintNumber = 9;

    /// <summary>The object number of the catalog.</summary>
    private const int CatalogNumber = 4;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPageNumber = 5;

    /// <summary>The object number of the first page's content.</summary>
    private const int FirstContentNumber = 6;

    /// <summary>The object number of the page tree root.</summary>
    private const int PagesNumber = 3;

    /// <summary>The object number of the second page.</summary>
    private const int SecondPageNumber = 1;

    /// <summary>The object number of the second page's content.</summary>
    private const int SecondContentNumber = 2;

    /// <summary>The object number of the main cross-reference stream.</summary>
    private const int MainStreamNumber = 9;

    /// <summary>The object number of the first-page cross-reference stream.</summary>
    private const int FirstStreamNumber = 10;

    /// <summary>The number of objects including the free head.</summary>
    private const int ObjectCount = 11;

    /// <summary>The byte count from the start of a classic main xref table to its first entry.</summary>
    private const int MainXrefHeaderLength = 9;

    /// <summary>The byte offset of the shared-object header in the decoded hint stream.</summary>
    private const int SharedHintOffset = 54;

    /// <summary>The number of bytes in the page and shared-object hint tables.</summary>
    private const int HintLength = 78;

    /// <summary>The least page object count in the two page groups.</summary>
    private const uint LeastPageObjects = 2;

    /// <summary>The least page group length encoded in the hint table.</summary>
    private const uint LeastPageLength = 1;

    /// <summary>The bits used for each object-count delta.</summary>
    private const ushort ObjectDeltaBits = 1;

    /// <summary>The bits used for each page-length delta.</summary>
    private const ushort PageLengthDeltaBits = 32;

    /// <summary>The unused page-header fields between length and shared-object widths.</summary>
    private const int UnusedPageHeaderBytes = 12;

    /// <summary>The page-length deltas start after the object-count byte.</summary>
    private const int PageLengthsOffset = 37;

    /// <summary>The shared-object count byte follows both page-length deltas.</summary>
    private const int SharedCountsOffset = 45;

    /// <summary>The size of one harmless comment line used to enlarge a benchmark file.</summary>
    private const int PaddingLineLength = 256;

    /// <summary>The number of bytes in a cross-reference stream row: type, offset, generation.</summary>
    private const int RowLength = 7;

    /// <summary>The offset of the generation in a row.</summary>
    private const int GenerationOffset = 5;

    /// <summary>The generation of the free head entry.</summary>
    private const int FreeGeneration = 65_535;

    /// <summary>Gets the content of the first page.</summary>
    internal static string FirstContent => "BT (First) Tj ET";

    /// <summary>Gets the content of the second page.</summary>
    internal static string SecondContent => "BT (Second) Tj ET";

    /// <summary>Builds the file.</summary>
    /// <param name="xrefStreams">Whether the two cross-reference sections are streams instead of tables.</param>
    /// <returns>The file.</returns>
    internal static byte[] Create(bool xrefStreams) => Create(xrefStreams, 0);

    /// <summary>Builds a linearized file with optional comment bytes after the first-page section.</summary>
    /// <param name="xrefStreams">Whether the cross-reference sections are streams.</param>
    /// <param name="paddingBytes">The extra comment bytes before the remaining pages.</param>
    /// <returns>The file.</returns>
    internal static byte[] Create(bool xrefStreams, int paddingBytes) => Create(xrefStreams, paddingBytes, false);

    /// <summary>Builds a classic linearized file with a second hint stream in /H.</summary>
    /// <returns>The file.</returns>
    internal static byte[] CreateWithSecondHint() => Create(false, 0, true);

    /// <summary>Builds a file with an optional overflow hint object.</summary>
    /// <param name="xrefStreams">Whether the cross-reference sections are streams.</param>
    /// <param name="paddingBytes">The extra comment bytes before the main xref.</param>
    /// <param name="secondHint">Whether /H names a second hint stream.</param>
    /// <returns>The file.</returns>
    private static byte[] Create(bool xrefStreams, int paddingBytes, bool secondHint)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(paddingBytes);
        var measured = new Layout();
        _ = Assemble(xrefStreams, paddingBytes, secondHint, new(), measured);
        var final = new Layout();
        return Assemble(xrefStreams, paddingBytes, secondHint, measured, final);
    }

    /// <summary>Writes the file once, reading the offsets of the previous pass and recording this pass's offsets.</summary>
    /// <param name="xrefStreams">Whether the sections are streams.</param>
    /// <param name="paddingBytes">The extra bytes between the first-page section and remaining objects.</param>
    /// <param name="secondHint">Whether /H names a second hint stream.</param>
    /// <param name="previous">The offsets measured by the previous pass.</param>
    /// <param name="current">Receives the offsets of this pass.</param>
    /// <returns>The file.</returns>
    private static byte[] Assemble(bool xrefStreams, int paddingBytes, bool secondHint, Layout previous, Layout current)
    {
        using var output = new MemoryStream();
        Write(output, "%PDF-1.7\n");
        current.Offsets[LinearizationNumber] = output.Position;
        var linearization = new StringBuilder();
        _ = linearization.Append(CultureInfo.InvariantCulture, $"{LinearizationNumber} 0 obj\n<< /Linearized 1 /L {previous.Length:D10} ");
        _ = linearization.Append(CultureInfo.InvariantCulture, $"/H [{previous.HintOffset:D10} {previous.HintObjectLength:D10}");
        if (secondHint)
        {
            _ = linearization.Append(CultureInfo.InvariantCulture, $" {previous.SecondHintOffset:D10} {previous.SecondHintLength:D10}");
        }

        _ = linearization.Append(CultureInfo.InvariantCulture, $"] /O {FirstPageNumber} ");
        var mainXrefItem = previous.MainXref + (xrefStreams ? 0 : MainXrefHeaderLength);
        _ = linearization.Append(CultureInfo.InvariantCulture, $"/E {previous.EndFirst:D10} /N {PageCount} /T {mainXrefItem:D10} >>\nendobj\n");
        Write(output, linearization.ToString());
        current.FirstXref = output.Position;
        current.Offsets[FirstStreamNumber] = output.Position;
        WriteFirstSection(output, xrefStreams, secondHint, previous);
        current.HintOffset = output.Position;
        current.Offsets[HintNumber] = output.Position;
        WriteObject(output, HintNumber, StreamObject($"/S {SharedHintOffset}", HintData(previous)));
        current.HintObjectLength = output.Position - current.HintOffset;
        if (secondHint)
        {
            current.SecondHintOffset = output.Position;
            current.Offsets[SecondaryHintNumber] = output.Position;
            WriteObject(output, SecondaryHintNumber, StreamObject(string.Empty, [(byte)'X']));
            current.SecondHintLength = output.Position - current.SecondHintOffset;
        }

        WriteFirstObjects(output, current);
        current.EndFirst = output.Position;
        WriteMainObjects(output, current);
        WritePadding(output, paddingBytes);
        current.MainXref = output.Position;
        current.Offsets[MainStreamNumber] = output.Position;
        WriteMainSection(output, xrefStreams, secondHint, previous);
        Write(output, string.Create(CultureInfo.InvariantCulture, $"startxref\n{current.FirstXref}\n%%EOF\n"));
        current.Length = output.Length;
        return output.ToArray();
    }

    /// <summary>Writes the first-page cross-reference section and its trailer, ending with the start-of-file startxref.</summary>
    /// <param name="output">The output.</param>
    /// <param name="xrefStreams">Whether to write a stream.</param>
    /// <param name="secondHint">Whether a second hint object appears before the catalog.</param>
    /// <param name="previous">The previous pass's offsets.</param>
    private static void WriteFirstSection(MemoryStream output, bool xrefStreams, bool secondHint, Layout previous)
    {
        if (xrefStreams)
        {
            var rows = new MemoryStream();
            for (var number = CatalogNumber; number <= FirstStreamNumber; number++)
            {
                AppendRow(rows, 1, previous.Offsets.GetValueOrDefault(number), 0);
            }

            var entries = string.Create(
                CultureInfo.InvariantCulture,
                $"/Type /XRef /Size {ObjectCount} /W [1 4 2] /Index [{CatalogNumber} {FirstStreamNumber - CatalogNumber + 1}] /Root {CatalogNumber} 0 R /Prev {previous.MainXref:D10}");
            WriteObject(output, FirstStreamNumber, StreamObject(entries, rows.ToArray()));
            Write(output, "startxref\n0\n%%EOF\n");
            return;
        }

        var last = secondHint ? SecondaryHintNumber : HintNumber;
        var table = new StringBuilder();
        _ = table.Append(CultureInfo.InvariantCulture, $"xref\n{CatalogNumber} {last - CatalogNumber + 1}\n");
        for (var number = CatalogNumber; number <= last; number++)
        {
            _ = table.Append(CultureInfo.InvariantCulture, $"{previous.Offsets.GetValueOrDefault(number):D10} 00000 n \n");
        }

        _ = table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {last + 1} /Root {CatalogNumber} 0 R /Prev {previous.MainXref:D10} >>\nstartxref\n0\n%%EOF\n");
        Write(output, table.ToString());
    }

    /// <summary>Writes the objects of the first page, and records their offsets.</summary>
    /// <param name="output">The output.</param>
    /// <param name="current">Receives the offsets.</param>
    private static void WriteFirstObjects(MemoryStream output, Layout current)
    {
        current.Offsets[CatalogNumber] = output.Position;
        WriteObject(output, CatalogNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {PagesNumber} 0 R >>"));
        current.Offsets[FirstPageNumber] = output.Position;
        WriteObject(
            output,
            FirstPageNumber,
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 200 100] /Resources << >> /Contents {FirstContentNumber} 0 R >>"));
        current.Offsets[FirstContentNumber] = output.Position;
        WriteObject(output, FirstContentNumber, StreamObject(string.Empty, Encoding.Latin1.GetBytes(FirstContent)));
    }

    /// <summary>Writes the page tree and the second page, and records their offsets.</summary>
    /// <param name="output">The output.</param>
    /// <param name="current">Receives the offsets.</param>
    private static void WriteMainObjects(MemoryStream output, Layout current)
    {
        current.Offsets[SecondPageNumber] = output.Position;
        WriteObject(
            output,
            SecondPageNumber,
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 100 200] /Resources << >> /Contents {SecondContentNumber} 0 R >>"));
        current.Offsets[SecondContentNumber] = output.Position;
        WriteObject(output, SecondContentNumber, StreamObject(string.Empty, Encoding.Latin1.GetBytes(SecondContent)));
        current.Offsets[PagesNumber] = output.Position;
        WriteObject(output, PagesNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{FirstPageNumber} 0 R {SecondPageNumber} 0 R] /Count {PageCount} >>"));
    }

    /// <summary>Writes the main cross-reference section: objects 0 to 3 (and the stream itself, for a stream).</summary>
    /// <param name="output">The output.</param>
    /// <param name="xrefStreams">Whether to write a stream.</param>
    /// <param name="secondHint">Whether /H names a second hint object.</param>
    /// <param name="previous">The previous pass's offsets.</param>
    private static void WriteMainSection(MemoryStream output, bool xrefStreams, bool secondHint, Layout previous)
    {
        if (xrefStreams)
        {
            var rows = new MemoryStream();
            AppendRow(rows, 0, 0, FreeGeneration);
            for (var number = SecondPageNumber; number <= PagesNumber; number++)
            {
                AppendRow(rows, 1, previous.Offsets.GetValueOrDefault(number), 0);
            }

            AppendRow(rows, 1, previous.MainXref, 0);
            WriteObject(output, MainStreamNumber, StreamObject($"/Type /XRef /Size {ObjectCount} /W [1 4 2] /Index [0 {PagesNumber + 1} {MainStreamNumber} 1]", rows.ToArray()));
            return;
        }

        var table = new StringBuilder("xref\n0 4\n0000000000 65535 f \n");
        for (var number = SecondPageNumber; number <= PagesNumber; number++)
        {
            _ = table.Append(CultureInfo.InvariantCulture, $"{previous.Offsets.GetValueOrDefault(number):D10} 00000 n \n");
        }

        _ = table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {(secondHint ? SecondaryHintNumber : HintNumber) + 1} >>\n");
        Write(output, table.ToString());
    }

    /// <summary>Appends one cross-reference stream row with fields of one, four and two bytes.</summary>
    /// <param name="rows">The rows so far.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="generation">The generation.</param>
    private static void AppendRow(MemoryStream rows, byte type, long offset, int generation)
    {
        Span<byte> row = stackalloc byte[RowLength];
        row[0] = type;
        BinaryPrimitives.WriteUInt32BigEndian(row[1..], (uint)offset);
        BinaryPrimitives.WriteUInt16BigEndian(row[GenerationOffset..], (ushort)generation);
        rows.Write(row);
    }

    /// <summary>Writes page-offset hints for the two object groups and an empty shared-object table.</summary>
    /// <param name="previous">The file offsets measured in the previous assembly pass.</param>
    /// <returns>The decoded hint-stream bytes.</returns>
    private static byte[] HintData(Layout previous)
    {
        var bytes = new byte[HintLength];
        var position = 0;
        var firstObjectOffset = Math.Max(1, previous.Offsets.GetValueOrDefault(FirstPageNumber) - previous.HintObjectLength);
        var firstLength = Math.Max(1, previous.EndFirst - previous.Offsets.GetValueOrDefault(FirstPageNumber));
        var secondLength = Math.Max(1, previous.Offsets.GetValueOrDefault(PagesNumber) - previous.EndFirst);
        WriteUInt32(bytes, ref position, LeastPageObjects);
        WriteUInt32(bytes, ref position, (uint)firstObjectOffset);
        WriteUInt16(bytes, ref position, ObjectDeltaBits);
        WriteUInt32(bytes, ref position, LeastPageLength);
        WriteUInt16(bytes, ref position, PageLengthDeltaBits);
        position += UnusedPageHeaderBytes;
        WriteUInt16(bytes, ref position, ObjectDeltaBits);
        WriteUInt16(bytes, ref position, ObjectDeltaBits);
        WriteUInt16(bytes, ref position, 0);
        WriteUInt16(bytes, ref position, 0);

        bytes[position] = 0;
        position = PageLengthsOffset;
        WriteUInt32(bytes, ref position, (uint)(firstLength - LeastPageLength));
        WriteUInt32(bytes, ref position, (uint)(secondLength - LeastPageLength));
        bytes[SharedCountsOffset] = 0;

        position = SharedHintOffset;
        WriteUInt32(bytes, ref position, 1);
        WriteUInt32(bytes, ref position, 1);
        WriteUInt32(bytes, ref position, 0);
        WriteUInt32(bytes, ref position, 0);
        WriteUInt16(bytes, ref position, 0);
        WriteUInt32(bytes, ref position, 0);
        WriteUInt16(bytes, ref position, ObjectDeltaBits);
        return bytes;
    }

    /// <summary>Writes a big-endian 32-bit hint field.</summary>
    /// <param name="bytes">The hint stream.</param>
    /// <param name="position">The field position, advanced after writing.</param>
    /// <param name="value">The field value.</param>
    private static void WriteUInt32(byte[] bytes, ref int position, uint value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(position), value);
        position += sizeof(uint);
    }

    /// <summary>Writes a big-endian 16-bit hint field.</summary>
    /// <param name="bytes">The hint stream.</param>
    /// <param name="position">The field position, advanced after writing.</param>
    /// <param name="value">The field value.</param>
    private static void WriteUInt16(byte[] bytes, ref int position, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(position), value);
        position += sizeof(ushort);
    }

    /// <summary>Writes PDF comment lines that enlarge the file without changing its pages.</summary>
    /// <param name="output">The PDF being assembled.</param>
    /// <param name="paddingBytes">The number of bytes to add.</param>
    private static void WritePadding(MemoryStream output, int paddingBytes)
    {
        if (paddingBytes == 0)
        {
            return;
        }

        var padding = new byte[paddingBytes];
        padding.AsSpan().Fill((byte)' ');
        for (var offset = 0; offset < padding.Length; offset += PaddingLineLength)
        {
            padding[offset] = (byte)'%';
            padding[Math.Min(offset + PaddingLineLength - 1, padding.Length - 1)] = (byte)'\n';
        }

        output.Write(padding);
    }

    /// <summary>Writes text.</summary>
    /// <param name="output">The output.</param>
    /// <param name="text">The text, one byte per character.</param>
    private static void Write(MemoryStream output, string text) => output.Write(Encoding.Latin1.GetBytes(text));

    /// <summary>Writes an indirect object.</summary>
    /// <param name="output">The output.</param>
    /// <param name="number">The object number.</param>
    /// <param name="body">The object body as text.</param>
    private static void WriteObject(MemoryStream output, int number, string body) =>
        Write(output, string.Create(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n"));

    /// <summary>Builds a stream object body.</summary>
    /// <param name="entries">The extra dictionary entries.</param>
    /// <param name="data">The data.</param>
    /// <returns>The body.</returns>
    private static string StreamObject(string entries, byte[] data) =>
        string.Create(CultureInfo.InvariantCulture, $"<< {entries} /Length {data.Length} >>\nstream\n{Encoding.Latin1.GetString(data)}\nendstream");

    /// <summary>The offsets one pass measured.</summary>
    private sealed class Layout
    {
        /// <summary>Gets the offset of each object.</summary>
        public Dictionary<int, long> Offsets { get; } = [];

        /// <summary>Gets or sets the offset of the first-page cross-reference section.</summary>
        public long FirstXref { get; set; }

        /// <summary>Gets or sets the offset of the main cross-reference section.</summary>
        public long MainXref { get; set; }

        /// <summary>Gets or sets the offset after the first page's objects.</summary>
        public long EndFirst { get; set; }

        /// <summary>Gets or sets the offset of the hint stream.</summary>
        public long HintOffset { get; set; }

        /// <summary>Gets or sets the length of the hint stream object.</summary>
        public long HintObjectLength { get; set; }

        /// <summary>Gets or sets the second hint object's offset.</summary>
        public long SecondHintOffset { get; set; }

        /// <summary>Gets or sets the second hint object's length.</summary>
        public long SecondHintLength { get; set; }

        /// <summary>Gets or sets the file length.</summary>
        public long Length { get; set; }
    }
}

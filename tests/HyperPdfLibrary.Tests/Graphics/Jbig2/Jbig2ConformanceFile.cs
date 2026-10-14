// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Splits a JBIG2 file (T.88 annex D: sequential or random-access organization) into its global segments and the segments
/// of each page, written in the embedded organization (segment header followed by data) that the decoder reads.
/// </summary>
internal sealed class Jbig2ConformanceFile
{
    /// <summary>The bytes of the file signature.</summary>
    private const int SignatureBytes = 8;

    /// <summary>The file flag for the sequential organization.</summary>
    private const int SequentialFlag = 0x01;

    /// <summary>The file flag for an unknown number of pages, which leaves out the page count.</summary>
    private const int UnknownPagesFlag = 0x02;

    /// <summary>The bytes of the page count field.</summary>
    private const int PageCountBytes = 4;

    /// <summary>The bytes of a 32-bit field.</summary>
    private const int IntBytes = 4;

    /// <summary>The segment flag bit that makes the page association 4 bytes.</summary>
    private const int LongAssociationFlag = 0x40;

    /// <summary>The referred-to count that marks the long form.</summary>
    private const int LongFormCount = 7;

    /// <summary>The shift of the referred-to count in the short form.</summary>
    private const int CountShift = 5;

    /// <summary>The mask of the long form's count.</summary>
    private const uint LongCountMask = 0x1FFFFFFF;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The largest segment number whose referred-to numbers take one byte.</summary>
    private const int OneByteNumbers = 256;

    /// <summary>The largest segment number whose referred-to numbers take two bytes.</summary>
    private const int TwoByteNumbers = 65_536;

    /// <summary>The bytes of a two-byte segment number reference.</summary>
    private const int TwoByteReference = 2;

    /// <summary>The segment type of an end-of-file segment.</summary>
    private const int EndOfFileType = 51;

    /// <summary>The mask of the segment type in the segment flags.</summary>
    private const int TypeMask = 0x3F;

    /// <summary>The page association of a global segment.</summary>
    private const int GlobalPage = 0;

    /// <summary>The segments of each page and of the globals, as header and data bytes.</summary>
    private readonly Dictionary<int, List<byte>> _pages = [];

    /// <summary>Initializes a new instance of the <see cref="Jbig2ConformanceFile"/> class.</summary>
    /// <param name="file">The JBIG2 file bytes.</param>
    internal Jbig2ConformanceFile(byte[] file)
    {
        var flags = file[SignatureBytes];
        var start = SignatureBytes + 1 + ((flags & UnknownPagesFlag) != 0 ? 0 : PageCountBytes);
        if ((flags & SequentialFlag) != 0)
        {
            ReadSequential(file, start, _pages);
        }
        else
        {
            ReadRandomAccess(file, start, _pages);
        }
    }

    /// <summary>Gets the page numbers the file has segments for, not counting the globals.</summary>
    internal IEnumerable<int> PageNumbers => _pages.Keys.Where(static page => page != GlobalPage).Order();

    /// <summary>Gets the segments of a page.</summary>
    /// <param name="page">The page number, or 0 for the global segments.</param>
    /// <returns>The segments, or an empty array.</returns>
    internal byte[] Segments(int page) => _pages.TryGetValue(page, out var bytes) ? [.. bytes] : [];

    /// <summary>Reads the sequential organization: each header is followed by its data.</summary>
    /// <param name="file">The file.</param>
    /// <param name="start">The offset after the file header.</param>
    /// <param name="pages">Receives the segments of each page.</param>
    private static void ReadSequential(byte[] file, int start, Dictionary<int, List<byte>> pages)
    {
        var offset = start;
        while (offset < file.Length)
        {
            var header = ReadHeader(file, offset);
            var dataStart = offset + header.Length;
            var dataLength = header.DataLength == uint.MaxValue ? file.Length - dataStart : (int)header.DataLength;
            Add(pages, header.Page, file.AsSpan(offset, header.Length + dataLength));
            offset = dataStart + dataLength;
        }
    }

    /// <summary>Reads the random-access organization: all the headers, then all the data.</summary>
    /// <param name="file">The file.</param>
    /// <param name="start">The offset after the file header.</param>
    /// <param name="pages">Receives the segments of each page.</param>
    private static void ReadRandomAccess(byte[] file, int start, Dictionary<int, List<byte>> pages)
    {
        var headers = new List<SegmentHeader>();
        var offset = start;
        while (offset < file.Length)
        {
            var header = ReadHeader(file, offset);
            headers.Add(header with { Offset = offset });
            offset += header.Length;

            // The end-of-file segment, type 51, ends the list of headers.
            if (header.Type == EndOfFileType)
            {
                break;
            }
        }

        foreach (var header in headers)
        {
            var length = (int)header.DataLength;
            var bytes = new byte[header.Length + length];
            file.AsSpan(header.Offset, header.Length).CopyTo(bytes);
            file.AsSpan(offset, length).CopyTo(bytes.AsSpan(header.Length));
            Add(pages, header.Page, bytes);
            offset += length;
        }
    }

    /// <summary>Appends a segment to a page.</summary>
    /// <param name="pages">The segments of each page.</param>
    /// <param name="page">The page association.</param>
    /// <param name="segment">The header and data.</param>
    private static void Add(Dictionary<int, List<byte>> pages, int page, ReadOnlySpan<byte> segment)
    {
        ref var bytes = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(pages, page, out _);
        bytes ??= [];
        bytes.AddRange(segment);
    }

    /// <summary>Reads a segment header (T.88 section 7.2).</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The header's offset.</param>
    /// <returns>The header.</returns>
    private static SegmentHeader ReadHeader(byte[] file, int offset)
    {
        var number = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(offset));
        var flags = file[offset + IntBytes];
        var position = offset + IntBytes + 1;
        var count = file[position] >> CountShift;
        if (count == LongFormCount)
        {
            count = (int)(BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position)) & LongCountMask);
            position += IntBytes + ((count + ByteBits) / ByteBits);
        }
        else
        {
            position++;
        }

        var referenceBytes = 1;
        if (number > TwoByteNumbers)
        {
            referenceBytes = IntBytes;
        }
        else if (number > OneByteNumbers)
        {
            referenceBytes = TwoByteReference;
        }

        position += count * referenceBytes;
        var longPage = (flags & LongAssociationFlag) != 0;
        var page = (int)(longPage ? BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position)) : file[position]);
        position += longPage ? IntBytes : 1;
        var dataLength = BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(position));
        position += IntBytes;
        return new(position - offset, dataLength, page, flags & TypeMask, 0);
    }

    /// <summary>A parsed segment header.</summary>
    /// <param name="Length">The bytes of the header.</param>
    /// <param name="DataLength">The data length, or 0xFFFFFFFF when unknown.</param>
    /// <param name="Page">The page association.</param>
    /// <param name="Type">The segment type.</param>
    /// <param name="Offset">The header's offset in the file, for random access.</param>
    private readonly record struct SegmentHeader(int Length, uint DataLength, int Page, int Type, int Offset);
}

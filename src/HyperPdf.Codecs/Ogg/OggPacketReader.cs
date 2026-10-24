// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdf.Codecs.Containers;

namespace HyperPdf.Codecs.Ogg;

/// <summary>
/// Reads the packets of one logical stream out of an Ogg file. A page holds up to 255 segments of up to 255 bytes; a segment shorter
/// than 255 ends a packet, so a packet can span pages. The reader joins the pieces of a packet in a buffer and skips the pages of
/// other streams.
/// </summary>
[DebuggerDisplay("OggPacketReader: page at {_pageOffset}")]
internal sealed class OggPacketReader
{
    /// <summary>The bytes of a page header before its segment table.</summary>
    internal const int HeaderLength = 27;

    /// <summary>The index of the granule position in a page header.</summary>
    internal const int GranuleAt = 6;

    /// <summary>The index of the stream number in a page header.</summary>
    internal const int SerialAt = 14;

    /// <summary>The bytes of the marker that starts a page.</summary>
    private const int MarkerLength = 4;

    /// <summary>The value of a segment that continues its packet in the next segment.</summary>
    private const int FullSegment = 255;

    /// <summary>The flag on a page whose first packet continues one from the page before.</summary>
    private const int ContinuedFlag = 1;

    /// <summary>The flag on the first page of a stream.</summary>
    private const int FirstFlag = 2;

    /// <summary>The index of the page flags in the header.</summary>
    private const int FlagsIndex = 5;

    /// <summary>The index of the number of segments in the header.</summary>
    private const int SegmentCountIndex = 26;

    /// <summary>The file.</summary>
    private readonly FileWindow _window;

    /// <summary>The lacing values of the current page.</summary>
    private readonly byte[] _lacing = new byte[FullSegment];

    /// <summary>The packet being joined.</summary>
    private byte[] _buffer = new byte[ushort.MaxValue];

    /// <summary>The bytes in the buffer.</summary>
    private int _length;

    /// <summary>The file offset of the current page.</summary>
    private long _pageOffset;

    /// <summary>The file offset of the next page.</summary>
    private long _nextPage;

    /// <summary>The offset of the current page's body.</summary>
    private long _bodyOffset;

    /// <summary>The number of segments in the current page.</summary>
    private int _segments;

    /// <summary>The next segment of the current page to read.</summary>
    private int _segment;

    /// <summary>The bytes of the current page's body that have been used.</summary>
    private int _used;

    /// <summary>Whether a page is being read.</summary>
    private bool _hasPage;

    /// <summary>Initializes a new instance of the <see cref="OggPacketReader"/> class.</summary>
    /// <param name="window">The file.</param>
    /// <param name="start">The offset of the page to start at.</param>
    internal OggPacketReader(FileWindow window, long start)
    {
        _window = window;
        _nextPage = start;
    }

    /// <summary>Gets the stream number the reader follows, or 0 before the first page.</summary>
    internal uint Serial { get; private set; }

    /// <summary>Gets the granule position of the page the last packet came from.</summary>
    internal long PageGranule { get; private set; }

    /// <summary>Gets the granule position of the last page with a position that was passed.</summary>
    internal long PreviousGranule { get; private set; }

    /// <summary>Gets the file offset of the page after the one being read.</summary>
    internal long NextPageOffset => _nextPage;

    /// <summary>Gets the file offset of the page the last packet ended on.</summary>
    internal long PageOffset => _pageOffset;

    /// <summary>Gets the last packet.</summary>
    internal ReadOnlySpan<byte> Packet => _buffer.AsSpan(0, _length);

    /// <summary>Follows one stream, or the first stream seen.</summary>
    /// <param name="serial">The stream number.</param>
    internal void Follow(uint serial) => Serial = serial;

    /// <summary>Moves to a page, dropping any packet being joined.</summary>
    /// <param name="offset">The file offset of the page.</param>
    /// <param name="previousGranule">The granule position of the page before.</param>
    internal void Seek(long offset, long previousGranule)
    {
        _nextPage = offset;
        _hasPage = false;
        _length = 0;
        PreviousGranule = previousGranule;
    }

    /// <summary>Reads the next packet of the stream.</summary>
    /// <returns><see langword="false"/> at the end of the file.</returns>
    internal bool ReadPacket()
    {
        _length = 0;
        while (true)
        {
            if (!_hasPage && !LoadPage())
            {
                return false;
            }

            if (_segment >= _segments)
            {
                PreviousGranule = PageGranule >= 0 ? PageGranule : PreviousGranule;
                _hasPage = false;
                continue;
            }

            if (TakeSegments())
            {
                return true;
            }
        }
    }

    /// <summary>Reads the header of the page at an offset, whichever stream it belongs to.</summary>
    /// <param name="offset">The file offset of the page.</param>
    /// <param name="header">Receives the page header.</param>
    /// <returns><see langword="true"/> when a page starts at the offset.</returns>
    internal bool TryReadHeader(long offset, out PageHeader header)
    {
        header = default;
        var bytes = _window.Get(offset, HeaderLength);
        if (bytes.Length < HeaderLength || !bytes[..MarkerLength].SequenceEqual("OggS"u8))
        {
            return false;
        }

        var segments = bytes[SegmentCountIndex];
        var withTable = _window.Get(offset, HeaderLength + segments);
        if (withTable.Length < HeaderLength + segments)
        {
            return false;
        }

        var bodyLength = 0;
        for (var index = 0; index < segments; index++)
        {
            bodyLength += withTable[HeaderLength + index];
        }

        header = new(
            offset,
            HeaderLength + segments,
            bodyLength,
            withTable[FlagsIndex],
            BinaryPrimitives.ReadInt64LittleEndian(withTable[GranuleAt..]),
            BinaryPrimitives.ReadUInt32LittleEndian(withTable[SerialAt..]),
            segments);
        return true;
    }

    /// <summary>Loads the next page of the followed stream.</summary>
    /// <returns><see langword="false"/> at the end of the file.</returns>
    private bool LoadPage()
    {
        while (TryReadHeader(_nextPage, out var header))
        {
            _nextPage = header.Offset + header.HeaderLength + header.BodyLength;
            if (Serial == 0 && (header.Flags & FirstFlag) != 0)
            {
                Serial = header.Serial;
            }

            if (header.Serial != Serial)
            {
                continue;
            }

            _pageOffset = header.Offset;
            _bodyOffset = header.Offset + header.HeaderLength;
            _segments = header.Segments;
            _segment = 0;
            _used = 0;
            PageGranule = header.Granule;
            var table = _window.Get(header.Offset + HeaderLength, header.Segments);
            table[..header.Segments].CopyTo(_lacing);
            _hasPage = true;
            if ((header.Flags & ContinuedFlag) != 0 && _length == 0)
            {
                SkipContinuation();
            }

            return true;
        }

        return false;
    }

    /// <summary>Skips the start of a page that continues a packet that was not joined, as after a seek.</summary>
    private void SkipContinuation()
    {
        while (_segment < _segments)
        {
            var size = _lacing[_segment];
            _used += size;
            _segment++;
            if (size < FullSegment)
            {
                return;
            }
        }
    }

    /// <summary>Joins segments into a packet until one ends it or the page runs out.</summary>
    /// <returns><see langword="true"/> when a packet is complete.</returns>
    private bool TakeSegments()
    {
        while (_segment < _segments)
        {
            var size = _lacing[_segment];
            if (_length + size > _buffer.Length)
            {
                Array.Resize(ref _buffer, Math.Max(_buffer.Length << 1, _length + size));
            }

            var body = _window.Get(_bodyOffset + _used, size);
            body[..size].CopyTo(_buffer.AsSpan(_length));
            _length += size;
            _used += size;
            _segment++;
            if (size < FullSegment)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The fields of a page header the reader needs.</summary>
    /// <param name="Offset">The file offset of the page.</param>
    /// <param name="HeaderLength">The bytes of the header including its segment table.</param>
    /// <param name="BodyLength">The bytes of the body.</param>
    /// <param name="Flags">The page flags.</param>
    /// <param name="Granule">The granule position, or -1.</param>
    /// <param name="Serial">The number of the stream.</param>
    /// <param name="Segments">The number of segments.</param>
    internal readonly record struct PageHeader(long Offset, int HeaderLength, int BodyLength, int Flags, long Granule, uint Serial, int Segments);
}

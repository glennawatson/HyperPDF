// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The structure of a JPEG 2000 codestream (ISO 15444-1 annex A): the image geometry, the main header parameters and
/// where each tile-part sits. Packed PPM headers are split into one run per tile-part.
/// </summary>
[DebuggerDisplay("JpxCodestream: {TileParts.Count} tile-parts")]
internal sealed class JpxCodestream
{
    /// <summary>The bytes of an SOT segment, marker included.</summary>
    private const int TileStartBytes = 12;

    /// <summary>The bytes of a marker and its length field.</summary>
    private const int MarkerHeaderBytes = 4;

    /// <summary>The offset of the tile index in an SOT segment.</summary>
    private const int TileIndexOffset = 4;

    /// <summary>The offset of the tile-part length in an SOT segment.</summary>
    private const int TileLengthOffset = 6;

    /// <summary>The bytes of the length before each tile-part's packed headers in PPM data.</summary>
    private const int PackedLengthBytes = 4;

    /// <summary>The high byte every marker code has.</summary>
    private const int MarkerPrefix = 0xFF00;

    /// <summary>The first reserved marker that has no segment.</summary>
    private const int FirstBareMarker = 0xFF30;

    /// <summary>The last reserved marker that has no segment.</summary>
    private const int LastBareMarker = 0xFF3F;

    /// <summary>Initializes a new instance of the <see cref="JpxCodestream"/> class.</summary>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="main">The main header parameters.</param>
    /// <param name="tileParts">The tile-parts in codestream order.</param>
    private JpxCodestream(JpxGeometry geometry, JpxHeaderState main, List<JpxTilePart> tileParts)
    {
        Geometry = geometry;
        Main = main;
        TileParts = tileParts;
    }

    /// <summary>Gets the image geometry.</summary>
    internal JpxGeometry Geometry { get; }

    /// <summary>Gets the main header parameters.</summary>
    internal JpxHeaderState Main { get; }

    /// <summary>Gets the tile-parts in codestream order.</summary>
    internal List<JpxTilePart> TileParts { get; }

    /// <summary>Gets the parts of JPEG 2000 the codestream declares, from SIZ and any CAP marker.</summary>
    internal JpxCapabilities Capabilities { get; private init; }

    /// <summary>Gets the PPM packed headers joined together, or <see langword="null"/>.</summary>
    internal byte[]? PackedMain { get; private set; }

    /// <summary>Gets each tile-part's run of <see cref="PackedMain"/>, in tile-part order, or <see langword="null"/>.</summary>
    internal JpxDataRange[]? PackedRanges { get; private set; }

    /// <summary>Reads the main header and finds the tile-parts.</summary>
    /// <param name="data">The codestream.</param>
    /// <returns>The structure, or <see langword="null"/> when the main header is missing or invalid.</returns>
    internal static JpxCodestream? Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < MarkerHeaderBytes || BinaryPrimitives.ReadUInt16BigEndian(data) != JpxMarkers.StartOfCodestream
            || BinaryPrimitives.ReadUInt16BigEndian(data[JpxMarkers.MarkerBytes..]) != JpxMarkers.ImageSize)
        {
            return null;
        }

        var position = JpxMarkers.MarkerBytes;
        if (!TryReadSegment(data, ref position, out var siz) || JpxGeometry.Read(siz) is not { } geometry)
        {
            return null;
        }

        var main = new JpxHeaderState(geometry.Components.Length);
        var capabilities = new JpxCapabilities(BinaryPrimitives.ReadUInt16BigEndian(siz), 0, 0);
        if (!ReadHeader(data, ref position, main, ref capabilities) || main.Coding is null || main.Style is null || main.Quantization is null)
        {
            return null;
        }

        var codestream = new JpxCodestream(geometry, main, FindTileParts(data, position, geometry)) { Capabilities = capabilities };
        codestream.SplitPacked(data);
        return codestream;
    }

    /// <summary>Reads the markers of a tile-part header into a tile's parameters.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="header">The header bytes, between SOT and SOD.</param>
    /// <param name="state">The tile's parameters.</param>
    /// <returns><see langword="false"/> when a needed marker is invalid.</returns>
    internal static bool ReadTileHeader(ReadOnlySpan<byte> data, in JpxDataRange header, JpxHeaderState state)
    {
        state.BeginTilePart();
        var position = header.Offset;
        var end = header.End;
        while (position + MarkerHeaderBytes <= end)
        {
            var marker = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
            var segmentStart = position + MarkerHeaderBytes;
            if (!TryReadSegment(data[..end], ref position, out var segment) || !state.Read(marker, segment, segmentStart))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads one marker segment and moves past it.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="position">The position of the marker; moved past the segment.</param>
    /// <param name="segment">Receives the segment after its length field.</param>
    /// <returns><see langword="false"/> when the segment does not fit or is not a marker.</returns>
    private static bool TryReadSegment(ReadOnlySpan<byte> data, ref int position, out ReadOnlySpan<byte> segment)
    {
        segment = default;
        if (position + MarkerHeaderBytes > data.Length || (BinaryPrimitives.ReadUInt16BigEndian(data[position..]) & MarkerPrefix) != MarkerPrefix)
        {
            return false;
        }

        // The markers 0xFF30 to 0xFF3F are reserved and have no segment, so there is no length field to read (A.1, table A.2).
        var code = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
        if (code is >= FirstBareMarker and <= LastBareMarker)
        {
            position += JpxMarkers.MarkerBytes;
            return true;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(data[(position + JpxMarkers.MarkerBytes)..]);
        if (length < JpxMarkers.MarkerBytes || position + JpxMarkers.MarkerBytes + length > data.Length)
        {
            return false;
        }

        segment = data.Slice(position + MarkerHeaderBytes, length - JpxMarkers.MarkerBytes);
        position += JpxMarkers.MarkerBytes + length;
        return true;
    }

    /// <summary>Reads the main header markers up to the first tile-part.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="position">The position after SIZ; moved to the first SOT.</param>
    /// <param name="main">The main header parameters.</param>
    /// <param name="capabilities">The capabilities, updated from a CAP marker.</param>
    /// <returns><see langword="false"/> when a needed marker is invalid.</returns>
    private static bool ReadHeader(ReadOnlySpan<byte> data, ref int position, JpxHeaderState main, ref JpxCapabilities capabilities)
    {
        while (position + MarkerHeaderBytes <= data.Length)
        {
            var marker = BinaryPrimitives.ReadUInt16BigEndian(data[position..]);
            if (marker == JpxMarkers.StartOfTile)
            {
                return true;
            }

            var segmentStart = position + MarkerHeaderBytes;
            if (!TryReadSegment(data, ref position, out var segment) || !main.Read(marker, segment, segmentStart))
            {
                return false;
            }

            if (marker == JpxMarkers.Capabilities)
            {
                capabilities = JpxCapabilities.Read(capabilities.Rsiz, segment);
            }
        }

        return true;
    }

    /// <summary>Finds every tile-part from the first SOT marker on; a damaged tail ends the list.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="position">The position of the first SOT marker.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <returns>The tile-parts in codestream order.</returns>
    private static List<JpxTilePart> FindTileParts(ReadOnlySpan<byte> data, int position, JpxGeometry geometry)
    {
        var parts = new List<JpxTilePart>();
        var tiles = geometry.TilesAcross * geometry.TilesDown;
        while (position + TileStartBytes <= data.Length && BinaryPrimitives.ReadUInt16BigEndian(data[position..]) == JpxMarkers.StartOfTile)
        {
            var tile = BinaryPrimitives.ReadUInt16BigEndian(data[(position + TileIndexOffset)..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(data[(position + TileLengthOffset)..]);
            var headerStart = position + TileStartBytes;
            var dataMarker = FindStartOfData(data, headerStart);
            if (dataMarker < 0)
            {
                break;
            }

            var toEnd = length == 0 || length > (uint)(data.Length - position);
            var end = toEnd ? DataEnd(data) : position + (int)length;
            var dataStart = dataMarker + JpxMarkers.MarkerBytes;
            if (tile < tiles && end >= dataStart)
            {
                parts.Add(new(tile, new(headerStart, dataMarker - headerStart), new(dataStart, end - dataStart)));
            }

            if (toEnd)
            {
                break;
            }

            position = end;
        }

        return parts;
    }

    /// <summary>Finds the SOD marker that ends a tile-part header.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="position">The first marker of the header.</param>
    /// <returns>The position of SOD, or -1.</returns>
    private static int FindStartOfData(ReadOnlySpan<byte> data, int position)
    {
        while (position + JpxMarkers.MarkerBytes <= data.Length)
        {
            if (BinaryPrimitives.ReadUInt16BigEndian(data[position..]) == JpxMarkers.StartOfData)
            {
                return position;
            }

            if (!TryReadSegment(data, ref position, out _))
            {
                return -1;
            }
        }

        return -1;
    }

    /// <summary>Gets the end of the last tile-part's data: the end of the codestream, before any EOC marker.</summary>
    /// <param name="data">The codestream.</param>
    /// <returns>The end position.</returns>
    private static int DataEnd(ReadOnlySpan<byte> data)
    {
        var end = data.Length;
        return end >= JpxMarkers.MarkerBytes && BinaryPrimitives.ReadUInt16BigEndian(data[(end - JpxMarkers.MarkerBytes)..]) == JpxMarkers.EndOfCodestream
            ? end - JpxMarkers.MarkerBytes
            : end;
    }

    /// <summary>Joins the PPM segments and splits them into one run of packed headers per tile-part.</summary>
    /// <param name="data">The codestream.</param>
    private void SplitPacked(ReadOnlySpan<byte> data)
    {
        if (Main.PackedHeaders is not { Count: > 0 } pieces)
        {
            return;
        }

        var total = 0;
        foreach (var piece in pieces)
        {
            total += piece.Length;
        }

        var joined = new byte[total];
        var written = 0;
        foreach (var piece in pieces)
        {
            data.Slice(piece.Offset, piece.Length).CopyTo(joined.AsSpan(written));
            written += piece.Length;
        }

        var ranges = new JpxDataRange[TileParts.Count];
        var position = 0;
        for (var i = 0; i < ranges.Length && position + PackedLengthBytes <= total; i++)
        {
            var length = (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(joined.AsSpan(position)), (uint)(total - position - PackedLengthBytes));
            ranges[i] = new(position + PackedLengthBytes, length);
            position += PackedLengthBytes + length;
        }

        PackedMain = joined;
        PackedRanges = ranges;
    }
}

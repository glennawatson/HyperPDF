// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Reads packets (ISO 15444-1 B.9 and B.10): each packet header says which code-blocks of one precinct gain coding
/// passes and how many bytes, and the packet body holds those bytes. The bytes stay in the tile data; code-blocks keep
/// chunk and segment lists that point into it. Packed headers from PPM or PPT markers are read from their own stream.
/// </summary>
[DebuggerDisplay("JpxPacketDecoder: body at {_bodyPosition}")]
internal sealed class JpxPacketDecoder
{
    /// <summary>The initial length indicator bits, Lblock.</summary>
    private const int InitialLengthBits = 3;

    /// <summary>The most zero bit-planes read before the tag tree counts as damaged.</summary>
    private const int MaxZeroPlanes = 74;

    /// <summary>The longest length increment comma code accepted.</summary>
    private const int MaxIncrement = 32;

    /// <summary>The widest segment length field.</summary>
    private const int MaxLengthBits = 32;

    /// <summary>The passes of the first bypass segment: the cleanup pass and three full bit-planes.</summary>
    private const int FirstBypassPasses = 10;

    /// <summary>The passes of a raw bypass segment: one significance and one refinement pass.</summary>
    private const int RawBypassPasses = 2;

    /// <summary>The passes of an HT refinement segment: SigProp and MagRef.</summary>
    private const int HtRefinementPasses = 2;

    /// <summary>The most passes of a segment without mode switches (ISO 15444-1 B.10.6).</summary>
    private const int MaxPasses = 109;

    /// <summary>The passes signalled by the two-bit code before the five-bit escape.</summary>
    private const int ShortPassBase = 3;

    /// <summary>The passes signalled before the seven-bit escape.</summary>
    private const int MediumPassBase = 6;

    /// <summary>The passes signalled after the seven-bit escape.</summary>
    private const int LongPassBase = 37;

    /// <summary>The bits of the short pass code.</summary>
    private const int ShortPassBits = 2;

    /// <summary>The bits of the medium pass code.</summary>
    private const int MediumPassBits = 5;

    /// <summary>The bits of the long pass code.</summary>
    private const int LongPassBits = 7;

    /// <summary>The short pass code that escapes to the medium code.</summary>
    private const int ShortEscape = 3;

    /// <summary>The medium pass code that escapes to the long code.</summary>
    private const int MediumEscape = 31;

    /// <summary>The second byte of the SOP marker.</summary>
    private const int SopLow = 0x91;

    /// <summary>The second byte of the EPH marker.</summary>
    private const int EphLow = 0x92;

    /// <summary>The first byte of every marker.</summary>
    private const int MarkerHigh = 0xFF;

    /// <summary>The tile being decoded.</summary>
    private JpxTile _tile = null!;

    /// <summary>The tile's coding parameters.</summary>
    private JpxTileCoding _coding = null!;

    /// <summary>The tile's packet data.</summary>
    private byte[] _data = [];

    /// <summary>The end of the packet data.</summary>
    private int _bodyEnd;

    /// <summary>The position in the packet data.</summary>
    private int _bodyPosition;

    /// <summary>The packed packet headers, or <see langword="null"/> when the headers are in the packet data.</summary>
    private byte[]? _packed;

    /// <summary>The end of the packed headers.</summary>
    private int _packedEnd;

    /// <summary>The position in the packed headers.</summary>
    private int _packedPosition;

    /// <summary>Forgets the tile and its data, so an idle reader keeps nothing alive.</summary>
    internal void Clear()
    {
        _tile = null!;
        _coding = null!;
        _data = [];
        _packed = null;
        _bodyEnd = 0;
        _bodyPosition = 0;
        _packedEnd = 0;
        _packedPosition = 0;
    }

    /// <summary>Starts a tile.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="coding">The tile's coding parameters.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="length">The bytes of packet data.</param>
    /// <param name="packed">The packed packet headers, or <see langword="null"/>.</param>
    /// <param name="packedLength">The bytes of packed headers.</param>
    internal void Start(JpxTile tile, JpxTileCoding coding, byte[] data, int length, byte[]? packed, int packedLength)
    {
        _tile = tile;
        _coding = coding;
        _data = data;
        _bodyEnd = length;
        _bodyPosition = 0;
        _packed = packed;
        _packedEnd = packedLength;
        _packedPosition = 0;
    }

    /// <summary>Reads one packet.</summary>
    /// <param name="layer">The quality layer.</param>
    /// <param name="resolution">The resolution index.</param>
    /// <param name="component">The component index.</param>
    /// <param name="precinct">The precinct index.</param>
    /// <returns><see langword="false"/> when the data has run out or is damaged, so later packets hold nothing.</returns>
    internal bool Decode(int layer, int resolution, int component, int precinct)
    {
        var layout = _tile.Resolutions[_tile.Components[component].FirstResolution + resolution];
        if (precinct >= layout.PrecinctCount)
        {
            return true;
        }

        if (HeaderExhausted())
        {
            return false;
        }

        SkipStartOfPacket();
        var header = _packed is null ? _data.AsSpan(_bodyPosition, _bodyEnd - _bodyPosition) : _packed.AsSpan(_packedPosition, _packedEnd - _packedPosition);
        var reader = new JpxBitReader(header);
        var present = reader.ReadBit() != 0;
        var valid = !present || ReadHeader(layout, precinct, layer, component, ref reader);
        reader.Align();
        if (!valid || reader.Overran)
        {
            return false;
        }

        AdvanceHeader(reader.Position);
        if (present)
        {
            ReadBodies(layout, precinct);
        }

        return true;
    }

    /// <summary>Reads the number of coding passes (ISO 15444-1 table B.4).</summary>
    /// <param name="reader">The header bits.</param>
    /// <returns>The number of passes, 1 to 164.</returns>
    private static int ReadPassCount(ref JpxBitReader reader)
    {
        if (reader.ReadBit() == 0)
        {
            return 1;
        }

        if (reader.ReadBit() == 0)
        {
            return RawBypassPasses;
        }

        var code = (int)reader.ReadBits(ShortPassBits);
        if (code != ShortEscape)
        {
            return ShortPassBase + code;
        }

        code = (int)reader.ReadBits(MediumPassBits);
        return code != MediumEscape ? MediumPassBase + code : LongPassBase + (int)reader.ReadBits(LongPassBits);
    }

    /// <summary>Reads a comma code: the count of one bits before a zero.</summary>
    /// <param name="reader">The header bits.</param>
    /// <returns>The count, or -1 when implausibly long.</returns>
    private static int ReadCommaCode(ref JpxBitReader reader)
    {
        var count = 0;
        while (reader.ReadBit() != 0)
        {
            count++;
            if (count > MaxIncrement)
            {
                return -1;
            }
        }

        return count;
    }

    /// <summary>Gets the passes of the bypass segment that follows another: raw segments alternate with one-pass cleanups.</summary>
    /// <param name="previous">The previous segment's most passes.</param>
    /// <returns>The next segment's most passes.</returns>
    private static int BypassFollower(int previous) => previous is 1 or FirstBypassPasses ? RawBypassPasses : 1;

    /// <summary>Determines whether the packet headers have run out.</summary>
    /// <returns><see langword="true"/> when no header bytes remain.</returns>
    private bool HeaderExhausted() => _packed is null ? _bodyPosition >= _bodyEnd : _packedPosition >= _packedEnd;

    /// <summary>Skips an SOP marker segment at the current packet, when the tile allows them.</summary>
    private void SkipStartOfPacket()
    {
        if (_coding.StartOfPacketMarkers && _bodyEnd - _bodyPosition >= JpxMarkers.StartOfPacketBytes
            && _data[_bodyPosition] == MarkerHigh && _data[_bodyPosition + 1] == SopLow)
        {
            _bodyPosition += JpxMarkers.StartOfPacketBytes;
        }
    }

    /// <summary>Moves past a packet header and any EPH marker after it.</summary>
    /// <param name="length">The header bytes read.</param>
    private void AdvanceHeader(int length)
    {
        if (_packed is null)
        {
            _bodyPosition += length;
            _bodyPosition += EndOfHeaderBytes(_data, _bodyPosition, _bodyEnd);
        }
        else
        {
            _packedPosition += length;
            _packedPosition += EndOfHeaderBytes(_packed, _packedPosition, _packedEnd);
        }
    }

    /// <summary>Gets the bytes of an EPH marker at a position, when the tile uses them.</summary>
    /// <param name="data">The header bytes.</param>
    /// <param name="position">The position after the header.</param>
    /// <param name="end">The end of the header bytes.</param>
    /// <returns>Two when an EPH marker is there, otherwise zero.</returns>
    private int EndOfHeaderBytes(byte[] data, int position, int end) =>
        _coding.EndOfHeaderMarkers && end - position >= JpxMarkers.MarkerBytes && data[position] == MarkerHigh && data[position + 1] == EphLow
            ? JpxMarkers.MarkerBytes
            : 0;

    /// <summary>Reads the code-block entries of a non-empty packet header.</summary>
    /// <param name="layout">The resolution.</param>
    /// <param name="precinct">The precinct index.</param>
    /// <param name="layer">The quality layer.</param>
    /// <param name="component">The component index.</param>
    /// <param name="reader">The header bits.</param>
    /// <returns><see langword="false"/> when the header is damaged.</returns>
    private bool ReadHeader(in JpxResolutionLayout layout, int precinct, int layer, int component, ref JpxBitReader reader)
    {
        var style = _tile.Components[component].BlockStyle;
        for (var b = 0; b < layout.BandCount; b++)
        {
            var band = _tile.Bands[layout.FirstBand + b];
            if (band.Area.IsEmpty)
            {
                continue;
            }

            var cell = _tile.Precincts[band.FirstPrecinct + precinct];
            for (var k = 0; k < cell.BlockCount; k++)
            {
                if (!ReadBlockHeader(cell, k, band.Magnitude, layer, style, ref reader))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Reads one code-block's entry in a packet header.</summary>
    /// <param name="cell">The precinct.</param>
    /// <param name="index">The code-block index within the precinct.</param>
    /// <param name="magnitude">The sub-band's magnitude bits.</param>
    /// <param name="layer">The quality layer.</param>
    /// <param name="style">The code-block mode switches.</param>
    /// <param name="reader">The header bits.</param>
    /// <returns><see langword="false"/> when the header is damaged.</returns>
    private bool ReadBlockHeader(in JpxPrecinctLayout cell, int index, int magnitude, int layer, JpxBlockStyle style, ref JpxBitReader reader)
    {
        ref var block = ref _tile.Blocks[cell.FirstBlock + index];
        var included = block.Included
            ? reader.ReadBit() != 0
            : JpxTagTree.Decode(_tile.Nodes, cell.InclusionTree, cell.BlocksWide, cell.BlocksHigh, index, layer + 1, ref reader);
        if (!included)
        {
            block.NewPasses = 0;
            return true;
        }

        if (!block.Included && !ReadZeroPlanes(ref block, cell, index, magnitude, ref reader))
        {
            return false;
        }

        var passes = ReadPassCount(ref reader);
        var increment = ReadCommaCode(ref reader);
        if (increment < 0)
        {
            return false;
        }

        block.LengthBits += increment;
        block.NewPasses = passes;
        return ReadSegmentLengths(ref block, passes, style, ref reader);
    }

    /// <summary>Reads a newly included code-block's zero bit-planes.</summary>
    /// <param name="block">The code-block.</param>
    /// <param name="cell">The precinct.</param>
    /// <param name="index">The code-block index within the precinct.</param>
    /// <param name="magnitude">The sub-band's magnitude bits.</param>
    /// <param name="reader">The header bits.</param>
    /// <returns><see langword="false"/> when the tag tree is damaged.</returns>
    private bool ReadZeroPlanes(ref JpxCodeBlock block, in JpxPrecinctLayout cell, int index, int magnitude, ref JpxBitReader reader)
    {
        var threshold = 0;
        while (!JpxTagTree.Decode(_tile.Nodes, cell.ZeroPlaneTree, cell.BlocksWide, cell.BlocksHigh, index, threshold, ref reader))
        {
            threshold++;
            if (threshold > MaxZeroPlanes)
            {
                return false;
            }
        }

        // The tag tree is read until its value is known; the loop ends one past the number of missing bit-planes P (B.10.5), so P = threshold - 1.
        block.BitPlanes = magnitude + 1 - threshold;
        block.LengthBits = InitialLengthBits;
        block.Included = true;
        return true;
    }

    /// <summary>Splits a code-block's new passes into codeword segments and reads each one's length.</summary>
    /// <param name="block">The code-block.</param>
    /// <param name="passes">The new passes.</param>
    /// <param name="style">The code-block mode switches.</param>
    /// <param name="reader">The header bits.</param>
    /// <returns><see langword="false"/> when a length field is too wide.</returns>
    private bool ReadSegmentLengths(ref JpxCodeBlock block, int passes, JpxBlockStyle style, ref JpxBitReader reader)
    {
        var segment = block.LastSegment;
        if (segment < 0 || _tile.Segments[segment].Passes == _tile.Segments[segment].MaxPasses)
        {
            segment = AddSegment(ref block, style);
        }

        block.PacketSegment = segment;
        var remaining = passes;
        while (true)
        {
            ref var current = ref _tile.Segments[segment];
            var count = Math.Min(current.MaxPasses - current.Passes, remaining);
            var bits = block.LengthBits + BitOperations.Log2((uint)count);
            if (bits > MaxLengthBits)
            {
                return false;
            }

            var length = reader.ReadBits(bits);
            current.NewPasses = count;
            current.NewLength = (int)Math.Min(length, int.MaxValue);
            remaining -= count;
            if (remaining <= 0)
            {
                return true;
            }

            segment = AddSegment(ref block, style);
        }
    }

    /// <summary>Adds a codeword segment to a code-block.</summary>
    /// <param name="block">The code-block.</param>
    /// <param name="style">The code-block mode switches.</param>
    /// <returns>The segment index.</returns>
    private int AddSegment(ref JpxCodeBlock block, JpxBlockStyle style)
    {
        var previous = block.LastSegment;
        var maxPasses = MaxPasses;
        if ((style & JpxBlockStyle.HighThroughput) != 0)
        {
            // HT sets alternate a one-pass cleanup segment with a SigProp and MagRef refinement segment.
            maxPasses = previous < 0 || _tile.Segments[previous].MaxPasses == HtRefinementPasses ? 1 : HtRefinementPasses;
        }
        else if ((style & JpxBlockStyle.TerminateAll) != 0)
        {
            maxPasses = 1;
        }
        else if ((style & JpxBlockStyle.Bypass) != 0)
        {
            maxPasses = previous < 0 ? FirstBypassPasses : BypassFollower(_tile.Segments[previous].MaxPasses);
        }

        var index = _tile.Segments.Add(new() { Next = -1, MaxPasses = maxPasses });
        if (previous < 0)
        {
            block.FirstSegment = index;
        }
        else
        {
            _tile.Segments[previous].Next = index;
        }

        block.LastSegment = index;
        return index;
    }

    /// <summary>Reads the packet body: each included code-block's new bytes, segment by segment.</summary>
    /// <param name="layout">The resolution.</param>
    /// <param name="precinct">The precinct index.</param>
    private void ReadBodies(in JpxResolutionLayout layout, int precinct)
    {
        var truncated = false;
        for (var b = 0; b < layout.BandCount; b++)
        {
            var band = _tile.Bands[layout.FirstBand + b];
            if (band.Area.IsEmpty)
            {
                continue;
            }

            var cell = _tile.Precincts[band.FirstPrecinct + precinct];
            for (var k = 0; k < cell.BlockCount; k++)
            {
                ref var block = ref _tile.Blocks[cell.FirstBlock + k];
                if (block.NewPasses > 0)
                {
                    truncated = !ReadBody(ref block, truncated);
                }
            }
        }

        if (truncated)
        {
            // A body longer than the data left means the rest of the tile is lost.
            _bodyPosition = _bodyEnd;
        }
    }

    /// <summary>Reads one code-block's new bytes.</summary>
    /// <param name="block">The code-block.</param>
    /// <param name="truncated">Whether an earlier body of this packet ran past the data.</param>
    /// <returns><see langword="false"/> when the data ran out.</returns>
    private bool ReadBody(ref JpxCodeBlock block, bool truncated)
    {
        if (truncated || block.Corrupted)
        {
            block.Corrupted = true;
            block.NewPasses = 0;
            return !truncated;
        }

        var segment = block.PacketSegment;
        while (segment >= 0 && block.NewPasses > 0)
        {
            ref var current = ref _tile.Segments[segment];
            if (current.NewLength > _bodyEnd - _bodyPosition)
            {
                block.Corrupted = true;
                block.NewPasses = 0;
                return false;
            }

            AddChunk(ref block, current.NewLength);
            current.Length += current.NewLength;
            current.Passes += current.NewPasses;
            block.NewPasses -= current.NewPasses;
            current.NewPasses = 0;
            current.NewLength = 0;
            segment = current.Next;
        }

        block.NewPasses = 0;
        return true;
    }

    /// <summary>Records the next bytes of the packet data as part of a code-block.</summary>
    /// <param name="block">The code-block.</param>
    /// <param name="length">The bytes.</param>
    private void AddChunk(ref JpxCodeBlock block, int length)
    {
        if (length == 0)
        {
            return;
        }

        var offset = _bodyPosition;
        _bodyPosition += length;
        block.DataLength += length;
        if (block.LastChunk >= 0)
        {
            ref var last = ref _tile.Chunks[block.LastChunk];
            if (last.Offset + last.Length == offset)
            {
                last.Length += length;
                return;
            }
        }

        var index = _tile.Chunks.Add(new() { Next = -1, Offset = offset, Length = length });
        if (block.LastChunk >= 0)
        {
            _tile.Chunks[block.LastChunk].Next = index;
        }
        else
        {
            block.FirstChunk = index;
        }

        block.LastChunk = index;
    }
}

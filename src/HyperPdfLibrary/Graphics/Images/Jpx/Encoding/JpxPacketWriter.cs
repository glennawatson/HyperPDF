// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Writes the packets of one tile (ISO 15444-1 B.9 to B.11): the packet header says which code-blocks of a precinct send
/// passes in a layer and how many bytes, and the body holds those bytes. The passes of each block are spread over the layers
/// by bit-plane: layer k holds the passes down to a bit-plane that falls evenly from the tile's top plane to plane zero in the
/// last layer, so every layer adds about the same detail to every sub-band.
/// </summary>
internal sealed class JpxPacketWriter : IDisposable
{
    /// <summary>The initial length indicator bits, Lblock.</summary>
    private const int InitialLengthBits = 3;

    /// <summary>The passes the two-bit code covers.</summary>
    private const int ShortBase = 3;

    /// <summary>The passes the five-bit code covers.</summary>
    private const int MediumBase = 6;

    /// <summary>The passes the seven-bit code covers.</summary>
    private const int LongBase = 37;

    /// <summary>The bits of the short pass code.</summary>
    private const int ShortBits = 2;

    /// <summary>The bits of the medium pass code.</summary>
    private const int MediumBits = 5;

    /// <summary>The bits of the long pass code.</summary>
    private const int LongBits = 7;

    /// <summary>The prefix of the short pass code, 11.</summary>
    private const int ShortPrefix = 0b11;

    /// <summary>The prefix of the medium pass code, 1111.</summary>
    private const int MediumPrefix = 0b1111;

    /// <summary>The bits of the medium pass code's prefix.</summary>
    private const int MediumPrefixBits = 4;

    /// <summary>The prefix of the long pass code, 1111 11111.</summary>
    private const int LongPrefix = 0b1_1111_1111;

    /// <summary>The bits of the long pass code's prefix.</summary>
    private const int LongPrefixBits = 9;

    /// <summary>The code for two passes, 10.</summary>
    private const int TwoPassCode = 0b10;

    /// <summary>The bits of the code for two passes.</summary>
    private const int TwoPassBits = 2;

    /// <summary>The passes coded as 10.</summary>
    private const int TwoPasses = 2;

    /// <summary>The passes a cleanup-only first bit-plane and one more plane make: the layer arithmetic below counts per plane.</summary>
    private const int PassesPerPlane = 3;

    /// <summary>The bytes of the SOP marker segment.</summary>
    private const int StartOfPacketBytes = 6;

    /// <summary>The marker byte prefix.</summary>
    private const byte MarkerHigh = 0xFF;

    /// <summary>The second byte of SOP.</summary>
    private const byte SopLow = 0x91;

    /// <summary>The second byte of EPH.</summary>
    private const byte EphLow = 0x92;

    /// <summary>The length field of an SOP segment.</summary>
    private const byte SopLength = 4;

    /// <summary>The offset of the packet number in an SOP segment.</summary>
    private const int SequenceOffset = 4;

    /// <summary>The mask of the 16-bit packet number an SOP segment holds.</summary>
    private const int PacketNumberMask = 0xFFFF;

    /// <summary>The coding parameters.</summary>
    private readonly JpxEncodeSettings _settings;

    /// <summary>The tag trees of the tile.</summary>
    private readonly JpxTagTrees _trees;

    /// <summary>The packet header bits.</summary>
    private readonly JpxBitWriter _header = new();

    /// <summary>For every block and layer, the passes sent up to and including that layer.</summary>
    private readonly JpxList<int> _layerEnds;

    /// <summary>For every block, the passes sent so far.</summary>
    private readonly JpxList<int> _sent;

    /// <summary>For every block, the length indicator bits Lblock.</summary>
    private readonly JpxList<int> _lengthBits;

    /// <summary>For every block, 1 once a packet has included it.</summary>
    private readonly JpxList<byte> _included;

    /// <summary>For every precinct, the index of its inclusion tag tree, or -1 without blocks.</summary>
    private readonly JpxList<int> _inclusionTrees;

    /// <summary>For every precinct, the index of its zero bit-plane tag tree, or -1 without blocks.</summary>
    private readonly JpxList<int> _zeroTrees;

    /// <summary>The pieces of the block being written.</summary>
    private readonly JpxList<Piece> _pieces;

    /// <summary>The body slices of the packet being written.</summary>
    private readonly JpxList<Piece> _bodies;

    /// <summary>The tile layout.</summary>
    private readonly JpxTile _tile;

    /// <summary>The coded blocks.</summary>
    private readonly JpxTileCode _code;

    /// <summary>The packets written so far, for SOP numbering.</summary>
    private int _packetNumber;

    /// <summary>Initializes a new instance of the <see cref="JpxPacketWriter"/> class, which takes ownership of the tile layout and the coded blocks.</summary>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="tile">The tile layout, rebuilt for each tile.</param>
    /// <param name="code">The coded blocks of the tile.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxPacketWriter(JpxEncodeSettings settings, JpxTile tile, JpxTileCode code, ScratchPools scratchPools)
    {
        _settings = settings;
        _tile = tile;
        _code = code;
        _trees = new(scratchPools);
        _layerEnds = new(scratchPools);
        _sent = new(scratchPools);
        _lengthBits = new(scratchPools);
        _included = new(scratchPools);
        _inclusionTrees = new(scratchPools);
        _zeroTrees = new(scratchPools);
        _pieces = new(scratchPools);
        _bodies = new(scratchPools);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _tile.Dispose();
        _code.Dispose();
        _trees.Dispose();
        _header.Dispose();
        _layerEnds.Dispose();
        _sent.Dispose();
        _lengthBits.Dispose();
        _included.Dispose();
        _inclusionTrees.Dispose();
        _zeroTrees.Dispose();
        _pieces.Dispose();
        _bodies.Dispose();
    }

    /// <summary>Prepares for a tile whose layout and coded blocks are ready: spreads each block's passes over the layers and builds the tag trees.</summary>
    /// <returns><see langword="false"/> when a block has more bit-planes than its sub-band allows, so the file needs more guard bits.</returns>
    internal bool Begin()
    {
        _packetNumber = 0;
        var blocks = _tile.Blocks.Count;
        _sent.Clear();
        _lengthBits.Clear();
        _included.Clear();
        _layerEnds.Clear();
        _ = _sent.AddDefault(blocks);
        _ = _included.AddDefault(blocks);
        _ = _lengthBits.AddDefault(blocks);
        _lengthBits.AsSpan().Fill(InitialLengthBits);
        _ = _layerEnds.AddDefault(blocks * _settings.Layers);
        AssignLayers();
        return BuildTrees();
    }

    /// <summary>Writes one packet.</summary>
    /// <param name="key">The packet.</param>
    /// <param name="output">The tile-part being written.</param>
    internal void Write(in JpxPacketKey key, ref PooledBuffer output)
    {
        var info = _tile.Components[key.Component];
        var layout = _tile.Resolutions[info.FirstResolution + key.Resolution];
        _bodies.Clear();
        _header.Reset();
        var present = AnyPasses(layout, key.Precinct, key.Layer);
        _header.WriteBit(present ? 1 : 0);
        if (present)
        {
            WriteBands(layout, key.Precinct, key.Layer);
        }

        WriteStartOfPacket(ref output);
        output.Write(_header.Finish());
        if (_settings.EndOfHeaderMarkers)
        {
            output.WriteByte(MarkerHigh);
            output.WriteByte(EphLow);
        }

        foreach (var body in _bodies.AsSpan())
        {
            output.Write(_code.Data.AsSpan().Slice(body.Offset, body.Length));
        }
    }

    /// <summary>Writes the number of coding passes (table B.4).</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="passes">The passes.</param>
    private static void WritePassCount(JpxBitWriter writer, int passes)
    {
        if (passes == 1)
        {
            writer.WriteBit(0);
        }
        else if (passes == TwoPasses)
        {
            writer.WriteBits(TwoPassCode, TwoPassBits);
        }
        else if (passes < MediumBase)
        {
            writer.WriteBits((ShortPrefix << ShortBits) | (passes - ShortBase), ShortBits + ShortBits);
        }
        else if (passes < LongBase)
        {
            writer.WriteBits((MediumPrefix << MediumBits) | (passes - MediumBase), MediumPrefixBits + MediumBits);
        }
        else
        {
            writer.WriteBits((LongPrefix << LongBits) | (passes - LongBase), LongPrefixBits + LongBits);
        }
    }

    /// <summary>Gets the bits a length needs.</summary>
    /// <param name="length">The length in bytes.</param>
    /// <returns>The bits; zero for a length of zero.</returns>
    private static int LengthWidth(int length) => length == 0 ? 0 : BitOperations.Log2((uint)length) + 1;

    /// <summary>Spreads every block's passes over the layers by bit-plane.</summary>
    private void AssignLayers()
    {
        var layers = _settings.Layers;
        var top = 0;
        for (var b = 0; b < _tile.Blocks.Count; b++)
        {
            top = Math.Max(top, _code.Blocks[b].Bits);
        }

        for (var b = 0; b < _tile.Blocks.Count; b++)
        {
            var bits = _code.Blocks[b].Bits;
            for (var l = 0; l < layers; l++)
            {
                // Layer l holds the planes from the cutoff up; the cutoff is zero in the last layer.
                var cutoff = (int)((long)top * (layers - 1 - l) / layers);
                _layerEnds[(b * layers) + l] = bits == 0 || cutoff > bits - 1 ? 0 : 1 + (PassesPerPlane * (bits - 1 - cutoff));
            }
        }
    }

    /// <summary>Builds the inclusion and zero bit-plane tag trees of every precinct.</summary>
    /// <returns><see langword="false"/> when a block has more bit-planes than its sub-band allows.</returns>
    private bool BuildTrees()
    {
        _trees.Clear();
        _inclusionTrees.Clear();
        _zeroTrees.Clear();
        for (var p = 0; p < _tile.Precincts.Count; p++)
        {
            var cell = _tile.Precincts[p];
            if (cell.BlockCount == 0)
            {
                _ = _inclusionTrees.Add(-1);
                _ = _zeroTrees.Add(-1);
                continue;
            }

            var inclusion = _trees.Add(cell.BlocksWide, cell.BlocksHigh);
            var zeros = _trees.Add(cell.BlocksWide, cell.BlocksHigh);
            for (var k = 0; k < cell.BlockCount; k++)
            {
                var block = cell.FirstBlock + k;
                var zeroPlanes = ZeroPlanes(block);
                if (zeroPlanes < 0)
                {
                    return false;
                }

                _trees.SetLeaf(inclusion, k, FirstLayer(block));
                _trees.SetLeaf(zeros, k, zeroPlanes);
            }

            _trees.Propagate(inclusion);
            _trees.Propagate(zeros);
            _ = _inclusionTrees.Add(inclusion);
            _ = _zeroTrees.Add(zeros);
        }

        return true;
    }

    /// <summary>Gets the first layer in which a block sends passes.</summary>
    /// <param name="block">The block.</param>
    /// <returns>The layer, or the layer count when the block never sends any.</returns>
    private int FirstLayer(int block)
    {
        var layers = _settings.Layers;
        for (var l = 0; l < layers; l++)
        {
            if (_layerEnds[(block * layers) + l] > 0)
            {
                return l;
            }
        }

        return layers;
    }

    /// <summary>Gets the number of missing most significant bit-planes of a block.</summary>
    /// <param name="block">The block.</param>
    /// <returns>The zero bit-planes, the largest value for a block that sends nothing, or -1 when the block has more bit-planes than its sub-band allows.</returns>
    private int ZeroPlanes(int block)
    {
        var bits = _code.Blocks[block].Bits;
        if (bits == 0)
        {
            return int.MaxValue;
        }

        var magnitude = _tile.Bands[_tile.Blocks[block].Band].Magnitude;
        return bits <= magnitude ? magnitude - bits : -1;
    }

    /// <summary>Determines whether any block of a precinct sends passes in a layer.</summary>
    /// <param name="layout">The resolution.</param>
    /// <param name="precinct">The precinct.</param>
    /// <param name="layer">The layer.</param>
    /// <returns><see langword="true"/> when the packet is not empty.</returns>
    private bool AnyPasses(in JpxResolutionLayout layout, int precinct, int layer)
    {
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
                if (NewPasses(cell.FirstBlock + k, layer) > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Gets the passes a block adds in a layer.</summary>
    /// <param name="block">The block.</param>
    /// <param name="layer">The layer.</param>
    /// <returns>The passes.</returns>
    private int NewPasses(int block, int layer)
    {
        var layers = _settings.Layers;
        var end = _layerEnds[(block * layers) + layer];
        return end - (layer == 0 ? 0 : _layerEnds[(block * layers) + layer - 1]);
    }

    /// <summary>Writes the SOP marker segment of a packet, when the tile uses them.</summary>
    /// <param name="output">The tile-part.</param>
    private void WriteStartOfPacket(ref PooledBuffer output)
    {
        if (!_settings.StartOfPacketMarkers)
        {
            return;
        }

        var span = output.GetSpan(StartOfPacketBytes);
        span[0] = MarkerHigh;
        span[1] = SopLow;
        span[2] = 0;
        span[3] = SopLength;
        BinaryPrimitives.WriteUInt16BigEndian(span[SequenceOffset..], (ushort)(_packetNumber & PacketNumberMask));
        output.Advance(StartOfPacketBytes);
        _packetNumber++;
    }

    /// <summary>Writes the header entries of every sub-band of a precinct.</summary>
    /// <param name="layout">The resolution.</param>
    /// <param name="precinct">The precinct.</param>
    /// <param name="layer">The layer.</param>
    private void WriteBands(in JpxResolutionLayout layout, int precinct, int layer)
    {
        for (var b = 0; b < layout.BandCount; b++)
        {
            var band = _tile.Bands[layout.FirstBand + b];
            if (band.Area.IsEmpty)
            {
                continue;
            }

            var index = band.FirstPrecinct + precinct;
            var cell = _tile.Precincts[index];
            for (var k = 0; k < cell.BlockCount; k++)
            {
                WriteBlock(index, k, cell.FirstBlock + k, layer);
            }
        }
    }

    /// <summary>Writes one block's header entry: inclusion, zero bit-planes, pass count and lengths.</summary>
    /// <param name="precinct">The precinct's index in the tile.</param>
    /// <param name="leaf">The block's index in the precinct.</param>
    /// <param name="block">The block's index in the tile.</param>
    /// <param name="layer">The layer.</param>
    private void WriteBlock(int precinct, int leaf, int block, int layer)
    {
        var passes = NewPasses(block, layer);
        if (_included[block] != 0)
        {
            _header.WriteBit(passes > 0 ? 1 : 0);
        }
        else
        {
            _trees.Encode(_header, _inclusionTrees[precinct], leaf, layer + 1);
        }

        if (passes == 0)
        {
            return;
        }

        if (_included[block] == 0)
        {
            _trees.Encode(_header, _zeroTrees[precinct], leaf, int.MaxValue);
            _included[block] = 1;
        }

        WritePassCount(_header, passes);
        WriteLengths(block, passes);
    }

    /// <summary>Writes a block's length increment and segment lengths, and queues the bytes for the packet body.</summary>
    /// <param name="block">The block.</param>
    /// <param name="passes">The passes this layer adds.</param>
    private void WriteLengths(int block, int passes)
    {
        CollectPieces(block, passes);
        var needed = _lengthBits[block];
        foreach (var piece in _pieces.AsSpan())
        {
            needed = Math.Max(needed, LengthWidth(piece.Length) - BitOperations.Log2((uint)piece.Passes));
        }

        for (var i = _lengthBits[block]; i < needed; i++)
        {
            _header.WriteBit(1);
        }

        _header.WriteBit(0);
        _lengthBits[block] = needed;
        foreach (var piece in _pieces.AsSpan())
        {
            _header.WriteBits(piece.Length, needed + BitOperations.Log2((uint)piece.Passes));
            _ = _bodies.Add(piece);
        }

        _sent[block] += passes;
    }

    /// <summary>Splits a layer's new passes at codeword segment ends: one piece per segment the layer touches.</summary>
    /// <param name="block">The block.</param>
    /// <param name="passes">The passes this layer adds.</param>
    private void CollectPieces(int block, int passes)
    {
        _pieces.Clear();
        var coded = _code.Blocks[block];
        var style = _settings.Style;
        var start = _sent[block];
        var end = start + passes;
        var ends = _code.PassEnds.AsSpan()[coded.PassStart..];
        var segmentStart = 0;
        var segmentOffset = 0;
        var limit = 0;
        while (segmentStart < coded.Passes && segmentStart < end)
        {
            limit = JpxPassPlan.SegmentLimit(style, limit);
            var segmentEnd = Math.Min(segmentStart + limit, coded.Passes);
            var from = Math.Max(start, segmentStart);
            var to = Math.Min(end, segmentEnd);
            if (from < to)
            {
                var before = from > segmentStart ? ends[from - 1] : 0;
                _ = _pieces.Add(new(to - from, ends[to - 1] - before, coded.DataStart + segmentOffset + before));
            }

            segmentOffset += ends[segmentEnd - 1];
            segmentStart = segmentEnd;
        }
    }

    /// <summary>The bytes one layer adds to one codeword segment of a block.</summary>
    /// <param name="Passes">The passes the piece covers.</param>
    /// <param name="Length">The bytes.</param>
    /// <param name="Offset">The offset of the first byte in the tile's data store.</param>
    private readonly record struct Piece(int Passes, int Length, int Offset);
}

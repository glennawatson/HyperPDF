// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Codes one tile's code-blocks and writes its packets (ISO 15444-1 B.9 to B.12) in the progression order. The packet
/// order of the position progressions comes from sorting precincts by where they sit on the reference grid, not from
/// the decoder's grid walk. Blocks with odd indices start one layer late, so inclusion tag trees span layers.
/// </summary>
internal sealed class JpxTestTileEncoder
{
    /// <summary>The values compared at each step of a key comparison.</summary>
    private const int PairSize = 2;

    /// <summary>The initial length indicator bits, Lblock.</summary>
    private const int InitialLengthBits = 3;

    /// <summary>The passes of one HT placeholder set.</summary>
    private const int PlaceholderSetPasses = 3;

    /// <summary>The fewest length indicator bits of an HT code-block in the MIXED mode.</summary>
    private const int MixedMinLengthBits = 4;

    /// <summary>A threshold above any zero bit-plane count, so the tag tree sends the whole value.</summary>
    private const int WholeValue = 999;

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

    /// <summary>The marker byte prefix.</summary>
    private const byte MarkerHigh = 0xFF;

    /// <summary>The second byte of SOP.</summary>
    private const byte SopLow = 0x91;

    /// <summary>The second byte of EPH.</summary>
    private const byte EphLow = 0x92;

    /// <summary>The length field of an SOP segment.</summary>
    private const byte SopLength = 4;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The passes coded as 10.</summary>
    private const int TwoPasses = 2;

    /// <summary>The code for two passes, 10.</summary>
    private const int TwoPassCode = 0b10;

    /// <summary>The bits of the code for two passes.</summary>
    private const int TwoPassBits = 2;

    /// <summary>The test options.</summary>
    private readonly JpxTestOptions _options;

    /// <summary>The image geometry.</summary>
    private readonly JpxGeometry _geometry;

    /// <summary>The tile layout from the decoder's partition code.</summary>
    private readonly JpxTile _tile;

    /// <summary>The coded blocks.</summary>
    private readonly JpxTestBlockCode[] _codes;

    /// <summary>The passes each block adds in each layer.</summary>
    private readonly int[][] _layerPasses;

    /// <summary>The passes each block has sent.</summary>
    private readonly int[] _sent;

    /// <summary>The length indicator bits of each block.</summary>
    private readonly int[] _lengthBits;

    /// <summary>Whether each block has been included.</summary>
    private readonly bool[] _included;

    /// <summary>Whether each block is coded with the HT block coder.</summary>
    private readonly bool[] _highThroughput;

    /// <summary>The inclusion tag tree of each precinct.</summary>
    private readonly Dictionary<int, JpxTestTagTree> _inclusion = [];

    /// <summary>The zero bit-plane tag tree of each precinct.</summary>
    private readonly Dictionary<int, JpxTestTagTree> _zeroPlanes = [];

    /// <summary>The packets written, for SOP numbering.</summary>
    private int _packetNumber;

    /// <summary>Initializes a new instance of the <see cref="JpxTestTileEncoder"/> class and codes every block.</summary>
    /// <param name="options">The test options.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="tile">The tile layout.</param>
    /// <param name="buffers">The tile-components' wavelet coefficients.</param>
    internal JpxTestTileEncoder(JpxTestOptions options, JpxGeometry geometry, JpxTile tile, int[][] buffers)
    {
        _options = options;
        _geometry = geometry;
        _tile = tile;
        var count = tile.Blocks.Count;
        _codes = new JpxTestBlockCode[count];
        _layerPasses = new int[count][];
        _sent = new int[count];
        _lengthBits = new int[count];
        _included = new bool[count];
        _highThroughput = new bool[count];
        var coder = new JpxTestBlockEncoder();
        for (var i = 0; i < count; i++)
        {
            _codes[i] = CodeBlock(coder, i, buffers);
            _layerPasses[i] = SplitLayers(i, _codes[i].Passes);
            _lengthBits[i] = InitialLengthBits;
        }

        BuildTrees();
    }

    /// <summary>Writes every packet in the progression order.</summary>
    /// <returns>The packets.</returns>
    internal List<JpxTestPacket> WritePackets()
    {
        var packets = new List<JpxTestPacket>();
        foreach (var key in Order())
        {
            packets.Add(WritePacket(key));
        }

        return packets;
    }

    /// <summary>Writes the number of coding passes (table B.4).</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="passes">The passes.</param>
    private static void WritePassCount(JpxTestBitWriter writer, int passes)
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

    /// <summary>Gets the packet comparison of a progression order.</summary>
    /// <param name="order">The order.</param>
    /// <returns>The comparison.</returns>
    private static Comparison<JpxTestPacketKey> Comparison(JpxProgressionOrder order) => order switch
    {
        JpxProgressionOrder.LayerResolutionComponentPosition => static (a, b) => Compare(a.Layer, b.Layer, a.Resolution, b.Resolution, a.Component, b.Component, a.Precinct, b.Precinct),
        JpxProgressionOrder.ResolutionLayerComponentPosition => static (a, b) => Compare(a.Resolution, b.Resolution, a.Layer, b.Layer, a.Component, b.Component, a.Precinct, b.Precinct),
        JpxProgressionOrder.ResolutionPositionComponentLayer => static (a, b) => Compare(a.Resolution, b.Resolution, a.Y, b.Y, a.X, b.X, a.Component, b.Component, a.Layer, b.Layer),
        JpxProgressionOrder.PositionComponentResolutionLayer => static (a, b) => Compare(a.Y, b.Y, a.X, b.X, a.Component, b.Component, a.Resolution, b.Resolution, a.Layer, b.Layer),
        _ => static (a, b) => Compare(a.Component, b.Component, a.Y, b.Y, a.X, b.X, a.Resolution, b.Resolution, a.Layer, b.Layer),
    };

    /// <summary>Compares keys field by field.</summary>
    /// <param name="values">Pairs of values, most significant first.</param>
    /// <returns>The comparison.</returns>
    private static int Compare(params ReadOnlySpan<long> values)
    {
        for (var i = 0; i + 1 < values.Length; i += PairSize)
        {
            var result = values[i].CompareTo(values[i + 1]);
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    /// <summary>Gets where on the reference grid the position progressions meet a precinct (B.12.1.3).</summary>
    /// <param name="start">The resolution's first coordinate.</param>
    /// <param name="exponent">The precinct size exponent.</param>
    /// <param name="index">The precinct's column or row.</param>
    /// <param name="level">The resolution's decomposition level.</param>
    /// <param name="subsampling">The component's subsampling.</param>
    /// <param name="tileStart">The tile's first reference grid coordinate.</param>
    /// <returns>The reference grid coordinate.</returns>
    private static long Position(int start, int exponent, int index, int level, int subsampling, int tileStart)
    {
        var grid = (long)((start >> exponent) + index) << exponent;
        return index == 0 && start % (1 << exponent) != 0 ? tileStart : (grid << level) * subsampling;
    }

    /// <summary>Codes one block from the tile-component coefficients.</summary>
    /// <param name="coder">The block coder.</param>
    /// <param name="index">The block index.</param>
    /// <param name="buffers">The tile-components' coefficients.</param>
    /// <returns>The coded block.</returns>
    /// <exception cref="InvalidOperationException">The block has more magnitude bits than the sub-band allows.</exception>
    private JpxTestBlockCode CodeBlock(JpxTestBlockEncoder coder, int index, int[][] buffers)
    {
        var block = _tile.Blocks[index];
        var band = _tile.Bands[block.Band];
        var stride = _tile.Components[band.Component].Area.Width;
        var x0 = band.BufferX + block.Area.X0 - band.Area.X0;
        var y0 = band.BufferY + block.Area.Y0 - band.Area.Y0;
        var width = block.Area.Width;
        var height = block.Area.Height;
        var coefficients = new int[width * height];
        for (var y = 0; y < height; y++)
        {
            buffers[band.Component].AsSpan(((y0 + y) * stride) + x0, width).CopyTo(coefficients.AsSpan(y * width));
        }

        var style = _tile.Components[band.Component].BlockStyle;

        // In the MIXED mode every other code-block is an HT code-block.
        _highThroughput[index] = (style & JpxBlockStyle.HighThroughput) != 0 && ((style & JpxBlockStyle.HighThroughputMixed) == 0 || (index & 1) == 1);
        var code = _highThroughput[index]
            ? JpxTestHtBlockEncoder.Encode(coefficients, width, height, _options.HtRefinement, (style & JpxBlockStyle.VerticallyCausal) != 0)
            : coder.Encode(coefficients, width, height, band.Orientation, style);
        if (code.Bits > band.Magnitude + _options.RoiShift)
        {
            throw new InvalidOperationException("The quantization exponents leave too few magnitude bits.");
        }

        return _highThroughput[index] ? WithPlaceholders(code, band.Magnitude + _options.RoiShift) : code;
    }

    /// <summary>
    /// Puts HT placeholder passes before a block's first cleanup pass: they join its first segment (T.814 clause B.2), and the zero
    /// bit-planes shrink by one for each set so the block's bit-planes stay the same (S_blk = P + P0, clause B.3).
    /// </summary>
    /// <param name="code">The coded block.</param>
    /// <param name="magnitude">The sub-band's magnitude bit-planes, with any region-of-interest shift.</param>
    /// <returns>The block with placeholder sets, as many as the option asks for and the zero bit-planes allow.</returns>
    private JpxTestBlockCode WithPlaceholders(JpxTestBlockCode code, int magnitude)
    {
        var sets = code.Bits == 0 ? 0 : Math.Min(_options.HtPlaceholderSets, magnitude - code.Bits);
        if (sets <= 0)
        {
            return code;
        }

        List<JpxTestSegment> segments = [code.Segments[0] with { Passes = code.Segments[0].Passes + (PlaceholderSetPasses * sets) }, .. code.Segments.Skip(1)];
        return new(code.Bits, segments) { PlaceholderSets = sets };
    }

    /// <summary>Spreads a block's passes over the layers.</summary>
    /// <param name="index">The block index.</param>
    /// <param name="passes">The block's passes.</param>
    /// <returns>The passes per layer.</returns>
    private int[] SplitLayers(int index, int passes)
    {
        var layers = _options.Layers;
        var shares = new int[layers];
        var first = layers > 1 && (index & 1) == 1 ? 1 : 0;
        var available = layers - first;
        for (var k = first; k < layers; k++)
        {
            shares[k] = ((passes * (k - first + 1)) / available) - ((passes * (k - first)) / available);
        }

        return shares;
    }

    /// <summary>Builds the inclusion and zero bit-plane tag trees of every precinct.</summary>
    private void BuildTrees()
    {
        for (var p = 0; p < _tile.Precincts.Count; p++)
        {
            var cell = _tile.Precincts[p];
            if (cell.BlockCount == 0)
            {
                continue;
            }

            var inclusion = new int[cell.BlockCount];
            var zeros = new int[cell.BlockCount];
            for (var k = 0; k < cell.BlockCount; k++)
            {
                var index = cell.FirstBlock + k;
                inclusion[k] = FirstLayer(index);
                zeros[k] = _codes[index].Bits == 0 ? 0 : _tile.Bands[_tile.Blocks[index].Band].Magnitude + _options.RoiShift - _codes[index].Bits - _codes[index].PlaceholderSets;
            }

            _inclusion[p] = new(cell.BlocksWide, cell.BlocksHigh, inclusion);
            _zeroPlanes[p] = new(cell.BlocksWide, cell.BlocksHigh, zeros);
        }
    }

    /// <summary>Gets the first layer a block sends passes in, or the layer count when it never does.</summary>
    /// <param name="index">The block index.</param>
    /// <returns>The layer.</returns>
    private int FirstLayer(int index)
    {
        var shares = _layerPasses[index];
        for (var k = 0; k < shares.Length; k++)
        {
            if (shares[k] > 0)
            {
                return k;
            }
        }

        return shares.Length;
    }

    /// <summary>Lists the tile's packets in its progression order.</summary>
    /// <returns>The packets.</returns>
    private List<JpxTestPacketKey> Order()
    {
        var keys = new List<JpxTestPacketKey>();
        for (var c = 0; c < _tile.Components.Length; c++)
        {
            var component = _tile.Components[c];
            for (var r = 0; r <= component.Levels; r++)
            {
                AddPrecinctKeys(keys, c, r);
            }
        }

        if (_options.Changes is not { } changes)
        {
            keys.Sort(Comparison(_options.Order));
            return keys;
        }

        // Each POC volume sends, in its own order, the packets of its ranges that no earlier volume sent.
        var ordered = new List<JpxTestPacketKey>();
        var sent = new HashSet<JpxTestPacketKey>();
        foreach (var change in changes)
        {
            var volume = keys.FindAll(key => key.Layer < change.LayerEnd
                && key.Resolution >= change.ResolutionStart && key.Resolution < change.ResolutionEnd
                && key.Component >= change.ComponentStart && key.Component < change.ComponentEnd
                && !sent.Contains(key));
            volume.Sort(Comparison(change.Order));
            ordered.AddRange(volume);
            sent.UnionWith(volume);
        }

        return ordered;
    }

    /// <summary>Adds a key per layer for each precinct of one resolution.</summary>
    /// <param name="keys">The keys.</param>
    /// <param name="component">The component.</param>
    /// <param name="resolution">The resolution.</param>
    private void AddPrecinctKeys(List<JpxTestPacketKey> keys, int component, int resolution)
    {
        var info = _tile.Components[component];
        var layout = _tile.Resolutions[info.FirstResolution + resolution];
        if (layout.Area.IsEmpty)
        {
            return;
        }

        var level = info.Levels - resolution;
        var dx = _geometry.Components[component].Dx;
        var dy = _geometry.Components[component].Dy;
        for (var p = 0; p < layout.PrecinctCount; p++)
        {
            var x = Position(layout.Area.X0, layout.PrecinctWidthExponent, p % layout.PrecinctsWide, level, dx, _tile.Area.X0);
            var y = Position(layout.Area.Y0, layout.PrecinctHeightExponent, p / layout.PrecinctsWide, level, dy, _tile.Area.Y0);
            for (var l = 0; l < _options.Layers; l++)
            {
                keys.Add(new(l, resolution, component, p, x, y));
            }
        }
    }

    /// <summary>Writes one packet.</summary>
    /// <param name="key">The packet.</param>
    /// <returns>The header and body.</returns>
    private JpxTestPacket WritePacket(in JpxTestPacketKey key)
    {
        var writer = new JpxTestBitWriter();
        var body = new List<byte>();
        var info = _tile.Components[key.Component];
        var layout = _tile.Resolutions[info.FirstResolution + key.Resolution];
        var present = AnyPasses(layout, key);
        writer.WriteBit(present ? 1 : 0);
        if (present)
        {
            for (var b = 0; b < layout.BandCount; b++)
            {
                var band = _tile.Bands[layout.FirstBand + b];
                if (!band.Area.IsEmpty)
                {
                    WriteBlocks(writer, body, band.FirstPrecinct + key.Precinct, key.Layer);
                }
            }
        }

        var header = new List<byte>(writer.Finish());
        if (!_options.Markers)
        {
            return new([], [.. header], [.. body]);
        }

        header.Add(MarkerHigh);
        header.Add(EphLow);
        return new(StartOfPacket(), [.. header], [.. body]);
    }

    /// <summary>Makes the SOP marker segment of the next packet.</summary>
    /// <returns>The marker segment bytes.</returns>
    private byte[] StartOfPacket()
    {
        var number = _packetNumber;
        _packetNumber++;
        return [MarkerHigh, SopLow, 0, SopLength, (byte)(number >> ByteBits), (byte)number];
    }

    /// <summary>Determines whether any block of a packet's precinct sends passes in its layer.</summary>
    /// <param name="layout">The resolution.</param>
    /// <param name="key">The packet.</param>
    /// <returns><see langword="true"/> when the packet is not empty.</returns>
    private bool AnyPasses(in JpxResolutionLayout layout, in JpxTestPacketKey key)
    {
        for (var b = 0; b < layout.BandCount; b++)
        {
            var band = _tile.Bands[layout.FirstBand + b];
            if (band.Area.IsEmpty)
            {
                continue;
            }

            var cell = _tile.Precincts[band.FirstPrecinct + key.Precinct];
            for (var k = 0; k < cell.BlockCount; k++)
            {
                if (_layerPasses[cell.FirstBlock + k][key.Layer] > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Writes the header entries and body bytes of one precinct's blocks in one band.</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="body">The body bytes.</param>
    /// <param name="precinct">The precinct index within the tile.</param>
    /// <param name="layer">The layer.</param>
    private void WriteBlocks(JpxTestBitWriter writer, List<byte> body, int precinct, int layer)
    {
        var cell = _tile.Precincts[precinct];
        for (var k = 0; k < cell.BlockCount; k++)
        {
            var index = cell.FirstBlock + k;
            var passes = _layerPasses[index][layer];
            if (_included[index])
            {
                writer.WriteBit(passes > 0 ? 1 : 0);
            }
            else
            {
                _inclusion[precinct].Encode(writer, k, layer + 1);
            }

            if (passes == 0)
            {
                continue;
            }

            if (!_included[index])
            {
                _zeroPlanes[precinct].Encode(writer, k, WholeValue);
                _included[index] = true;
            }

            WritePassCount(writer, passes);
            WriteLengths(writer, body, index, passes);
        }
    }

    /// <summary>Writes a block's length increment and segment lengths, and appends the bytes of segments it completes.</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="body">The body bytes.</param>
    /// <param name="index">The block index.</param>
    /// <param name="passes">The passes this layer adds.</param>
    private void WriteLengths(JpxTestBitWriter writer, List<byte> body, int index, int passes)
    {
        var pieces = Pieces(index, passes);
        var needed = _lengthBits[index];
        foreach (var piece in pieces)
        {
            var bits = piece.Length == 0 ? 0 : BitOperations.Log2((uint)piece.Length) + 1;
            needed = Math.Max(needed, bits - BitOperations.Log2((uint)piece.Passes));
        }

        if (_highThroughput[index] && _sent[index] == 0 && (_tile.Components[_tile.Bands[_tile.Blocks[index].Band].Component].BlockStyle & JpxBlockStyle.HighThroughputMixed) != 0)
        {
            // T.814 clause A.4: the first length field of an HT code-block in the MIXED mode starts with a 0 bit and Lblock is above 3.
            var first = pieces[0].Length == 0 ? 0 : BitOperations.Log2((uint)pieces[0].Length) + 1;
            needed = Math.Max(needed, Math.Max(MixedMinLengthBits, first + 1));
        }

        for (var i = _lengthBits[index]; i < needed; i++)
        {
            writer.WriteBit(1);
        }

        writer.WriteBit(0);
        _lengthBits[index] = needed;
        foreach (var piece in pieces)
        {
            writer.WriteBits(piece.Length, needed + BitOperations.Log2((uint)piece.Passes));
            body.AddRange(piece.Data);
        }

        _sent[index] += passes;
    }

    /// <summary>Splits a layer's new passes at segment ends, as the decoder will.</summary>
    /// <param name="index">The block index.</param>
    /// <param name="passes">The passes this layer adds.</param>
    /// <returns>The pieces: passes, and the segment's bytes when the piece ends it.</returns>
    private List<JpxTestSegmentPiece> Pieces(int index, int passes)
    {
        var pieces = new List<JpxTestSegmentPiece>();
        var start = _sent[index];
        var end = start + passes;
        var segmentStart = 0;
        foreach (var segment in _codes[index].Segments)
        {
            var segmentEnd = segmentStart + segment.Passes;
            var from = Math.Max(start, segmentStart);
            var to = Math.Min(end, segmentEnd);
            if (from < to)
            {
                pieces.Add(new(to - from, (to == segmentEnd) ? segment.Data : []));
            }

            segmentStart = segmentEnd;
        }

        return pieces;
    }
}

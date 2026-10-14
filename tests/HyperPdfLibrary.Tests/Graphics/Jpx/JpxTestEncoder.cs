// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Writes lossless JPEG 2000 codestreams (5/3 wavelet, no quantization) of 8-bit samples for round-trip tests: tiles,
/// subsampled components, the reversible component transform, precincts, layers, every progression order, the code-block
/// mode switches, SOP and EPH markers, PPT packed headers, split tile-parts and an optional JP2 wrapper.
/// </summary>
internal static class JpxTestEncoder
{
    /// <summary>The sample precision.</summary>
    private const int Precision = 8;

    /// <summary>The DC level shift of 8-bit samples.</summary>
    private const int LevelShift = 128;

    /// <summary>The guard bits.</summary>
    private const int GuardBits = 2;

    /// <summary>The exponent added to the precision for every sub-band: room for wavelet and transform growth.</summary>
    private const int ExponentHeadroom = 6;

    /// <summary>The sub-bands per resolution above the lowest.</summary>
    private const int BandsPerLevel = 3;

    /// <summary>The marker byte prefix.</summary>
    private const byte MarkerHigh = 0xFF;

    /// <summary>The low byte of SOC.</summary>
    private const byte SocLow = 0x4F;

    /// <summary>The low byte of SIZ.</summary>
    private const byte SizLow = 0x51;

    /// <summary>The low byte of CAP.</summary>
    private const byte CapLow = 0x50;

    /// <summary>The low byte of COD.</summary>
    private const byte CodLow = 0x52;

    /// <summary>The low byte of QCD.</summary>
    private const byte QcdLow = 0x5C;

    /// <summary>The low byte of POC.</summary>
    private const byte PocLow = 0x5F;

    /// <summary>The spacing of the coefficients inside the region of interest.</summary>
    private const int RegionStride = 3;

    /// <summary>The high byte of Ccap15 for the MIXED mode: both code-block mix bits set.</summary>
    private const byte MixedCcapHigh = 0xC0;

    /// <summary>The low byte of RGN.</summary>
    private const byte RgnLow = 0x5E;

    /// <summary>The low byte of PPT.</summary>
    private const byte PptLow = 0x61;

    /// <summary>The low byte of SOT.</summary>
    private const byte SotLow = 0x90;

    /// <summary>The low byte of SOD.</summary>
    private const byte SodLow = 0x93;

    /// <summary>The low byte of EOC.</summary>
    private const byte EocLow = 0xD9;

    /// <summary>The Ssiz value of unsigned 8-bit samples.</summary>
    private const byte EightBitUnsigned = Precision - 1;

    /// <summary>The bytes of the fixed part of SIZ after its length.</summary>
    private const int SizFixedBytes = 36;

    /// <summary>The bytes of each SIZ component entry.</summary>
    private const int SizComponentBytes = 3;

    /// <summary>The bits of Scod that ask for SOP and EPH markers.</summary>
    private const byte MarkerFlags = 0x06;

    /// <summary>The bias of the stored code-block exponents.</summary>
    private const int BlockExponentBias = 2;

    /// <summary>The transform value of the 5/3 reversible wavelet.</summary>
    private const byte ReversibleTransform = 1;

    /// <summary>The shift of the exponent in an SPqcd byte.</summary>
    private const int ExponentShift = 3;

    /// <summary>The shift of the guard bits in Sqcd.</summary>
    private const int GuardShift = 5;

    /// <summary>The bits of a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The tile-parts of a split tile.</summary>
    private const int SplitParts = 2;

    /// <summary>The bytes of the SOT segment, marker included.</summary>
    private const int TileStartBytes = 12;

    /// <summary>The bytes of the SOD marker.</summary>
    private const int MarkerBytes = 2;

    /// <summary>The largest PPT data piece that fits one marker segment.</summary>
    private const int MaxPackedPiece = 60_000;

    /// <summary>The weight of green in the forward RCT.</summary>
    private const int GreenWeight = 2;

    /// <summary>The shift that divides the RCT luma sum by four.</summary>
    private const int QuarterShift = 2;

    /// <summary>The third component, which the RCT also covers.</summary>
    private const int ThirdComponent = 2;

    /// <summary>The components the RCT covers.</summary>
    private const int TransformComponents = 3;

    /// <summary>Encodes an image.</summary>
    /// <param name="options">What to write.</param>
    /// <param name="planes">Each component's 8-bit samples, at the component's resolution.</param>
    /// <returns>The codestream, or a JP2 file when <see cref="JpxTestOptions.Wrap"/> is set.</returns>
    /// <exception cref="InvalidOperationException">The options describe an invalid image.</exception>
    internal static byte[] Encode(JpxTestOptions options, int[][] planes)
    {
        var siz = SizSegment(options);
        var cod = CodSegment(options);
        var qcd = QcdSegment(options);
        var geometry = JpxGeometry.Read(siz) ?? throw new InvalidOperationException("The SIZ segment is invalid.");
        var main = new JpxHeaderState(options.Components);
        _ = main.Read(JpxMarkers.CodingStyle, cod, 0);
        _ = main.Read(JpxMarkers.Quantization, qcd, 0);
        for (var c = 0; options.RoiShift > 0 && c < options.Components; c++)
        {
            _ = main.Read(JpxMarkers.RegionOfInterest, RgnSegment(c, options.RoiShift), 0);
        }

        var parameters = JpxTileParameters.Resolve(main, new(options.Components));
        var output = new List<byte> { MarkerHigh, SocLow };
        WriteSegment(output, SizLow, siz);
        if ((options.Style & JpxBlockStyle.HighThroughput) != 0)
        {
            WriteSegment(output, CapLow, CapSegment(options.Style));
        }

        WriteSegment(output, CodLow, cod);
        WriteSegment(output, QcdLow, qcd);
        for (var c = 0; options.RoiShift > 0 && c < options.Components; c++)
        {
            WriteSegment(output, RgnLow, RgnSegment(c, options.RoiShift));
        }

        if (options.Changes is { } changes)
        {
            WriteSegment(output, PocLow, PocSegment(changes));
        }

        using var tile = new JpxTile(options.Components);
        var tiles = geometry.TilesAcross * geometry.TilesDown;
        for (var t = 0; t < tiles; t++)
        {
            _ = tile.Build(geometry, t, parameters);
            var buffers = TileBuffers(options, geometry, tile, planes);
            var packets = new JpxTestTileEncoder(options, geometry, tile, buffers).WritePackets();
            WriteTileParts(output, options, t, packets);
        }

        output.Add(MarkerHigh);
        output.Add(EocLow);
        return options.Wrap ? JpxTestFileWriter.Wrap([.. output], options.Components, options.Width, options.Height) : [.. output];
    }

    /// <summary>Writes the SIZ segment after its length field.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The segment.</returns>
    private static byte[] SizSegment(JpxTestOptions options)
    {
        var segment = new byte[SizFixedBytes + (options.Components * SizComponentBytes)];
        var span = segment.AsSpan();
        if ((options.Style & JpxBlockStyle.HighThroughput) != 0)
        {
            BinaryPrimitives.WriteUInt16BigEndian(span, JpxCapabilities.CapabilitiesFlag);
        }

        var right = options.X0 + options.Width;
        var bottom = options.Y0 + options.Height;
        int[] values = [right, bottom, options.X0, options.Y0, options.TileWidth > 0 ? options.TileWidth : right, options.TileHeight > 0 ? options.TileHeight : bottom, 0, 0];
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(span[(MarkerBytes + (i * sizeof(int)))..], values[i]);
        }

        BinaryPrimitives.WriteUInt16BigEndian(span[(SizFixedBytes - MarkerBytes)..], (ushort)options.Components);
        for (var c = 0; c < options.Components; c++)
        {
            var entry = span.Slice(SizFixedBytes + (c * SizComponentBytes), SizComponentBytes);
            var subsampling = (byte)(c == 0 ? 1 : options.ChromaSubsampling);
            entry[0] = EightBitUnsigned;
            entry[1] = subsampling;
            entry[MarkerBytes] = subsampling;
        }

        return segment;
    }

    /// <summary>Writes the CAP segment after its length field: Part 15 used, with Ccap15 zero for HT-only reversible coding, or the MIXED mode bits.</summary>
    /// <param name="style">The code-block style.</param>
    /// <returns>The segment.</returns>
    private static byte[] CapSegment(JpxBlockStyle style)
    {
        var segment = new byte[sizeof(uint) + sizeof(ushort)];
        BinaryPrimitives.WriteUInt32BigEndian(segment, JpxCapabilities.Part15);
        if ((style & JpxBlockStyle.HighThroughputMixed) != 0)
        {
            segment[sizeof(uint)] = MixedCcapHigh;
        }

        return segment;
    }

    /// <summary>Writes the COD segment after its length field.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The segment.</returns>
    private static byte[] CodSegment(JpxTestOptions options)
    {
        var precincts = options.PrecinctExponent > 0;
        var flags = (byte)((precincts ? 1 : 0) | (options.Markers ? MarkerFlags : 0));
        var bytes = new List<byte>
        {
            flags,
            (byte)options.Order,
            (byte)(options.Layers >> Precision),
            (byte)options.Layers,
            (byte)(options.Transform ? 1 : 0),
            (byte)options.Levels,
            (byte)(options.BlockExponent - BlockExponentBias),
            (byte)(options.BlockExponent - BlockExponentBias),
            (byte)options.Style,
            ReversibleTransform,
        };
        for (var r = 0; precincts && r <= options.Levels; r++)
        {
            bytes.Add((byte)(options.PrecinctExponent | (options.PrecinctExponent << NibbleBits)));
        }

        return [.. bytes];
    }

    /// <summary>Writes the QCD segment after its length field: no quantization, the same exponent for every sub-band.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The segment.</returns>
    private static byte[] QcdSegment(JpxTestOptions options)
    {
        var segment = new byte[1 + (BandsPerLevel * options.Levels) + 1];
        segment[0] = GuardBits << GuardShift;
        segment.AsSpan(1).Fill((Precision + ExponentHeadroom) << ExponentShift);
        return segment;
    }

    /// <summary>Writes an RGN segment after its length field: a one-byte component index and the implicit (maxshift) style.</summary>
    /// <param name="component">The component.</param>
    /// <param name="shift">The shift.</param>
    /// <returns>The segment.</returns>
    private static byte[] RgnSegment(int component, int shift) => [(byte)component, 0, (byte)shift];

    /// <summary>Writes the POC segment after its length field, with one-byte component indices.</summary>
    /// <param name="changes">The progression volumes.</param>
    /// <returns>The segment.</returns>
    private static byte[] PocSegment(JpxProgressionChange[] changes)
    {
        var bytes = new List<byte>();
        foreach (var change in changes)
        {
            bytes.Add((byte)change.ResolutionStart);
            bytes.Add((byte)change.ComponentStart);
            bytes.Add((byte)(change.LayerEnd >> Precision));
            bytes.Add((byte)change.LayerEnd);
            bytes.Add((byte)change.ResolutionEnd);
            bytes.Add((byte)change.ComponentEnd);
            bytes.Add((byte)change.Order);
        }

        return [.. bytes];
    }

    /// <summary>Appends a marker segment.</summary>
    /// <param name="output">The output.</param>
    /// <param name="marker">The marker's low byte.</param>
    /// <param name="segment">The segment after its length field.</param>
    private static void WriteSegment(List<byte> output, byte marker, byte[] segment)
    {
        var length = segment.Length + MarkerBytes;
        output.Add(MarkerHigh);
        output.Add(marker);
        output.Add((byte)(length >> Precision));
        output.Add((byte)length);
        output.AddRange(segment);
    }

    /// <summary>Gets each tile-component's wavelet coefficients: level-shifted, transformed and decomposed.</summary>
    /// <param name="options">The options.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="tile">The tile layout.</param>
    /// <param name="planes">The component samples.</param>
    /// <returns>The coefficients per component.</returns>
    private static int[][] TileBuffers(JpxTestOptions options, JpxGeometry geometry, JpxTile tile, int[][] planes)
    {
        var buffers = new int[options.Components][];
        for (var c = 0; c < options.Components; c++)
        {
            var area = tile.Components[c].Area;
            var plane = geometry.ToComponent(geometry.Image, c);
            buffers[c] = new int[area.Width * area.Height];
            for (var y = 0; y < area.Height; y++)
            {
                for (var x = 0; x < area.Width; x++)
                {
                    var source = ((area.Y0 + y - plane.Y0) * plane.Width) + area.X0 + x - plane.X0;
                    buffers[c][(y * area.Width) + x] = planes[c][source] - LevelShift;
                }
            }
        }

        if (options.Transform && options.Components >= TransformComponents)
        {
            ForwardTransform(buffers);
        }

        for (var c = 0; c < options.Components; c++)
        {
            JpxTestWavelet.Forward(tile, tile.Components[c], buffers[c]);
            ShiftRegion(buffers[c], options.RoiShift);
        }

        return buffers;
    }

    /// <summary>Shifts every third coefficient up by the region-of-interest shift (the maxshift method, annex H): the decoder tells them apart by magnitude.</summary>
    /// <param name="buffer">The coefficients.</param>
    /// <param name="shift">The shift, or zero for none.</param>
    private static void ShiftRegion(int[] buffer, int shift)
    {
        for (var i = 0; shift > 0 && i < buffer.Length; i += RegionStride)
        {
            buffer[i] <<= shift;
        }
    }

    /// <summary>Applies the forward reversible component transform (equation G-5).</summary>
    /// <param name="buffers">The first three components, transformed in place.</param>
    private static void ForwardTransform(int[][] buffers)
    {
        var red = buffers[0];
        var green = buffers[1];
        var blue = buffers[ThirdComponent];
        for (var i = 0; i < red.Length; i++)
        {
            var luma = (red[i] + (GreenWeight * green[i]) + blue[i]) >> QuarterShift;
            var cb = blue[i] - green[i];
            var cr = red[i] - green[i];
            red[i] = luma;
            green[i] = cb;
            blue[i] = cr;
        }
    }

    /// <summary>Writes a tile's packets as one or two tile-parts.</summary>
    /// <param name="output">The codestream.</param>
    /// <param name="options">The options.</param>
    /// <param name="tile">The tile index.</param>
    /// <param name="packets">The packets in order.</param>
    private static void WriteTileParts(List<byte> output, JpxTestOptions options, int tile, List<JpxTestPacket> packets)
    {
        var parts = options.SplitTileParts ? SplitParts : 1;
        var split = options.SplitTileParts ? packets.Count / SplitParts : packets.Count;
        WriteTilePart(output, options, tile, packets.GetRange(0, split), new(0, parts));
        if (options.SplitTileParts)
        {
            WriteTilePart(output, options, tile, packets.GetRange(split, packets.Count - split), new(1, parts));
        }
    }

    /// <summary>Writes one tile-part.</summary>
    /// <param name="output">The codestream.</param>
    /// <param name="options">The options.</param>
    /// <param name="tile">The tile index.</param>
    /// <param name="packets">The tile-part's packets.</param>
    /// <param name="part">The tile-part's index and the tile's tile-part count.</param>
    private static void WriteTilePart(List<byte> output, JpxTestOptions options, int tile, List<JpxTestPacket> packets, JpxTestPartIndex part)
    {
        var data = new List<byte>();
        var headers = new List<byte>();
        foreach (var packet in packets)
        {
            data.AddRange(packet.Start);
            (options.PackedHeaders ? headers : data).AddRange(packet.Header);
            data.AddRange(packet.Body);
        }

        var markers = new List<byte>();
        for (var z = 0; z * MaxPackedPiece < headers.Count; z++)
        {
            var piece = headers.GetRange(z * MaxPackedPiece, Math.Min(MaxPackedPiece, headers.Count - (z * MaxPackedPiece)));
            WriteSegment(markers, PptLow, [(byte)z, .. piece]);
        }

        var length = TileStartBytes + markers.Count + MarkerBytes + data.Count;
        output.AddRange([MarkerHigh, SotLow, 0, TileStartBytes - MarkerBytes, (byte)(tile >> Precision), (byte)tile]);
        var lengthBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, length);
        output.AddRange(lengthBytes);
        output.Add((byte)part.Index);
        output.Add((byte)part.Count);
        output.AddRange(markers);
        output.Add(MarkerHigh);
        output.Add(SodLow);
        output.AddRange(data);
    }
}

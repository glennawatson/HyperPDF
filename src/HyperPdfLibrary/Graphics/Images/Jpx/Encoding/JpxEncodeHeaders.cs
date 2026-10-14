// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>Writes the markers of a codestream (ISO 15444-1 annex A): SIZ, COD, and the start of each tile-part.</summary>
internal static class JpxEncodeHeaders
{
    /// <summary>The bytes of the fixed part of SIZ after its length.</summary>
    private const int SizFixedBytes = 36;

    /// <summary>The bytes of each SIZ component entry.</summary>
    private const int SizComponentBytes = 3;

    /// <summary>The offset of the image width in SIZ.</summary>
    private const int WidthOffset = 2;

    /// <summary>The offset of the image height in SIZ.</summary>
    private const int HeightOffset = 6;

    /// <summary>The offset of the tile width in SIZ.</summary>
    private const int TileWidthOffset = 18;

    /// <summary>The offset of the tile height in SIZ.</summary>
    private const int TileHeightOffset = 22;

    /// <summary>The offset of the component count in SIZ.</summary>
    private const int CountOffset = 34;

    /// <summary>The offset of the horizontal subsampling in a SIZ component entry.</summary>
    private const int SubsamplingXOffset = 1;

    /// <summary>The offset of the vertical subsampling in a SIZ component entry.</summary>
    private const int SubsamplingYOffset = 2;

    /// <summary>The bytes of the fixed part of COD after its length.</summary>
    private const int CodFixedBytes = 10;

    /// <summary>The offset of the progression order in COD.</summary>
    private const int ProgressionOffset = 1;

    /// <summary>The offset of the layer count in COD.</summary>
    private const int LayersOffset = 2;

    /// <summary>The offset of the multiple component transform flag in COD.</summary>
    private const int ComponentTransformOffset = 4;

    /// <summary>The offset of the decomposition levels in COD.</summary>
    private const int LevelsOffset = 5;

    /// <summary>The offset of the code-block width in COD.</summary>
    private const int BlockWidthOffset = 6;

    /// <summary>The offset of the code-block height in COD.</summary>
    private const int BlockHeightOffset = 7;

    /// <summary>The offset of the code-block style in COD.</summary>
    private const int StyleOffset = 8;

    /// <summary>The offset of the wavelet transform in COD.</summary>
    private const int WaveletOffset = 9;

    /// <summary>The Scod bit that says precinct sizes follow.</summary>
    private const int PrecinctsBit = 1;

    /// <summary>The Scod bit that asks for SOP markers.</summary>
    private const int SopBit = 2;

    /// <summary>The Scod bit that asks for EPH markers.</summary>
    private const int EphBit = 4;

    /// <summary>The bias of the stored code-block exponents.</summary>
    private const int BlockExponentBias = 2;

    /// <summary>The transform value of the 5/3 reversible wavelet.</summary>
    private const byte ReversibleTransform = 1;

    /// <summary>The bits of a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The bytes of a marker.</summary>
    private const int MarkerBytes = 2;

    /// <summary>The bytes of a marker and its length field.</summary>
    private const int MarkerHeaderBytes = 4;

    /// <summary>The length field of an SOT segment.</summary>
    private const int TileStartLength = 10;

    /// <summary>The bytes of the SOT segment, marker included.</summary>
    private const int TileStartBytes = 12;

    /// <summary>The offset of the tile index in an SOT segment.</summary>
    private const int TileIndexOffset = 4;

    /// <summary>The offset of the tile-part length in an SOT segment.</summary>
    private const int TileLengthOffset = 6;

    /// <summary>The offset of the tile-part index in an SOT segment.</summary>
    private const int TilePartOffset = 10;

    /// <summary>The offset of the tile-part count in an SOT segment.</summary>
    private const int TilePartCountOffset = 11;

    /// <summary>Writes the SIZ segment after its length field.</summary>
    /// <param name="settings">The coding parameters.</param>
    /// <returns>The segment.</returns>
    internal static byte[] Siz(JpxEncodeSettings settings)
    {
        var segment = new byte[SizFixedBytes + (settings.Components * SizComponentBytes)];
        var span = segment.AsSpan();
        BinaryPrimitives.WriteInt32BigEndian(span[WidthOffset..], settings.Width);
        BinaryPrimitives.WriteInt32BigEndian(span[HeightOffset..], settings.Height);
        BinaryPrimitives.WriteInt32BigEndian(span[TileWidthOffset..], settings.TileWidth);
        BinaryPrimitives.WriteInt32BigEndian(span[TileHeightOffset..], settings.TileHeight);
        BinaryPrimitives.WriteUInt16BigEndian(span[CountOffset..], (ushort)settings.Components);
        for (var c = 0; c < settings.Components; c++)
        {
            var entry = span.Slice(SizFixedBytes + (c * SizComponentBytes), SizComponentBytes);
            entry[0] = (byte)(settings.Precision - 1);
            entry[SubsamplingXOffset] = 1;
            entry[SubsamplingYOffset] = 1;
        }

        return segment;
    }

    /// <summary>Writes the COD segment after its length field.</summary>
    /// <param name="settings">The coding parameters.</param>
    /// <returns>The segment.</returns>
    internal static byte[] Cod(JpxEncodeSettings settings)
    {
        var precincts = settings.PrecinctExponent > 0;
        var segment = new byte[CodFixedBytes + (precincts ? settings.Levels + 1 : 0)];
        segment[0] = (byte)((precincts ? PrecinctsBit : 0) | (settings.StartOfPacketMarkers ? SopBit : 0) | (settings.EndOfHeaderMarkers ? EphBit : 0));
        segment[ProgressionOffset] = (byte)settings.Order;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(LayersOffset), (ushort)settings.Layers);
        segment[ComponentTransformOffset] = (byte)(settings.UseTransform ? 1 : 0);
        segment[LevelsOffset] = (byte)settings.Levels;
        segment[BlockWidthOffset] = (byte)(settings.BlockWidthExponent - BlockExponentBias);
        segment[BlockHeightOffset] = (byte)(settings.BlockHeightExponent - BlockExponentBias);
        segment[StyleOffset] = (byte)settings.Style;
        segment[WaveletOffset] = settings.Reversible ? ReversibleTransform : (byte)0;
        for (var r = 0; precincts && r <= settings.Levels; r++)
        {
            segment[CodFixedBytes + r] = (byte)(settings.PrecinctExponent | (settings.PrecinctExponent << NibbleBits));
        }

        return segment;
    }

    /// <summary>Writes a marker segment: the marker, its length and its contents.</summary>
    /// <param name="output">The codestream.</param>
    /// <param name="marker">The marker code.</param>
    /// <param name="segment">The segment after its length field.</param>
    internal static void WriteSegment(ref PooledBuffer output, int marker, ReadOnlySpan<byte> segment)
    {
        var span = output.GetSpan(MarkerHeaderBytes + segment.Length);
        BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)marker);
        BinaryPrimitives.WriteUInt16BigEndian(span[MarkerBytes..], (ushort)(segment.Length + MarkerBytes));
        segment.CopyTo(span[MarkerHeaderBytes..]);
        output.Advance(MarkerHeaderBytes + segment.Length);
    }

    /// <summary>Writes a bare marker.</summary>
    /// <param name="output">The codestream.</param>
    /// <param name="marker">The marker code.</param>
    internal static void WriteMarker(ref PooledBuffer output, int marker)
    {
        BinaryPrimitives.WriteUInt16BigEndian(output.GetSpan(MarkerBytes), (ushort)marker);
        output.Advance(MarkerBytes);
    }

    /// <summary>Writes the SOT segment of a tile's only tile-part and its SOD marker, with the length left to <see cref="FinishTilePart"/>.</summary>
    /// <param name="output">The codestream.</param>
    /// <param name="tile">The tile index.</param>
    /// <returns>The position of the SOT marker.</returns>
    internal static int WriteTileStart(ref PooledBuffer output, int tile)
    {
        var position = output.Length;
        var span = output.GetSpan(TileStartBytes);
        span[..TileStartBytes].Clear();
        BinaryPrimitives.WriteUInt16BigEndian(span, JpxMarkers.StartOfTile);
        BinaryPrimitives.WriteUInt16BigEndian(span[MarkerBytes..], TileStartLength);
        BinaryPrimitives.WriteUInt16BigEndian(span[TileIndexOffset..], (ushort)tile);
        span[TilePartOffset] = 0;
        span[TilePartCountOffset] = 1;
        output.Advance(TileStartBytes);
        WriteMarker(ref output, JpxMarkers.StartOfData);
        return position;
    }

    /// <summary>Writes the length of a tile-part into its SOT segment.</summary>
    /// <param name="output">The codestream.</param>
    /// <param name="position">The position of the SOT marker.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FinishTilePart(ref PooledBuffer output, int position) =>
        BinaryPrimitives.WriteUInt32BigEndian(Written(ref output)[(position + TileLengthOffset)..], (uint)(output.Length - position));

    /// <summary>Writes a length into the bytes already written.</summary>
    /// <param name="output">The buffer.</param>
    /// <param name="position">The position of the four-byte length.</param>
    /// <param name="value">The length.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void PatchLength(ref PooledBuffer output, int position, uint value) =>
        BinaryPrimitives.WriteUInt32BigEndian(Written(ref output)[position..], value);

    /// <summary>Gets the bytes written so far, for patching a length.</summary>
    /// <param name="output">The buffer.</param>
    /// <returns>The written bytes.</returns>
    private static Span<byte> Written(ref PooledBuffer output) => output.Array is { } array ? array.AsSpan(0, output.Length) : [];
}

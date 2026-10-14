// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The coding parameters one header sets: the main header, or the tile-part headers of one tile. Values left
/// <see langword="null"/> fall back to the main header; component-specific markers win over the defaults of the same header.
/// </summary>
[DebuggerDisplay("JpxHeaderState: coding {Coding}")]
internal sealed class JpxHeaderState
{
    /// <summary>The bytes of one progression change with a one-byte component index.</summary>
    private const int ChangeBytes = 7;

    /// <summary>The offset of the layer end in a progression change.</summary>
    private const int LayerEndOffset = 2;

    /// <summary>The bytes of a 16-bit field.</summary>
    private const int ShortBytes = 2;

    /// <summary>The component end value that means 256 components.</summary>
    private const int AllComponents = 256;

    /// <summary>The component end value that means 16384 components, the most there can be.</summary>
    private const int AllWideComponents = 16_384;

    /// <summary>The components above which a component index takes two bytes.</summary>
    private const int ByteIndexComponents = 256;

    /// <summary>The bytes of an RGN segment with a one-byte component index.</summary>
    private const int RoiBytes = 3;

    /// <summary>The marker value that means no region-of-interest shift was given.</summary>
    private const int Unset = -1;

    /// <summary>The Zppm or Zppt index of each entry of <see cref="PackedHeaders"/>.</summary>
    private readonly List<byte> _packedIndices = [];

    /// <summary>The bytes of a component index in COC, QCC, RGN and POC segments.</summary>
    private readonly int _indexBytes;

    /// <summary>The count of packed pieces before the current tile-part header; Zppt numbers restart in each header.</summary>
    private int _partStart;

    /// <summary>Initializes a new instance of the <see cref="JpxHeaderState"/> class.</summary>
    /// <param name="components">The number of components.</param>
    internal JpxHeaderState(int components)
    {
        _indexBytes = components > ByteIndexComponents ? ShortBytes : 1;
        ComponentStyles = new JpxCodingStyle?[components];
        ComponentQuantizations = new JpxQuantization?[components];
        RoiShifts = new int[components];
        RoiShifts.AsSpan().Fill(Unset);
    }

    /// <summary>Gets the per-component coding styles from COC markers.</summary>
    internal JpxCodingStyle?[] ComponentStyles { get; }

    /// <summary>Gets the per-component quantizations from QCC markers.</summary>
    internal JpxQuantization?[] ComponentQuantizations { get; }

    /// <summary>Gets the per-component region-of-interest shifts from RGN markers, or -1.</summary>
    internal int[] RoiShifts { get; }

    /// <summary>Gets the tile-wide coding parameters from COD, or <see langword="null"/>.</summary>
    internal JpxTileCoding? Coding { get; private set; }

    /// <summary>Gets the default coding style from COD, or <see langword="null"/>.</summary>
    internal JpxCodingStyle? Style { get; private set; }

    /// <summary>Gets the default quantization from QCD, or <see langword="null"/>.</summary>
    internal JpxQuantization? Quantization { get; private set; }

    /// <summary>Gets the progression changes from POC markers, or <see langword="null"/>.</summary>
    internal List<JpxProgressionChange>? Changes { get; private set; }

    /// <summary>Gets the packed packet header pieces from PPM or PPT markers, offsets into the codestream, or <see langword="null"/>.</summary>
    internal List<JpxDataRange>? PackedHeaders { get; private set; }

    /// <summary>Starts the header of another tile-part, whose PPT segments are numbered again from zero.</summary>
    internal void BeginTilePart() => _partStart = PackedHeaders?.Count ?? 0;

    /// <summary>Reads one marker segment; markers the decoder does not need are skipped.</summary>
    /// <param name="marker">The marker code.</param>
    /// <param name="segment">The segment after its length field.</param>
    /// <param name="offset">The offset of <paramref name="segment"/> in the codestream.</param>
    /// <returns><see langword="false"/> when a needed segment is invalid.</returns>
    internal bool Read(int marker, ReadOnlySpan<byte> segment, int offset) => marker switch
    {
        JpxMarkers.CodingStyle => ReadCodingStyle(segment),
        JpxMarkers.ComponentCodingStyle => ReadComponentStyle(segment),
        JpxMarkers.Quantization => (Quantization = JpxQuantization.Read(segment)) is not null,
        JpxMarkers.ComponentQuantization => ReadComponentQuantization(segment),
        JpxMarkers.RegionOfInterest => ReadRegion(segment),
        JpxMarkers.ProgressionChange => ReadChanges(segment),
        JpxMarkers.PackedMainHeaders or JpxMarkers.PackedTileHeaders => AddPacked(segment, offset),
        _ => true,
    };

    /// <summary>Reads a COD segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadCodingStyle(ReadOnlySpan<byte> segment)
    {
        Coding = JpxTileCoding.Read(segment);
        if (Coding is null)
        {
            return false;
        }

        Style = JpxCodingStyle.Read(segment[JpxTileCoding.Bytes..], (segment[0] & JpxCodingStyle.PrecinctsBit) != 0, out _);
        return Style is not null;
    }

    /// <summary>Reads a COC segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadComponentStyle(ReadOnlySpan<byte> segment)
    {
        if (segment.Length < _indexBytes + 1 || ReadIndex(segment, 0) >= ComponentStyles.Length)
        {
            return false;
        }

        var style = JpxCodingStyle.Read(segment[(_indexBytes + 1)..], (segment[_indexBytes] & JpxCodingStyle.PrecinctsBit) != 0, out _);
        ComponentStyles[ReadIndex(segment, 0)] = style;
        return style is not null;
    }

    /// <summary>Reads a component index: one byte, or two when the image has more than 256 components (A.6.2, A.6.3).</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="offset">The offset of the index.</param>
    /// <returns>The index, or a value past any component when the segment is too short.</returns>
    private int ReadIndex(ReadOnlySpan<byte> segment, int offset)
    {
        if (segment.Length < offset + _indexBytes)
        {
            return int.MaxValue;
        }

        return _indexBytes == 1 ? segment[offset] : BinaryPrimitives.ReadUInt16BigEndian(segment[offset..]);
    }

    /// <summary>Reads a QCC segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadComponentQuantization(ReadOnlySpan<byte> segment)
    {
        if (segment.Length <= _indexBytes || ReadIndex(segment, 0) >= ComponentQuantizations.Length)
        {
            return false;
        }

        var quantization = JpxQuantization.Read(segment[_indexBytes..]);
        ComponentQuantizations[ReadIndex(segment, 0)] = quantization;
        return quantization is not null;
    }

    /// <summary>Reads an RGN segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadRegion(ReadOnlySpan<byte> segment)
    {
        if (segment.Length < _indexBytes + RoiBytes - 1 || ReadIndex(segment, 0) >= RoiShifts.Length)
        {
            return false;
        }

        // Srgn is 0 (implicit maxshift) in this edition (A.6.3); other methods are not defined, so the marker is ignored.
        if (segment[_indexBytes] == 0)
        {
            RoiShifts[ReadIndex(segment, 0)] = segment[_indexBytes + 1];
        }

        return true;
    }

    /// <summary>Reads a POC segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadChanges(ReadOnlySpan<byte> segment)
    {
        // An entry is the resolution start, component start, layer end, resolution end, component end and order; the component fields are two bytes when there are over 256 components (A.6.6).
        var width = ChangeBytes + (ShortBytes * (_indexBytes - 1));
        var count = segment.Length / width;
        if (count == 0)
        {
            return false;
        }

        // A tile's first POC replaces any inherited list; later POC segments of the same header add to it.
        Changes ??= [];
        for (var i = 0; i < count; i++)
        {
            var entry = segment.Slice(i * width, width);
            var order = entry[width - 1];
            if (order > (int)JpxProgressionOrder.ComponentPositionResolutionLayer)
            {
                return false;
            }

            Changes.Add(ReadChange(entry, (JpxProgressionOrder)order));
        }

        return true;
    }

    /// <summary>Reads one POC entry.</summary>
    /// <param name="entry">The entry's bytes.</param>
    /// <param name="order">The progression order, the entry's last byte.</param>
    /// <returns>The change.</returns>
    private JpxProgressionChange ReadChange(ReadOnlySpan<byte> entry, JpxProgressionOrder order)
    {
        var layerEnd = LayerEndOffset - 1 + _indexBytes;
        var componentEnd = ReadIndex(entry, layerEnd + ShortBytes + 1);
        if (componentEnd == 0)
        {
            componentEnd = _indexBytes == 1 ? AllComponents : AllWideComponents;
        }

        return new(entry[0], ReadIndex(entry, 1), BinaryPrimitives.ReadUInt16BigEndian(entry[layerEnd..]), entry[layerEnd + ShortBytes], componentEnd, order);
    }

    /// <summary>Records a PPM or PPT segment's packed headers, after its index byte.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="offset">The offset of the segment in the codestream.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool AddPacked(ReadOnlySpan<byte> segment, int offset)
    {
        if (segment.IsEmpty)
        {
            return false;
        }

        // Zppm and Zppt number the segments, which may appear in any order; the packed data is read in that order (A.7.4, A.7.5).
        var headers = PackedHeaders ??= [];
        var index = _packedIndices.Count;
        while (index > _partStart && _packedIndices[index - 1] > segment[0])
        {
            index--;
        }

        _packedIndices.Insert(index, segment[0]);
        headers.Insert(index, new(offset + 1, segment.Length - 1));
        return true;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decodes a JPEG 2000 codestream (ISO 15444-1) to component planes: for each tile, the tile-part headers, the packets
/// (tier 2), the code-blocks (tier 1), the inverse wavelet transform, the inverse component transform and the DC level
/// shift. Code-blocks may use the high-throughput coder (T.814), chosen per tile-component by its coding style.
/// Damaged tiles keep what decoded before the damage; missing tiles stay zero.
/// </summary>
[DebuggerDisplay("JpxDecoder: {_codestream.TileParts.Count} tile-parts")]
internal sealed class JpxDecoder : IDisposable
{
    /// <summary>The codestream structure.</summary>
    private readonly JpxCodestream _codestream;

    /// <summary>The tile layout, reused for every tile.</summary>
    private readonly JpxTile _tile;

    /// <summary>The packet reader.</summary>
    private readonly JpxPacketDecoder _packets = new();

    /// <summary>The progression walker.</summary>
    private readonly JpxProgression _progression;

    /// <summary>The tile rebuild: code-blocks, transforms and output.</summary>
    private readonly JpxTileWork _work;

    /// <summary>The code-block decoder used on the calling thread.</summary>
    private readonly JpxBlockState _blocks = new();

    /// <summary>The finest wavelet levels omitted from output.</summary>
    private readonly int _reductionLevels;

    /// <summary>The current tile's packet data.</summary>
    private byte[] _tileData = [];

    /// <summary>The current tile's packed packet headers.</summary>
    private byte[] _packed = [];

    /// <summary>Initializes a new instance of the <see cref="JpxDecoder"/> class.</summary>
    /// <param name="codestream">The codestream structure.</param>
    /// <param name="reductionLevels">The finest levels to omit.</param>
    private JpxDecoder(JpxCodestream codestream, int reductionLevels)
    {
        _codestream = codestream;
        _reductionLevels = reductionLevels;
        _tile = new(codestream.Geometry.Components.Length);
        _progression = new(codestream.Geometry, _packets);
        _work = new(codestream.Geometry);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _tile.Dispose();
        _work.Release();
        _blocks.Dispose();
        ReturnArray(ref _tileData);
        ReturnArray(ref _packed);
    }

    /// <summary>Decodes a codestream.</summary>
    /// <param name="codestream">The codestream's structure, from <see cref="JpxCodestream.Read"/>.</param>
    /// <param name="data">The codestream, from its SOC marker.</param>
    /// <returns>The component planes; the caller disposes them.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JpxDecodedImage Decode(JpxCodestream codestream, ReadOnlySpan<byte> data) =>
        Decode(codestream, data, 0);

    /// <summary>Decodes only the wavelet resolutions needed for a smaller displayed image.</summary>
    /// <param name="codestream">The parsed codestream.</param>
    /// <param name="data">The codestream bytes.</param>
    /// <param name="reductionLevels">The finest levels to leave out.</param>
    /// <returns>The reduced component planes.</returns>
    internal static JpxDecodedImage Decode(JpxCodestream codestream, ReadOnlySpan<byte> data, int reductionLevels)
    {
        var image = new JpxDecodedImage(codestream.Geometry, reductionLevels);
        using var decoder = new JpxDecoder(codestream, reductionLevels);
        decoder.DecodeTiles(data, image);
        return image;
    }

    /// <summary>Returns a rented array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, emptied.</param>
    private static void ReturnArray<T>(ref T[] array)
    {
        if (array.Length > 0)
        {
            ScratchPool<T>.Shared.Return(array);
        }

        array = [];
    }

    /// <summary>Makes sure a rented array holds enough elements, keeping none of its contents.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, replaced when too small.</param>
    /// <param name="length">The elements needed.</param>
    private static void EnsureArray<T>(ref T[] array, int length)
    {
        if (array.Length >= length)
        {
            return;
        }

        ReturnArray(ref array);
        array = ScratchPool<T>.Shared.Rent(length);
    }

    /// <summary>Sorts the tile-part indices by tile, keeping codestream order within a tile.</summary>
    /// <param name="parts">The tile-parts.</param>
    /// <param name="tiles">The number of tiles.</param>
    /// <param name="starts">Receives where each tile's tile-parts start in <paramref name="order"/>, plus the end.</param>
    /// <param name="order">Receives the tile-part indices.</param>
    private static void GroupByTile(List<JpxTilePart> parts, int tiles, int[] starts, int[] order)
    {
        starts.AsSpan(0, tiles + 1).Clear();
        foreach (var part in parts)
        {
            starts[part.Tile + 1]++;
        }

        for (var t = 0; t < tiles; t++)
        {
            starts[t + 1] += starts[t];
        }

        // Each tile's next free slot starts at its start and moves one along per tile-part placed.
        var next = ScratchPool<int>.Shared.Rent(tiles);
        starts.AsSpan(0, tiles).CopyTo(next);
        for (var i = 0; i < parts.Count; i++)
        {
            var tile = parts[i].Tile;
            order[next[tile]] = i;
            next[tile]++;
        }

        ScratchPool<int>.Shared.Return(next);
    }

    /// <summary>Determines whether a tile mixes HT and regular code-blocks within a component, which is not decoded.</summary>
    /// <param name="parameters">The tile's coding parameters.</param>
    /// <returns><see langword="true"/> when any component does.</returns>
    private static bool HasMixedBlocks(JpxTileParameters parameters)
    {
        foreach (var style in parameters.Styles)
        {
            if ((style.BlockStyle & JpxBlockStyle.HighThroughputMixed) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Decodes every tile that has data, in tile order.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="image">The output planes.</param>
    private void DecodeTiles(ReadOnlySpan<byte> data, JpxDecodedImage image)
    {
        var parts = _codestream.TileParts;
        var tiles = _codestream.Geometry.TilesAcross * _codestream.Geometry.TilesDown;
        var starts = ScratchPool<int>.Shared.Rent(tiles + 1);
        var order = ScratchPool<int>.Shared.Rent(Math.Max(parts.Count, 1));
        try
        {
            GroupByTile(parts, tiles, starts, order);
            for (var t = 0; t < tiles; t++)
            {
                PdfCancellation.ThrowIfCancelled();
                if (starts[t + 1] > starts[t])
                {
                    DecodeTile(data, t, order.AsSpan(starts[t], starts[t + 1] - starts[t]), image);
                }
            }
        }
        finally
        {
            ScratchPool<int>.Shared.Return(starts);
            ScratchPool<int>.Shared.Return(order);
        }
    }

    /// <summary>Decodes one tile into the output planes.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="tile">The tile index.</param>
    /// <param name="parts">The indices of the tile's tile-parts.</param>
    /// <param name="image">The output planes.</param>
    private void DecodeTile(ReadOnlySpan<byte> data, int tile, ReadOnlySpan<int> parts, JpxDecodedImage image)
    {
        var state = new JpxHeaderState(_codestream.Geometry.Components.Length);
        foreach (var part in parts)
        {
            if (!JpxCodestream.ReadTileHeader(data, _codestream.TileParts[part].Header, state))
            {
                return;
            }
        }

        var parameters = JpxTileParameters.Resolve(_codestream.Main, state);
        if (HasMixedBlocks(parameters) || !_tile.Build(_codestream.Geometry, tile, parameters))
        {
            return;
        }

        var length = CopyTileData(data, parts);
        var packedLength = CopyPackedHeaders(data, parts, state);
        _packets.Start(_tile, parameters.Coding, _tileData, length, packedLength >= 0 ? _packed : null, Math.Max(packedLength, 0));
        _progression.Run(_tile, parameters);
        _work.Run(_tile, _tileData, parameters.Coding.ComponentTransform, image, _blocks, _reductionLevels);
    }

    /// <summary>Joins the tile's packet data from all its tile-parts.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="parts">The indices of the tile's tile-parts.</param>
    /// <returns>The bytes of packet data.</returns>
    private int CopyTileData(ReadOnlySpan<byte> data, ReadOnlySpan<int> parts)
    {
        var total = 0;
        foreach (var part in parts)
        {
            total += _codestream.TileParts[part].Data.Length;
        }

        EnsureArray(ref _tileData, total);
        var written = 0;
        foreach (var part in parts)
        {
            var range = _codestream.TileParts[part].Data;
            data.Slice(range.Offset, range.Length).CopyTo(_tileData.AsSpan(written));
            written += range.Length;
        }

        return total;
    }

    /// <summary>Joins the tile's packed packet headers from PPM or PPT markers.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="parts">The indices of the tile's tile-parts.</param>
    /// <param name="state">The tile's header parameters, which hold any PPT pieces.</param>
    /// <returns>The bytes of packed headers, or -1 when the headers are in the packet data.</returns>
    private int CopyPackedHeaders(ReadOnlySpan<byte> data, ReadOnlySpan<int> parts, JpxHeaderState state)
    {
        if (_codestream.PackedMain is { } main && _codestream.PackedRanges is { } ranges)
        {
            var total = 0;
            foreach (var part in parts)
            {
                total += ranges[part].Length;
            }

            EnsureArray(ref _packed, total);
            var written = 0;
            foreach (var part in parts)
            {
                main.AsSpan(ranges[part].Offset, ranges[part].Length).CopyTo(_packed.AsSpan(written));
                written += ranges[part].Length;
            }

            return total;
        }

        return state.PackedHeaders is { } pieces ? CopyPieces(data, pieces) : -1;
    }

    /// <summary>Joins PPT pieces of the codestream.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="pieces">The pieces.</param>
    /// <returns>The bytes joined.</returns>
    private int CopyPieces(ReadOnlySpan<byte> data, List<JpxDataRange> pieces)
    {
        var total = 0;
        foreach (var piece in pieces)
        {
            total += piece.Length;
        }

        EnsureArray(ref _packed, total);
        var written = 0;
        foreach (var piece in pieces)
        {
            data.Slice(piece.Offset, piece.Length).CopyTo(_packed.AsSpan(written));
            written += piece.Length;
        }

        return total;
    }
}

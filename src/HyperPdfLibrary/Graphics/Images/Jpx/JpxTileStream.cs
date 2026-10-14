// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// One tile as a source of finished sample rows. Opening the tile reads its headers and packets (tier 2), which is
/// proportional to the compressed size; the code-blocks (tier 1), the inverse wavelet transform and the inverse component
/// transform then run a block of rows at a time as rows are read, and each row is level-shifted into a row buffer
/// (ISO/IEC 15444-1 annexes F and G). The objects are reused from tile to tile.
/// </summary>
[DebuggerDisplay("JpxTileStream: {_components.Length} components")]
internal sealed class JpxTileStream : IDisposable
{
    /// <summary>The components the multiple component transform covers.</summary>
    private const int TransformComponents = 3;

    /// <summary>The index of the third component.</summary>
    private const int ThirdComponent = 2;

    /// <summary>The fewest samples in a row of the transform components for them to rebuild a block on several threads.</summary>
    private const int ParallelWidth = 512;

    /// <summary>The cache, decoders and token of the decode.</summary>
    private readonly JpxBlockWorkers _workers;

    /// <summary>The tile layout and per-block state.</summary>
    private readonly JpxTile _tile;

    /// <summary>The component streams.</summary>
    private readonly JpxComponentStream[] _components;

    /// <summary>The row buffers of the finished rows, one for each wanted component.</summary>
    private readonly int[][] _out;

    /// <summary>The rows each component has finished.</summary>
    private readonly int[] _produced;

    /// <summary>Prepares the transform components' next blocks of rows on several threads.</summary>
    private readonly JpxComponentPrepareJob _prepare;

    /// <summary>The geometry of the image.</summary>
    private JpxGeometry _geometry;

    /// <summary>The tile's packet data.</summary>
    private byte[] _data = [];

    /// <summary>The tile's packed packet headers.</summary>
    private byte[] _packed = [];

    /// <summary>Where each sub-band's code-blocks start in the tile.</summary>
    private int[] _bandStarts = [];

    /// <summary>The components the caller reads.</summary>
    private uint _wanted;

    /// <summary>Whether the inverse component transform runs on the first three components.</summary>
    private bool _transform;

    /// <summary>Whether the transform components are reversible.</summary>
    private bool _reversibleTransform;

    /// <summary>The rows the transform components have finished.</summary>
    private int _transformProduced;

    /// <summary>Initializes a new instance of the <see cref="JpxTileStream"/> class.</summary>
    /// <param name="geometry">The geometry of the image.</param>
    /// <param name="workers">The cache, decoders and token of the decode.</param>
    internal JpxTileStream(JpxGeometry geometry, JpxBlockWorkers workers)
    {
        _geometry = geometry;
        _workers = workers;
        var components = geometry.Components.Length;
        _tile = new(components, workers.Pools);
        _components = new JpxComponentStream[components];
        _out = new int[components][];
        _produced = new int[components];
        for (var c = 0; c < components; c++)
        {
            _components[c] = new();
            _out[c] = [];
        }

        _prepare = new(_components);
    }

    /// <summary>Gets the number of components the stream was made for.</summary>
    internal int ComponentCount => _components.Length;

    /// <inheritdoc/>
    public void Dispose()
    {
        Release();
        _tile.Dispose();
        foreach (var component in _components)
        {
            component.Dispose();
        }
    }

    /// <summary>Points an idle stream at the image of the next decode, which has as many components as the stream was made for.</summary>
    /// <param name="geometry">The geometry of the image.</param>
    internal void Bind(JpxGeometry geometry) => _geometry = geometry;

    /// <summary>Reads a tile's headers and packets, and sets up its rows.</summary>
    /// <param name="decoder">The decoder, which holds the codestream, packet reader and progression walker.</param>
    /// <param name="tile">The tile index.</param>
    /// <param name="parts">The indices of the tile's tile-parts.</param>
    /// <param name="data">The codestream.</param>
    /// <returns><see langword="false"/> when the tile cannot be decoded, and so stays zero.</returns>
    internal bool Start(JpxDecoder decoder, int tile, ReadOnlySpan<int> parts, ReadOnlySpan<byte> data)
    {
        Release();
        var codestream = decoder.Codestream;
        var state = new JpxHeaderState(_geometry.Components.Length);
        foreach (var part in parts)
        {
            if (!JpxCodestream.ReadTileHeader(data, codestream.TileParts[part].Header, state))
            {
                return false;
            }
        }

        var parameters = JpxTileParameters.Resolve(codestream.Main, state);
        if (!_tile.Build(_geometry, tile, parameters))
        {
            return false;
        }

        var length = CopyTileData(codestream, data, parts);
        var packedLength = CopyPackedHeaders(codestream, data, parts, state);
        decoder.Packets.Start(_tile, parameters.Coding, _data, length, packedLength >= 0 ? _packed : null, Math.Max(packedLength, 0));
        decoder.Progression.Run(_tile, parameters);
        ReturnArray(ref _packed);
        BindComponents(decoder, parameters.Coding.ComponentTransform);
        return true;
    }

    /// <summary>Gets a finished row of a component.</summary>
    /// <param name="component">The component, which the caller must have asked to read.</param>
    /// <param name="row">The row in the tile-component. Rows are read in order; the last row read may be read again.</param>
    /// <returns>The level-shifted samples, valid until the next row of the component is read.</returns>
    /// <exception cref="InvalidOperationException">A row before the last one read is asked for.</exception>
    internal ReadOnlySpan<int> ReadRow(int component, int row)
    {
        if (_transform && component < TransformComponents)
        {
            while (_transformProduced <= row)
            {
                ProduceTransformRow();
            }

            if (_transformProduced - 1 != row)
            {
                throw new InvalidOperationException("The rows of a tile are read out of order.");
            }
        }
        else
        {
            while (_produced[component] <= row)
            {
                ProduceRow(component);
            }

            if (_produced[component] - 1 != row)
            {
                throw new InvalidOperationException("The rows of a tile are read out of order.");
            }
        }

        return _out[component].AsSpan(0, _components[component].Width);
    }

    /// <summary>Returns the working memory, keeping the objects for the next tile.</summary>
    internal void Release()
    {
        foreach (var component in _components)
        {
            component.Release();
        }

        for (var c = 0; c < _out.Length; c++)
        {
            ReturnArray(ref _out[c]);
            _produced[c] = 0;
        }

        ReturnArray(ref _data);
        ReturnArray(ref _packed);
        ReturnArray(ref _bandStarts);
        _transform = false;
        _transformProduced = 0;
    }

    /// <summary>Returns a rented array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, emptied.</param>
    private void ReturnArray<T>(ref T[] array)
    {
        if (array.Length > 0)
        {
            _workers.Pools.Get<T>().Return(array);
        }

        array = [];
    }

    /// <summary>Rents an array that holds the given elements, replacing a smaller one.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array.</param>
    /// <param name="length">The elements needed.</param>
    private void EnsureArray<T>(ref T[] array, int length)
    {
        if (array.Length >= length)
        {
            return;
        }

        ReturnArray(ref array);
        array = _workers.Pools.Get<T>().Rent(length);
    }

    /// <summary>Joins the tile's packet data from all its tile-parts.</summary>
    /// <param name="codestream">The codestream structure.</param>
    /// <param name="data">The codestream.</param>
    /// <param name="parts">The indices of the tile's tile-parts.</param>
    /// <returns>The bytes of packet data.</returns>
    private int CopyTileData(JpxCodestream codestream, ReadOnlySpan<byte> data, ReadOnlySpan<int> parts)
    {
        var total = 0;
        foreach (var part in parts)
        {
            total += codestream.TileParts[part].Data.Length;
        }

        EnsureArray(ref _data, total);
        var written = 0;
        foreach (var part in parts)
        {
            var range = codestream.TileParts[part].Data;
            data.Slice(range.Offset, range.Length).CopyTo(_data.AsSpan(written));
            written += range.Length;
        }

        return total;
    }

    /// <summary>Joins the tile's packed packet headers from PPM or PPT markers.</summary>
    /// <param name="codestream">The codestream structure.</param>
    /// <param name="data">The codestream.</param>
    /// <param name="parts">The indices of the tile's tile-parts.</param>
    /// <param name="state">The tile's header parameters, which hold any PPT pieces.</param>
    /// <returns>The bytes of packed headers, or -1 when the headers are in the packet data.</returns>
    private int CopyPackedHeaders(JpxCodestream codestream, ReadOnlySpan<byte> data, ReadOnlySpan<int> parts, JpxHeaderState state)
    {
        if (codestream.PackedMain is { } main && codestream.PackedRanges is { } ranges)
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

    /// <summary>Sets up the rows of the components the tile decodes.</summary>
    /// <param name="decoder">The decoder.</param>
    /// <param name="transform">Whether the tile asks for the multiple component transform.</param>
    private void BindComponents(JpxDecoder decoder, bool transform)
    {
        var reduction = decoder.ReductionLevels;
        _wanted = decoder.Wanted;
        _transform = transform && SameSize(reduction);
        var decoded = _transform ? JpxComponentMask.WithTransformComponents(_wanted) : _wanted;
        _reversibleTransform = _tile.Components[0].Reversible;
        EnsureArray(ref _bandStarts, _tile.Bands.Count + 1);
        _tile.FindBandBlocks(_bandStarts);
        for (var c = 0; c < _components.Length; c++)
        {
            _produced[c] = 0;
            if (!JpxComponentMask.Contains(decoded, c))
            {
                continue;
            }

            _components[c].Bind(_workers, _tile, _data, _bandStarts, c, reduction);
            if (JpxComponentMask.Contains(_wanted, c))
            {
                EnsureArray(ref _out[c], Math.Max(_components[c].Width, 1));
            }
        }
    }

    /// <summary>Determines whether the first three components can take part in the component transform: there are three, and they have the same size at the decoded resolution.</summary>
    /// <param name="reduction">The finest levels left out.</param>
    /// <returns><see langword="true"/> when the transform applies.</returns>
    private bool SameSize(int reduction)
    {
        if (_components.Length < TransformComponents)
        {
            return false;
        }

        var area = _tile.Components[0].Area.Reduce(reduction);
        return _tile.Components[1].Area.Reduce(reduction) == area && _tile.Components[ThirdComponent].Area.Reduce(reduction) == area;
    }

    /// <summary>Finishes the next row of a component outside the transform.</summary>
    /// <param name="component">The component.</param>
    private void ProduceRow(int component)
    {
        Shift(component, _components[component].NextRow());
        _produced[component]++;
    }

    /// <summary>Finishes the next row of the three transform components together.</summary>
    private void ProduceTransformRow()
    {
        if (_components[0].Width >= ParallelWidth && Environment.ProcessorCount > 1 && !(_components[0].IsReady && _components[1].IsReady && _components[ThirdComponent].IsReady))
        {
            _prepare.Run(TransformComponents);
        }

        var first = _components[0].NextRow();
        var second = _components[1].NextRow();
        var third = _components[ThirdComponent].NextRow();
        if (_reversibleTransform)
        {
            JpxComponentTransform.InverseReversible(first, second, third);
        }
        else
        {
            JpxComponentTransform.InverseIrreversible(MemoryMarshal.Cast<int, float>(first), MemoryMarshal.Cast<int, float>(second), MemoryMarshal.Cast<int, float>(third));
        }

        Shift(0, first);
        Shift(1, second);
        Shift(ThirdComponent, third);
        _transformProduced++;
    }

    /// <summary>Adds the level shift to a row and clamps it into the component's row buffer, when the caller reads the component.</summary>
    /// <param name="component">The component.</param>
    /// <param name="row">The row's samples, as integers or the bits of floats.</param>
    private void Shift(int component, Span<int> row)
    {
        if (!JpxComponentMask.Contains(_wanted, component))
        {
            return;
        }

        var range = JpxSampleRange.For(_geometry.Components[component]);
        var destination = _out[component].AsSpan(0, row.Length);
        if (_tile.Components[component].Reversible)
        {
            JpxComponentTransform.ShiftReversible(row, destination, range);
        }
        else
        {
            JpxComponentTransform.ShiftIrreversible(MemoryMarshal.Cast<int, float>(row), destination, range);
        }
    }
}

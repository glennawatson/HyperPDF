// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Rebuilds one tile after its packets are read: decodes the code-blocks, runs the inverse wavelet and component
/// transforms, and level-shifts the samples into the output planes. Code-blocks decode in parallel, and components
/// transform and store in parallel, when the tile is large enough for that to pay; every worker writes its own part of
/// the buffers.
/// </summary>
[DebuggerDisplay("JpxTileWork: {_buffers.Length} components")]
internal sealed class JpxTileWork
{
    /// <summary>The fewest code-blocks decoded in parallel.</summary>
    private const int ParallelBlocks = 16;

    /// <summary>The fewest samples per component for components to be transformed in parallel.</summary>
    private const long ParallelSamples = 1L << 16;

    /// <summary>The components the multiple component transform covers.</summary>
    private const int TransformComponents = 3;

    /// <summary>The index of the third component.</summary>
    private const int ThirdComponent = 2;

    /// <summary>The image geometry.</summary>
    private readonly JpxGeometry _geometry;

    /// <summary>The tile-component buffers of the current tile.</summary>
    private readonly int[][] _buffers;

    /// <summary>Which tile-component buffers are rented rather than output planes.</summary>
    private readonly bool[] _rented;

    /// <summary>The wavelet scratch space of each component.</summary>
    private readonly int[][] _scratches;

    /// <summary>The tile being rebuilt.</summary>
    private JpxTile _tile = null!;

    /// <summary>The tile's packet data.</summary>
    private byte[] _data = [];

    /// <summary>The output planes.</summary>
    private JpxDecodedImage _image = null!;

    /// <summary>The number of finest wavelet levels omitted from this decode.</summary>
    private int _reductionLevels;

    /// <summary>Initializes a new instance of the <see cref="JpxTileWork"/> class.</summary>
    /// <param name="geometry">The image geometry.</param>
    internal JpxTileWork(JpxGeometry geometry)
    {
        _geometry = geometry;
        var components = geometry.Components.Length;
        _buffers = new int[components][];
        _rented = new bool[components];
        _scratches = new int[components][];
        for (var c = 0; c < components; c++)
        {
            _buffers[c] = [];
            _scratches[c] = [];
        }
    }

    /// <summary>Rebuilds a tile into the output planes.</summary>
    /// <param name="tile">The tile, with its packets read.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="transform">Whether the tile uses the multiple component transform.</param>
    /// <param name="image">The output planes.</param>
    /// <param name="blocks">The block decoder used when the blocks decode on the calling thread.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Run(JpxTile tile, byte[] data, bool transform, JpxDecodedImage image, JpxBlockState blocks) =>
        Run(tile, data, transform, image, blocks, 0);

    /// <summary>Rebuilds a tile only to the requested wavelet resolution.</summary>
    /// <param name="tile">The parsed tile.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="transform">Whether the colour components are transformed.</param>
    /// <param name="image">The reduced output planes.</param>
    /// <param name="blocks">The calling thread's block decoder.</param>
    /// <param name="reductionLevels">The omitted finest wavelet levels.</param>
    internal void Run(JpxTile tile, byte[] data, bool transform, JpxDecodedImage image, JpxBlockState blocks, int reductionLevels)
    {
        _tile = tile;
        _data = data;
        _image = image;
        _reductionLevels = reductionLevels;
        try
        {
            PrepareBuffers();
            DecodeBlocks(blocks);
            ForEachComponent(InverseWavelet);
            if (transform)
            {
                InverseComponentTransform();
            }

            ForEachComponent(Store);
        }
        finally
        {
            ReleaseBuffers();
        }
    }

    /// <summary>Returns the pooled scratch space.</summary>
    internal void Release()
    {
        ReleaseBuffers();
        for (var c = 0; c < _scratches.Length; c++)
        {
            ReturnArray(ref _scratches[c]);
        }
    }

    /// <summary>Returns a rented array.</summary>
    /// <param name="array">The array, emptied.</param>
    private static void ReturnArray(ref int[] array)
    {
        if (array.Length > 0)
        {
            ScratchPool<int>.Shared.Return(array);
        }

        array = [];
    }

    /// <summary>Disposes a worker's block decoder.</summary>
    /// <param name="decoder">The decoder.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReleaseDecoder(JpxBlockState decoder) => decoder.Dispose();

    /// <summary>Creates a worker's block decoder.</summary>
    /// <returns>The decoder.</returns>
    private static JpxBlockState CreateDecoder() => new();

    /// <summary>Gets a zeroed coefficient buffer and wavelet scratch for each tile-component; the buffer is the output plane itself when the tile covers it.</summary>
    private void PrepareBuffers()
    {
        for (var c = 0; c < _buffers.Length; c++)
        {
            var area = _tile.Components[c].Area.Reduce(_reductionLevels);
            var length = area.Width * area.Height;
            _rented[c] = area != _image.Areas[c];
            _buffers[c] = _rented[c] ? ScratchPool<int>.Shared.Rent(Math.Max(length, 1)) : _image.Planes[c];
            _buffers[c].AsSpan(0, length).Clear();
            var scratch = JpxWavelet.ScratchLength(area.Width, area.Height);
            if (_scratches[c].Length >= scratch)
            {
                continue;
            }

            ReturnArray(ref _scratches[c]);
            _scratches[c] = ScratchPool<int>.Shared.Rent(scratch);
        }
    }

    /// <summary>Returns the rented tile-component buffers.</summary>
    private void ReleaseBuffers()
    {
        for (var c = 0; c < _buffers.Length; c++)
        {
            if (_rented[c])
            {
                ScratchPool<int>.Shared.Return(_buffers[c]);
            }

            _rented[c] = false;
            _buffers[c] = [];
        }
    }

    /// <summary>Decodes every code-block into its tile-component buffer, in parallel for larger tiles.</summary>
    /// <param name="blocks">The block decoder used on the calling thread.</param>
    private void DecodeBlocks(JpxBlockState blocks)
    {
        var count = _tile.Blocks.Count;
        if (count >= ParallelBlocks && Environment.ProcessorCount > 1)
        {
            _ = Parallel.For(0, count, CreateDecoder, DecodeBlock, ReleaseDecoder);
            return;
        }

        for (var i = 0; i < count; i++)
        {
            _ = DecodeBlock(i, null, blocks);
        }
    }

    /// <summary>Decodes one code-block.</summary>
    /// <param name="index">The code-block index.</param>
    /// <param name="state">The parallel loop state, unused.</param>
    /// <param name="decoder">The worker's block decoder.</param>
    /// <returns>The same decoder, for the worker's next block.</returns>
    private JpxBlockState DecodeBlock(int index, ParallelLoopState? state, JpxBlockState decoder)
    {
        var component = _tile.Bands[_tile.Blocks[index].Band].Component;
        var info = _tile.Components[component];
        var lastResolution = _tile.Resolutions[info.FirstResolution + info.Levels - _reductionLevels];
        if (_tile.Blocks[index].Band < lastResolution.FirstBand + lastResolution.BandCount)
        {
            JpxBlockDecoder.Decode(decoder, _tile, index, _data, new(_buffers[component], info.Area.Reduce(_reductionLevels).Width, info.Reversible, info.RoiShift, info.BlockStyle));
        }

        return decoder;
    }

    /// <summary>Runs work for every component, in parallel when there are several large ones.</summary>
    /// <param name="work">The work for one component.</param>
    private void ForEachComponent(Action<int> work)
    {
        var area = _tile.Components[0].Area;
        if (_buffers.Length > 1 && (long)area.Width * area.Height >= ParallelSamples && Environment.ProcessorCount > 1)
        {
            _ = Parallel.For(0, _buffers.Length, work);
            return;
        }

        for (var c = 0; c < _buffers.Length; c++)
        {
            work(c);
        }
    }

    /// <summary>Runs the inverse wavelet transform of one component.</summary>
    /// <param name="component">The component index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InverseWavelet(int component) => JpxWavelet.Inverse(_tile, _tile.Components[component], _buffers[component], _scratches[component], _reductionLevels);

    /// <summary>Applies the inverse RCT or ICT to the first three tile-components when they have the same size.</summary>
    private void InverseComponentTransform()
    {
        if (_buffers.Length < TransformComponents)
        {
            return;
        }

        var area = _tile.Components[0].Area.Reduce(_reductionLevels);
        if (_tile.Components[1].Area.Reduce(_reductionLevels) != area || _tile.Components[ThirdComponent].Area.Reduce(_reductionLevels) != area)
        {
            return;
        }

        var length = area.Width * area.Height;
        if (_tile.Components[0].Reversible)
        {
            JpxComponentTransform.InverseReversible(_buffers[0].AsSpan(0, length), _buffers[1].AsSpan(0, length), _buffers[ThirdComponent].AsSpan(0, length));
            return;
        }

        JpxComponentTransform.InverseIrreversible(
            MemoryMarshal.Cast<int, float>(_buffers[0].AsSpan(0, length)),
            MemoryMarshal.Cast<int, float>(_buffers[1].AsSpan(0, length)),
            MemoryMarshal.Cast<int, float>(_buffers[ThirdComponent].AsSpan(0, length)));
    }

    /// <summary>Level-shifts one tile-component into its output plane.</summary>
    /// <param name="component">The component index.</param>
    private void Store(int component)
    {
        var info = _tile.Components[component];
        var area = info.Area.Reduce(_reductionLevels);
        var plane = _image.Areas[component];
        var range = JpxSampleRange.For(_geometry.Components[component]);
        var buffer = _buffers[component];
        var output = _image.Planes[component];
        for (var y = 0; y < area.Height; y++)
        {
            var source = buffer.AsSpan(y * area.Width, area.Width);
            var destination = output.AsSpan(((area.Y0 - plane.Y0 + y) * plane.Width) + area.X0 - plane.X0, area.Width);
            if (info.Reversible)
            {
                JpxComponentTransform.ShiftReversible(source, destination, range);
            }
            else
            {
                JpxComponentTransform.ShiftIrreversible(MemoryMarshal.Cast<int, float>(source), destination, range);
            }
        }
    }
}

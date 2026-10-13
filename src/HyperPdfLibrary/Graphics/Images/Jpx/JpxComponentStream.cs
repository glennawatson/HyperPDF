// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The rows of one tile-component, rebuilt a block at a time: the lowest resolution's sub-band, then one
/// <see cref="JpxLevelRows"/> for each wavelet level kept, each fed by three detail sub-bands. All the working memory of
/// the component is one array whose size depends on the component's width and the code-block height, not its height. The
/// objects are reused from tile to tile.
/// </summary>
[DebuggerDisplay("JpxComponentStream: {Width}x{Height}")]
internal sealed class JpxComponentStream : IDisposable
{
    /// <summary>The detail sub-bands of a resolution above the lowest.</summary>
    private const int DetailBands = 3;

    /// <summary>The sub-band objects: the lowest resolution's, then three for each level.</summary>
    private JpxBandRows[] _bands = [];

    /// <summary>The level objects.</summary>
    private JpxLevelRows[] _levels = [];

    /// <summary>The job that decodes code-blocks into strips.</summary>
    private JpxStripJob? _job;

    /// <summary>The cache and token of the decode.</summary>
    private JpxBlockWorkers? _workers;

    /// <summary>The working memory of every source.</summary>
    private int[] _arena = [];

    /// <summary>The source of the finished rows.</summary>
    private JpxRowSource _top = null!;

    /// <summary>Gets the samples in a row.</summary>
    internal int Width => _top.Width;

    /// <summary>Gets the rows.</summary>
    internal int Height => _top.Height;

    /// <summary>Gets a value indicating whether the next row is ready without decoding.</summary>
    internal bool IsReady => _top.IsReady;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Release();

    /// <summary>Points the stream at a tile-component.</summary>
    /// <param name="workers">The decoders, cache and token of the decode.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="bandStarts">Where each sub-band's code-blocks start in the tile, with the block count last.</param>
    /// <param name="component">The component index.</param>
    /// <param name="reductionLevels">The finest wavelet levels to leave out.</param>
    internal void Bind(JpxBlockWorkers workers, JpxTile tile, byte[] data, ReadOnlySpan<int> bandStarts, int component, int reductionLevels)
    {
        var info = tile.Components[component];
        var levels = info.Levels - reductionLevels;
        ArgumentOutOfRangeException.ThrowIfNegative(levels, nameof(reductionLevels));
        EnsureObjects(levels);
        _workers = workers;
        _job ??= new(workers);
        var lowest = tile.Resolutions[info.FirstResolution].FirstBand;
        _bands[0].Bind(_job, workers, tile, data, lowest, Blocks(bandStarts, lowest), info);
        JpxRowSource top = _bands[0];
        for (var r = 1; r <= levels; r++)
        {
            top = BindLevel(workers, tile, data, bandStarts, info, r, top);
        }

        _top = top;
        Attach(workers.Pools, levels);
    }

    /// <summary>Does the work for the next block of rows, if the next row starts one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Prepare() => _top.Prepare();

    /// <summary>Gets the next finished row.</summary>
    /// <returns>The samples of the row, as integers or the bits of floats, valid until the next call.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Span<int> NextRow() => _top.NextRow();

    /// <summary>Returns the working memory and forgets the tile.</summary>
    internal void Release()
    {
        foreach (var band in _bands)
        {
            band.Release();
        }

        foreach (var level in _levels)
        {
            level.Release();
        }

        if (_arena.Length > 0 && _workers is not null)
        {
            _workers.Pools.Get<int>().Return(_arena);
        }

        _arena = [];
        _job?.Clear();
        _workers = null;
    }

    /// <summary>Gets the code-block run of a sub-band.</summary>
    /// <param name="bandStarts">Where each sub-band's code-blocks start.</param>
    /// <param name="band">The sub-band index.</param>
    /// <returns>The run.</returns>
    private static JpxBlockRun Blocks(ReadOnlySpan<int> bandStarts, int band) => new(bandStarts[band], bandStarts[band + 1] - bandStarts[band]);

    /// <summary>Makes sure there are enough sub-band and level objects for a tile-component.</summary>
    /// <param name="levels">The wavelet levels kept.</param>
    private void EnsureObjects(int levels)
    {
        var bands = 1 + (DetailBands * levels);
        if (_bands.Length < bands)
        {
            var old = _bands.Length;
            Array.Resize(ref _bands, bands);
            for (var i = old; i < bands; i++)
            {
                _bands[i] = new();
            }
        }

        if (_levels.Length < levels)
        {
            var old = _levels.Length;
            Array.Resize(ref _levels, levels);
            for (var i = old; i < levels; i++)
            {
                _levels[i] = new();
            }
        }
    }

    /// <summary>Binds the sub-bands and the rebuild of one wavelet level.</summary>
    /// <param name="workers">The decoders, cache and token of the decode.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="bandStarts">Where each sub-band's code-blocks start.</param>
    /// <param name="info">The tile-component.</param>
    /// <param name="level">The level, counted from the lowest resolution as one.</param>
    /// <param name="lower">The rows of the resolution below.</param>
    /// <returns>The rows of the level.</returns>
    private JpxLevelRows BindLevel(JpxBlockWorkers workers, JpxTile tile, byte[] data, ReadOnlySpan<int> bandStarts, in JpxTileComponent info, int level, JpxRowSource lower)
    {
        var resolution = tile.Resolutions[info.FirstResolution + level];
        var below = tile.Resolutions[info.FirstResolution + level - 1];
        var first = 1 + (DetailBands * (level - 1));
        for (var b = 0; b < DetailBands; b++)
        {
            var band = resolution.FirstBand + b;
            _bands[first + b].Bind(_job!, workers, tile, data, band, Blocks(bandStarts, band), info);
        }

        var rows = _levels[level - 1];
        rows.Bind(workers, JpxLevelShape.Of(resolution.Area, below.Area), info.Reversible, lower, new(_bands[first], _bands[first + 1], _bands[first + DetailBands - 1]));
        return rows;
    }

    /// <summary>Rents the working memory and hands each source its part.</summary>
    /// <param name="pools">The cache.</param>
    /// <param name="levels">The wavelet levels kept.</param>
    private void Attach(ScratchPools pools, int levels)
    {
        var bands = 1 + (DetailBands * levels);
        var total = 0;
        for (var i = 0; i < bands; i++)
        {
            total += _bands[i].RequiredInts;
        }

        for (var i = 0; i < levels; i++)
        {
            total += _levels[i].RequiredInts;
        }

        _arena = pools.Get<int>().Rent(Math.Max(total, 1));
        var offset = 0;
        for (var i = 0; i < bands; i++)
        {
            _bands[i].Attach(_arena, offset);
            offset += _bands[i].RequiredInts;
        }

        for (var i = 0; i < levels; i++)
        {
            _levels[i].Attach(_arena, offset);
            offset += _levels[i].RequiredInts;
        }
    }
}

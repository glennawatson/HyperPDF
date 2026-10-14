// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// Encodes the tiles of an image one after another: it loads a tile's samples, applies the component transform and the
/// wavelet, codes every code-block (tier 1), and writes the packets (tier 2) as a tile-part. The working buffers are
/// rented once for the largest tile and reused.
/// </summary>
internal sealed class JpxTileEncoder : IDisposable
{
    /// <summary>The components the colour transform covers.</summary>
    private const int TransformComponents = 3;

    /// <summary>The cache for the pooled stores and tile buffers, shared by every encode so a steady stream of encodes reuses the same arrays.</summary>
    private static readonly ScratchPools Pools = new();

    /// <summary>The coding parameters.</summary>
    private readonly JpxEncodeSettings _settings;

    /// <summary>The image geometry.</summary>
    private readonly JpxGeometry _geometry;

    /// <summary>The tile coding parameters as a decoder resolves them.</summary>
    private readonly JpxTileParameters _parameters;

    /// <summary>The quantization of every sub-band.</summary>
    private readonly JpxQuantPlan _quantization;

    /// <summary>The tile layout.</summary>
    private readonly JpxTile _tile;

    /// <summary>The coded blocks of the tile.</summary>
    private readonly JpxTileCode _code;

    /// <summary>The block coder.</summary>
    private readonly JpxBlockEncoder _blocks;

    /// <summary>The packet writer.</summary>
    private readonly JpxPacketWriter _packets;

    /// <summary>The packets of the tile in order.</summary>
    private readonly JpxList<JpxPacketKey> _keys;

    /// <summary>The quantization index of every sub-band of the tile.</summary>
    private readonly JpxList<int> _bandQuantization;

    /// <summary>The samples of each component of the tile, as integers or the bits of floats.</summary>
    private readonly int[][] _planes;

    /// <summary>Working space for the wavelet, as large as a plane.</summary>
    private int[] _scratch;

    /// <summary>Initializes a new instance of the <see cref="JpxTileEncoder"/> class.</summary>
    /// <param name="settings">The coding parameters.</param>
    /// <param name="geometry">The image geometry.</param>
    /// <param name="parameters">The tile coding parameters.</param>
    /// <param name="quantization">The quantization of every sub-band.</param>
    internal JpxTileEncoder(JpxEncodeSettings settings, JpxGeometry geometry, JpxTileParameters parameters, JpxQuantPlan quantization)
    {
        _settings = settings;
        _geometry = geometry;
        _parameters = parameters;
        _quantization = quantization;
        _tile = new(settings.Components, Pools);
        _code = new(Pools);
        _blocks = new(Pools);
        _packets = new(settings, _tile, _code, Pools);
        _keys = new(Pools);
        _bandQuantization = new(Pools);
        var size = Math.Min(settings.TileWidth, settings.Width) * Math.Min(settings.TileHeight, settings.Height);
        _planes = new int[settings.Components][];
        for (var c = 0; c < _planes.Length; c++)
        {
            _planes[c] = Pools.Get<int>().Rent(size);
        }

        _scratch = Pools.Get<int>().Rent(size);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var plane in _planes)
        {
            Pools.Get<int>().Return(plane);
        }

        Pools.Get<int>().Return(_scratch);
        _scratch = [];
        _tile.Dispose();
        _code.Dispose();
        _blocks.Dispose();
        _packets.Dispose();
        _keys.Dispose();
        _bandQuantization.Dispose();
    }

    /// <summary>Encodes a tile and appends it to the codestream as one tile-part.</summary>
    /// <param name="samples">The interleaved image samples.</param>
    /// <param name="tileIndex">The tile index.</param>
    /// <param name="output">The codestream.</param>
    /// <param name="cancellationToken">Cancels between code-blocks.</param>
    /// <returns><see langword="false"/> when a code-block has more bit-planes than its sub-band allows, so the file needs more guard bits.</returns>
    /// <exception cref="InvalidOperationException">The tile is too finely divided to lay out.</exception>
    internal bool Encode(ReadOnlySpan<byte> samples, int tileIndex, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        if (!_tile.Build(_geometry, tileIndex, _parameters))
        {
            throw new InvalidOperationException("The tile has too many code-blocks to encode.");
        }

        _code.Reset(_tile.Blocks.Count);
        Transform(samples);
        CodeBlocks(cancellationToken);
        if (!_packets.Begin())
        {
            return false;
        }

        var start = JpxEncodeHeaders.WriteTileStart(ref output, tileIndex);
        JpxPacketOrder.Build(_tile, _geometry, _settings, _keys);
        foreach (var key in _keys.AsSpan())
        {
            _packets.Write(key, ref output);
        }

        JpxEncodeHeaders.FinishTilePart(ref output, start);
        return true;
    }

    /// <summary>Loads the tile's samples and runs the component transform and the wavelet on them.</summary>
    /// <param name="samples">The interleaved image samples.</param>
    private void Transform(ReadOnlySpan<byte> samples)
    {
        var area = _tile.Area;
        var length = area.Width * area.Height;
        for (var c = 0; c < _settings.Components; c++)
        {
            if (_settings.Reversible)
            {
                JpxTileSamples.LoadIntegers(samples, _settings, c, area, _planes[c].AsSpan(0, length));
            }
            else
            {
                JpxTileSamples.LoadFloats(samples, _settings, c, area, MemoryMarshal.Cast<int, float>(_planes[c].AsSpan(0, length)));
            }
        }

        if (_settings.UseTransform)
        {
            RunColourTransform(length);
        }

        for (var c = 0; c < _settings.Components; c++)
        {
            RunWavelet(c, length);
        }
    }

    /// <summary>Runs the component transform on the first three components.</summary>
    /// <param name="length">The samples in a tile-component.</param>
    private void RunColourTransform(int length)
    {
        if (_settings.Reversible)
        {
            JpxTileSamples.ForwardReversible(_planes[0].AsSpan(0, length), _planes[1].AsSpan(0, length), _planes[TransformComponents - 1].AsSpan(0, length));
            return;
        }

        JpxTileSamples.ForwardIrreversible(
            MemoryMarshal.Cast<int, float>(_planes[0].AsSpan(0, length)),
            MemoryMarshal.Cast<int, float>(_planes[1].AsSpan(0, length)),
            MemoryMarshal.Cast<int, float>(_planes[TransformComponents - 1].AsSpan(0, length)));
    }

    /// <summary>Runs the forward wavelet on one component.</summary>
    /// <param name="component">The component.</param>
    /// <param name="length">The samples in a tile-component.</param>
    private void RunWavelet(int component, int length)
    {
        var plane = _planes[component].AsSpan(0, length);
        var scratch = _scratch.AsSpan(0, length);
        if (_settings.Reversible)
        {
            JpxForwardWavelet53.Forward(_tile, _tile.Components[component], plane, scratch);
        }
        else
        {
            JpxForwardWavelet97.Forward(_tile, _tile.Components[component], MemoryMarshal.Cast<int, float>(plane), MemoryMarshal.Cast<int, float>(scratch));
        }
    }

    /// <summary>Codes every code-block of the tile.</summary>
    /// <param name="cancellationToken">Cancels between code-blocks.</param>
    private void CodeBlocks(CancellationToken cancellationToken)
    {
        FindBandQuantization();
        for (var i = 0; i < _tile.Blocks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var block = _tile.Blocks[i];
            if (block.Area.IsEmpty)
            {
                continue;
            }

            var band = _tile.Bands[block.Band];
            var window = new JpxBlockWindow(
                band.BufferX + block.Area.X0 - band.Area.X0,
                band.BufferY + block.Area.Y0 - band.Area.Y0,
                block.Area.Width,
                block.Area.Height,
                band.Orientation,
                _settings.Style);
            _blocks.Encode(LoadBlock(block.Band, window), _settings.Style, _code, i);
        }
    }

    /// <summary>Loads a code-block's coefficients, quantizing them for a lossy file.</summary>
    /// <param name="bandIndex">The index of the block's sub-band in the tile.</param>
    /// <param name="window">Where the block is in its tile-component.</param>
    /// <returns>The magnitude bit-planes of the block.</returns>
    private int LoadBlock(int bandIndex, in JpxBlockWindow window)
    {
        var component = _tile.Bands[bandIndex].Component;
        var area = _tile.Components[component].Area;
        var plane = _planes[component].AsSpan(0, area.Width * area.Height);
        return _settings.Reversible
            ? _blocks.Load(plane, area.Width, window)
            : _blocks.Load(MemoryMarshal.Cast<int, float>(plane), area.Width, window, 1F / _quantization.GetStep(_bandQuantization[bandIndex]));
    }

    /// <summary>Finds the quantization index of every sub-band of the tile.</summary>
    private void FindBandQuantization()
    {
        _bandQuantization.Clear();
        _ = _bandQuantization.AddDefault(_tile.Bands.Count);
        foreach (var component in _tile.Components)
        {
            for (var r = 0; r <= component.Levels; r++)
            {
                var layout = _tile.Resolutions[component.FirstResolution + r];
                for (var b = 0; b < layout.BandCount; b++)
                {
                    _bandQuantization[layout.FirstBand + b] = JpxQuantPlan.BandIndex(r, b + 1);
                }
            }
        }
    }
}

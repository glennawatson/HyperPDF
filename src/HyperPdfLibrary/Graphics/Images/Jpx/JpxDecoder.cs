// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decodes a JPEG 2000 codestream (ISO 15444-1) to component rows: for each tile, the tile-part headers, the packets
/// (tier 2), the code-blocks (tier 1), the inverse wavelet transform, the inverse component transform and the DC level
/// shift. The rows are asked for in order, and the work for a block of rows is done when the first of them is asked for,
/// so the memory in use depends on the width of the image and the size of its code-blocks, not on its height. A
/// codestream with more than one tile column keeps the tiles of the current tile row open together. Code-blocks may use the
/// high-throughput coder (T.814), chosen per tile-component by its coding style. Damaged tiles keep what decoded before
/// the damage; missing tiles are zero.
/// </summary>
[DebuggerDisplay("JpxDecoder: {Codestream.TileParts.Count} tile-parts")]
internal sealed class JpxDecoder : IDisposable
{
    /// <summary>The tile rows kept open at once before extra ones are made in a new list slot; two cover subsampled components whose tile rows start a row apart.</summary>
    private const int ExpectedOpenRows = 2;

    /// <summary>The cache, decoders and token of this decode.</summary>
    private readonly JpxBlockWorkers _workers;

    /// <summary>The tile rows with open tiles.</summary>
    private readonly List<TileRowSet> _open = [with(ExpectedOpenRows)];

    /// <summary>Row sets that are free to use for the next tile row.</summary>
    private readonly Stack<TileRowSet> _spareSets = [];

    /// <summary>Gets each wanted component's current tile row, or -1 before the first row.</summary>
    private int[] _tileRow = [];

    /// <summary>The first row of each component in its current tile row.</summary>
    private int[] _rowStart = [];

    /// <summary>The row after the last of each component in its current tile row.</summary>
    private int[] _rowEnd = [];

    /// <summary>Whether each component counts among the readers of its current tile row.</summary>
    private bool[] _holds = [];

    /// <summary>The row each component's assembled row buffer holds, or -1.</summary>
    private int[] _assembled = [];

    /// <summary>The row buffers of components that span several tile columns, or that are zero.</summary>
    private int[][] _rows = [];

    /// <summary>Whether the decoder is open for a codestream, so a second dispose does nothing.</summary>
    private bool _active;

    /// <summary>The tile-parts grouped by tile: where each tile's tile-parts start in <see cref="_order"/>, with the end last.</summary>
    private int[] _starts = [];

    /// <summary>The tile-part indices, tile after tile.</summary>
    private int[] _order = [];

    /// <summary>Initializes a new instance of the <see cref="JpxDecoder"/> class, which <see cref="Initialize"/> opens for a codestream.</summary>
    /// <param name="workers">The cache, decoders and token of the decodes this decoder serves.</param>
    private JpxDecoder(JpxBlockWorkers workers) => _workers = workers;

    /// <summary>Gets the codestream structure.</summary>
    internal JpxCodestream Codestream { get; private set; } = null!;

    /// <summary>Gets the packet reader.</summary>
    internal JpxPacketDecoder Packets { get; } = new();

    /// <summary>Gets the progression walker.</summary>
    internal JpxProgression Progression { get; private set; } = null!;

    /// <summary>Gets the finest wavelet levels left out.</summary>
    internal int ReductionLevels { get; private set; }

    /// <summary>Gets the components the caller reads.</summary>
    internal uint Wanted { get; private set; }

    /// <summary>Gets each component's area in its own sample grid at the decoded resolution.</summary>
    internal JpxRectangle[] Areas { get; private set; } = [];

    /// <summary>Gets the geometry of the image.</summary>
    internal JpxGeometry Geometry => Codestream.Geometry;

    /// <summary>Closes the decoder and hands it, with the tile streams and code-block decoders it used, back to the cache for the next decode.</summary>
    public void Dispose()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        foreach (var set in _open)
        {
            set.Close(_workers.SpareStreams);
        }

        _open.Clear();
        for (var c = 0; c < _rows.Length; c++)
        {
            ReturnArray(ref _rows[c]);
        }

        ReturnArray(ref _starts);
        ReturnArray(ref _order);
        Packets.Clear();
        Codestream = null!;
        _workers.Return();
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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The reduced component planes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JpxDecodedImage Decode(JpxCodestream codestream, ReadOnlySpan<byte> data, int reductionLevels, CancellationToken cancellationToken = default) =>
        Decode(codestream, data, reductionLevels, new(), cancellationToken);

    /// <summary>Decodes a codestream with an explicitly owned scratch cache.</summary>
    /// <param name="codestream">The parsed codestream.</param>
    /// <param name="data">The codestream bytes.</param>
    /// <param name="reductionLevels">The omitted finest levels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded component planes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JpxDecodedImage Decode(JpxCodestream codestream, ReadOnlySpan<byte> data, int reductionLevels, ScratchPools scratchPools, CancellationToken cancellationToken) =>
        Decode(codestream, data, reductionLevels, scratchPools, JpxComponentMask.All, cancellationToken);

    /// <summary>
    /// Decodes only the components the output reads, into whole planes. Callers that can use rows as they come should use
    /// <see cref="Open"/> instead, which never holds a whole plane.
    /// </summary>
    /// <param name="codestream">The parsed codestream.</param>
    /// <param name="data">The codestream bytes.</param>
    /// <param name="reductionLevels">The omitted finest levels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="wanted">The components to decode.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded planes; a component that was not wanted has an empty plane.</returns>
    internal static JpxDecodedImage Decode(JpxCodestream codestream, ReadOnlySpan<byte> data, int reductionLevels, ScratchPools scratchPools, uint wanted, CancellationToken cancellationToken)
    {
        var image = new JpxDecodedImage(codestream.Geometry, reductionLevels, scratchPools, wanted);
        try
        {
            using var decoder = Open(codestream, reductionLevels, scratchPools, wanted, cancellationToken);
            decoder.FillPlanes(data, image);
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>Opens a codestream to be read row by row.</summary>
    /// <param name="codestream">The parsed codestream.</param>
    /// <param name="reductionLevels">The omitted finest levels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="wanted">The components to decode.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoder; the caller disposes it.</returns>
    internal static JpxDecoder Open(JpxCodestream codestream, int reductionLevels, ScratchPools scratchPools, uint wanted, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(reductionLevels);
        var workers = JpxBlockWorkers.Rent(scratchPools, cancellationToken);
        var decoder = workers.Decoder ??= new(workers);
        try
        {
            decoder.Initialize(codestream, reductionLevels, wanted);
        }
        catch
        {
            decoder.Dispose();
            throw;
        }

        return decoder;
    }

    /// <summary>Gets one row of a component.</summary>
    /// <param name="component">The component, which must be among those the decoder was opened for.</param>
    /// <param name="row">
    /// The row in the component's area. Rows of a component are read in order, each at least once; rows of different
    /// components follow the picture, so a component with fewer rows is read less often.
    /// </param>
    /// <param name="data">The codestream, as given to the decoder's caller; every call must pass the same data.</param>
    /// <returns>The level-shifted samples, valid until the next row of the component is read.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The row is outside the component.</exception>
    internal ReadOnlySpan<int> GetRow(int component, int row, ReadOnlySpan<byte> data)
    {
        var area = Areas[component];
        if ((uint)row >= (uint)area.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        while (row >= _rowEnd[component])
        {
            NextTileRow(component);
        }

        var tileRow = _tileRow[component];
        var set = FindOpen(tileRow) ?? OpenRow(tileRow, data);
        var local = row - _rowStart[component];
        if (Codestream.Geometry.TilesAcross == 1)
        {
            return set.Streams[0] is { } only ? only.ReadRow(component, local) : ZeroRow(component, area.Width);
        }

        return Assemble(set, component, tileRow, local, row);
    }

    /// <summary>Opens the decoder for a codestream, keeping the arrays of an earlier decode with as many components.</summary>
    /// <param name="codestream">The codestream structure.</param>
    /// <param name="reductionLevels">The finest levels to omit.</param>
    /// <param name="wanted">The components the caller reads.</param>
    private void Initialize(JpxCodestream codestream, int reductionLevels, uint wanted)
    {
        _active = true;
        Codestream = codestream;
        ReductionLevels = reductionLevels;
        Wanted = wanted;
        var geometry = codestream.Geometry;
        if (Progression is null)
        {
            Progression = new(geometry, Packets, _workers.Pools);
        }
        else
        {
            Progression.Bind(geometry);
        }

        var components = geometry.Components.Length;
        if (_tileRow.Length != components)
        {
            Areas = new JpxRectangle[components];
            _tileRow = new int[components];
            _rowStart = new int[components];
            _rowEnd = new int[components];
            _holds = new bool[components];
            _assembled = new int[components];
            _rows = new int[components][];
        }

        for (var c = 0; c < components; c++)
        {
            Areas[c] = geometry.ToComponent(geometry.Image, c).Reduce(reductionLevels);
            _tileRow[c] = -1;
            _rowStart[c] = 0;
            _rowEnd[c] = 0;
            _holds[c] = false;
            _assembled[c] = -1;
            _rows[c] ??= [];
        }

        GroupByTile();
    }

    /// <summary>Takes a tile stream for the next tile: an idle one built for this many components, or a new one.</summary>
    /// <returns>The stream.</returns>
    private JpxTileStream TakeStream()
    {
        var geometry = Codestream.Geometry;
        while (_workers.SpareStreams.TryPop(out var idle))
        {
            if (idle.ComponentCount == geometry.Components.Length)
            {
                idle.Bind(geometry);
                return idle;
            }

            idle.Dispose();
        }

        return new(geometry, _workers);
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

    /// <summary>Sorts the tile-part indices by tile, keeping codestream order within a tile.</summary>
    private void GroupByTile()
    {
        var parts = Codestream.TileParts;
        var tiles = Codestream.Geometry.TilesAcross * Codestream.Geometry.TilesDown;
        var pool = _workers.Pools.Get<int>();
        _starts = pool.Rent(tiles + 1);
        _order = pool.Rent(Math.Max(parts.Count, 1));
        _starts.AsSpan(0, tiles + 1).Clear();
        foreach (var part in parts)
        {
            _starts[part.Tile + 1]++;
        }

        for (var t = 0; t < tiles; t++)
        {
            _starts[t + 1] += _starts[t];
        }

        // Each tile's next free slot starts at its start and moves one along per tile-part placed.
        var next = pool.Rent(tiles);
        _starts.AsSpan(0, tiles).CopyTo(next);
        for (var i = 0; i < parts.Count; i++)
        {
            var tile = parts[i].Tile;
            _order[next[tile]] = i;
            next[tile]++;
        }

        pool.Return(next);
    }

    /// <summary>Gets the rows a component has in a tile row.</summary>
    /// <param name="component">The component.</param>
    /// <param name="tileRow">The tile row.</param>
    /// <returns>The first row and the row after the last, in the component's area.</returns>
    private TileRowRange RowRange(int component, int tileRow)
    {
        var geometry = Codestream.Geometry;
        var area = geometry.ToComponent(geometry.GetTile(tileRow * geometry.TilesAcross), component).Reduce(ReductionLevels);
        return new(area.Y0 - Areas[component].Y0, area.Y1 - Areas[component].Y0);
    }

    /// <summary>Moves a component to its next tile row that has rows, letting go of the tile row it leaves.</summary>
    /// <param name="component">The component.</param>
    /// <exception cref="InvalidOperationException">The component has no tile rows left.</exception>
    private void NextTileRow(int component)
    {
        if (_holds[component])
        {
            Leave(_tileRow[component]);
            _holds[component] = false;
        }

        var tileRow = _tileRow[component] + 1;
        if (tileRow >= Codestream.Geometry.TilesDown)
        {
            throw new InvalidOperationException("The component has no more rows.");
        }

        var range = RowRange(component, tileRow);
        _tileRow[component] = tileRow;
        _rowStart[component] = range.Start;
        _rowEnd[component] = range.End;
        _holds[component] = range.End > range.Start;
    }

    /// <summary>Finds the open tiles of a tile row.</summary>
    /// <param name="tileRow">The tile row.</param>
    /// <returns>The set, or <see langword="null"/> when the row is not open.</returns>
    private TileRowSet? FindOpen(int tileRow)
    {
        foreach (var set in _open)
        {
            if (set.Row == tileRow)
            {
                return set;
            }
        }

        return null;
    }

    /// <summary>Opens the tiles of a tile row.</summary>
    /// <param name="tileRow">The tile row.</param>
    /// <param name="data">The codestream.</param>
    /// <returns>The set of open tiles.</returns>
    private TileRowSet OpenRow(int tileRow, ReadOnlySpan<byte> data)
    {
        var across = Codestream.Geometry.TilesAcross;
        var set = _spareSets.TryPop(out var spare) ? spare : new();
        set.Begin(tileRow, across, CountReaders(tileRow));
        for (var column = 0; column < across; column++)
        {
            var tile = (tileRow * across) + column;
            var from = _starts[tile];
            var count = _starts[tile + 1] - from;
            if (count == 0)
            {
                continue;
            }

            _workers.Token.ThrowIfCancellationRequested();
            var stream = TakeStream();
            if (stream.Start(this, tile, _order.AsSpan(from, count), data))
            {
                set.Streams[column] = stream;
            }
            else
            {
                stream.Release();
                _workers.SpareStreams.Push(stream);
            }
        }

        _open.Add(set);
        return set;
    }

    /// <summary>Counts the wanted components that have rows in a tile row.</summary>
    /// <param name="tileRow">The tile row.</param>
    /// <returns>The number of readers.</returns>
    private int CountReaders(int tileRow)
    {
        var readers = 0;
        for (var c = 0; c < Areas.Length; c++)
        {
            var range = RowRange(c, tileRow);
            if (JpxComponentMask.Contains(Wanted, c) && range.End > range.Start)
            {
                readers++;
            }
        }

        return readers;
    }

    /// <summary>Lets a component go from a tile row; the row closes when its last reader leaves.</summary>
    /// <param name="tileRow">The tile row.</param>
    private void Leave(int tileRow)
    {
        var set = FindOpen(tileRow);
        if (set is null || set.Leave() > 0)
        {
            return;
        }

        _ = _open.Remove(set);
        set.Close(_workers.SpareStreams);
        _spareSets.Push(set);
    }

    /// <summary>Gets a row of zeros, for a tile that has no data.</summary>
    /// <param name="component">The component.</param>
    /// <param name="width">The row width.</param>
    /// <returns>The zeros.</returns>
    private ReadOnlySpan<int> ZeroRow(int component, int width)
    {
        EnsureRow(component, width);
        var zeros = _rows[component].AsSpan(0, width);
        zeros.Clear();
        _assembled[component] = -1;
        return zeros;
    }

    /// <summary>Makes sure a component has a row buffer.</summary>
    /// <param name="component">The component.</param>
    /// <param name="width">The row width.</param>
    private void EnsureRow(int component, int width)
    {
        if (_rows[component].Length >= width)
        {
            return;
        }

        ReturnArray(ref _rows[component]);
        _rows[component] = _workers.Pools.Get<int>().Rent(Math.Max(width, 1));
    }

    /// <summary>Joins the rows of the tiles across a tile row into one row of the component.</summary>
    /// <param name="set">The open tiles of the tile row.</param>
    /// <param name="component">The component.</param>
    /// <param name="tileRow">The tile row.</param>
    /// <param name="local">The row within the tile row.</param>
    /// <param name="row">The row within the component.</param>
    /// <returns>The row.</returns>
    private ReadOnlySpan<int> Assemble(TileRowSet set, int component, int tileRow, int local, int row)
    {
        var width = Areas[component].Width;
        EnsureRow(component, width);
        var joined = _rows[component].AsSpan(0, width);
        if (_assembled[component] == row)
        {
            return joined;
        }

        var geometry = Codestream.Geometry;
        for (var column = 0; column < geometry.TilesAcross; column++)
        {
            var area = geometry.ToComponent(geometry.GetTile((tileRow * geometry.TilesAcross) + column), component).Reduce(ReductionLevels);
            if (area.Width == 0)
            {
                continue;
            }

            var destination = joined.Slice(area.X0 - Areas[component].X0, area.Width);
            if (set.Streams[column] is { } stream)
            {
                stream.ReadRow(component, local).CopyTo(destination);
            }
            else
            {
                destination.Clear();
            }
        }

        _assembled[component] = row;
        return joined;
    }

    /// <summary>Reads every row of every wanted component into planes, following the picture so that components of different sizes advance together.</summary>
    /// <param name="data">The codestream.</param>
    /// <param name="image">The planes.</param>
    private void FillPlanes(ReadOnlySpan<byte> data, JpxDecodedImage image)
    {
        var rows = 0;
        for (var c = 0; c < Areas.Length; c++)
        {
            rows = Math.Max(rows, Areas[c].Height);
        }

        Span<int> last = Areas.Length <= 32 ? stackalloc int[Areas.Length] : new int[Areas.Length];
        last.Fill(-1);
        for (var y = 0; y < rows; y++)
        {
            for (var c = 0; c < Areas.Length; c++)
            {
                var area = Areas[c];
                var row = (int)((long)y * area.Height / rows);
                if (!JpxComponentMask.Contains(Wanted, c) || area.IsEmpty || row == last[c])
                {
                    continue;
                }

                last[c] = row;
                GetRow(c, row, data).CopyTo(image.Planes[c].AsSpan(row * area.Width, area.Width));
            }
        }
    }

    /// <summary>A component's rows in a tile row.</summary>
    /// <param name="Start">The first row.</param>
    /// <param name="End">The row after the last.</param>
    private readonly record struct TileRowRange(int Start, int End);

    /// <summary>The open tiles of one tile row, and how many components still read them.</summary>
    private sealed class TileRowSet
    {
        /// <summary>Gets the tile row.</summary>
        internal int Row { get; private set; }

        /// <summary>Gets the tile streams, by tile column; null for a tile with no data.</summary>
        internal JpxTileStream?[] Streams { get; private set; } = [];

        /// <summary>Gets or sets the components that have not yet left the tile row.</summary>
        private int Readers { get; set; }

        /// <summary>Starts the set for a tile row.</summary>
        /// <param name="row">The tile row.</param>
        /// <param name="columns">The tile columns.</param>
        /// <param name="readers">The components that read the tile row.</param>
        internal void Begin(int row, int columns, int readers)
        {
            Row = row;
            Readers = readers;
            if (Streams.Length != columns)
            {
                Streams = new JpxTileStream?[columns];
            }
            else
            {
                Array.Clear(Streams);
            }
        }

        /// <summary>Records that a component has left the tile row.</summary>
        /// <returns>The components still reading.</returns>
        internal int Leave()
        {
            Readers--;
            return Readers;
        }

        /// <summary>Releases the tile streams for the next tile row.</summary>
        /// <param name="spare">Receives the streams.</param>
        internal void Close(Stack<JpxTileStream> spare)
        {
            for (var i = 0; i < Streams.Length; i++)
            {
                if (Streams[i] is not { } stream)
                {
                    continue;
                }

                stream.Release();
                spare.Push(stream);
                Streams[i] = null;
            }
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Gives the dequantized coefficients of one sub-band row by row (ISO/IEC 15444-1 annex E). The code-blocks of a band form
/// a grid; the source decodes one row of that grid at a time into a strip as tall as a code-block, so a band never needs
/// more memory than its width times the code-block height, however tall the image is. A code-block that carries no data
/// leaves its part of the strip zero.
/// </summary>
[DebuggerDisplay("JpxBandRows: {Width}x{Height}, row {_next}")]
internal sealed class JpxBandRows : JpxRowSource
{
    /// <summary>The bits of a key that hold the block's column; the row goes above them.</summary>
    private const int KeyShift = 32;

    /// <summary>The samples in a row.</summary>
    private int _width;

    /// <summary>The number of rows.</summary>
    private int _height;

    /// <summary>The job that decodes the code-blocks of a strip.</summary>
    private JpxStripJob _job = null!;

    /// <summary>The cache the code-block order is rented from.</summary>
    private JpxBlockWorkers _workers = null!;

    /// <summary>The tile the band belongs to.</summary>
    private JpxTile _tile = null!;

    /// <summary>The tile's packet data.</summary>
    private byte[] _data = [];

    /// <summary>The sub-band.</summary>
    private JpxBandLayout _band;

    /// <summary>Whether the 5/3 reversible path is used.</summary>
    private bool _reversible;

    /// <summary>The region-of-interest up-shift, or zero.</summary>
    private int _roiShift;

    /// <summary>The code-block mode switches.</summary>
    private JpxBlockStyle _style;

    /// <summary>The code-block indices in row-major order.</summary>
    private int[] _order = [];

    /// <summary>The number of code-blocks of the band.</summary>
    private int _orderCount;

    /// <summary>The position in <see cref="_order"/> of the first code-block not yet decoded.</summary>
    private int _cursor;

    /// <summary>The array the strip lives in.</summary>
    private int[] _arena = [];

    /// <summary>The offset of the strip in <see cref="_arena"/>.</summary>
    private int _offset;

    /// <summary>The most rows a strip holds.</summary>
    private int _capacityRows;

    /// <summary>The band row the strip starts at.</summary>
    private int _stripTop;

    /// <summary>The rows in the strip.</summary>
    private int _stripRows;

    /// <summary>The next band row to give.</summary>
    private int _next;

    /// <inheritdoc/>
    internal override int Width => _width;

    /// <inheritdoc/>
    internal override int Height => _height;

    /// <summary>Gets the ints the strip needs in the arena.</summary>
    internal int RequiredInts => Width * _capacityRows;

    /// <inheritdoc/>
    internal override bool IsReady => _next < _stripTop + _stripRows;

    /// <summary>Points the source at a sub-band of a tile.</summary>
    /// <param name="job">The job that decodes the code-blocks of a strip.</param>
    /// <param name="workers">The cache and token of the decode.</param>
    /// <param name="tile">The tile.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="bandIndex">The sub-band index.</param>
    /// <param name="blocks">The code-blocks of the band: the first index and the count.</param>
    /// <param name="info">The tile-component the band belongs to.</param>
    internal void Bind(JpxStripJob job, JpxBlockWorkers workers, JpxTile tile, byte[] data, int bandIndex, JpxBlockRun blocks, in JpxTileComponent info)
    {
        _job = job;
        _workers = workers;
        _tile = tile;
        _data = data;
        _band = tile.Bands[bandIndex];
        _reversible = info.Reversible;
        _roiShift = info.RoiShift;
        _style = info.BlockStyle;
        _width = _band.Area.Width;
        _height = _band.Area.Height;
        _next = 0;
        _stripTop = 0;
        _stripRows = 0;
        _cursor = 0;
        var empty = Width == 0 || Height == 0;
        _capacityRows = empty ? 0 : Math.Min(Height, 1 << _band.BlockHeightExponent);
        _orderCount = empty ? 0 : blocks.Count;
        if (_orderCount > 0)
        {
            BuildOrder(blocks.First);
        }
    }

    /// <summary>Gives the source the memory for its strip.</summary>
    /// <param name="arena">The array.</param>
    /// <param name="offset">Where the strip starts in the array.</param>
    internal void Attach(int[] arena, int offset)
    {
        _arena = arena;
        _offset = offset;
    }

    /// <inheritdoc/>
    internal override void Prepare()
    {
        if (!IsReady && _next < Height)
        {
            LoadStrip();
        }
    }

    /// <inheritdoc/>
    internal override Span<int> NextRow()
    {
        if (_next >= Height)
        {
            throw new InvalidOperationException("The sub-band has no more rows.");
        }

        if (Width == 0)
        {
            _next++;
            return default;
        }

        Prepare();
        var row = _arena.AsSpan(_offset + ((_next - _stripTop) * Width), Width);
        _next++;
        return row;
    }

    /// <inheritdoc/>
    internal override void Release()
    {
        if (_order.Length > 0)
        {
            _workers.Pools.Get<int>().Return(_order);
            _order = [];
        }

        _arena = [];
        _tile = null!;
        _data = [];
        _job = null!;
        _workers = null!;
    }

    /// <summary>
    /// Lists the band's code-blocks row by row. The blocks are made precinct by precinct, which is already row-major when
    /// the band has one column of precincts; otherwise they are sorted.
    /// </summary>
    /// <param name="first">The index of the band's first code-block.</param>
    private void BuildOrder(int first)
    {
        var pool = _workers.Pools.Get<int>();
        _order = pool.Rent(_orderCount);
        var order = _order.AsSpan(0, _orderCount);
        var sorted = true;
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = first + i;
            sorted = sorted && (i == 0 || IsAfter(order[i - 1], order[i]));
        }

        if (sorted)
        {
            return;
        }

        var keyPool = _workers.Pools.Get<long>();
        var keys = keyPool.Rent(_orderCount);
        for (var i = 0; i < order.Length; i++)
        {
            var area = _tile.Blocks[order[i]].Area;
            keys[i] = ((long)area.Y0 << KeyShift) | (uint)area.X0;
        }

        MemoryExtensions.Sort(keys.AsSpan(0, _orderCount), order);
        keyPool.Return(keys);
    }

    /// <summary>Determines whether a code-block comes after another in row-major order.</summary>
    /// <param name="previous">The earlier code-block.</param>
    /// <param name="block">The later code-block.</param>
    /// <returns><see langword="true"/> when the block is on a lower row, or to the right on the same row.</returns>
    private bool IsAfter(int previous, int block)
    {
        var before = _tile.Blocks[previous].Area;
        var after = _tile.Blocks[block].Area;
        return after.Y0 > before.Y0 || (after.Y0 == before.Y0 && after.X0 > before.X0);
    }

    /// <summary>Decodes the next row of code-blocks into the strip, or makes a strip of zeros where no code-block covers.</summary>
    private void LoadStrip()
    {
        _workers.Token.ThrowIfCancellationRequested();
        SkipBlocksAbove();
        var runEnd = _cursor;
        var top = _next;
        var rows = Math.Min(Height - _next, _capacityRows);
        if (_cursor < _orderCount)
        {
            var firstTop = _tile.Blocks[_order[_cursor]].Area.Y0 - _band.Area.Y0;
            if (firstTop > _next)
            {
                rows = Math.Min(firstTop - _next, _capacityRows);
            }
            else
            {
                runEnd = FindRunEnd(firstTop, out rows);
            }
        }

        _stripTop = top;
        _stripRows = rows;
        _arena.AsSpan(_offset, Width * rows).Clear();
        DecodeRun(runEnd, top);
    }

    /// <summary>Decodes the code-blocks from the cursor up to a position into the strip.</summary>
    /// <param name="runEnd">The position after the last code-block.</param>
    /// <param name="top">The band row the strip starts at.</param>
    private void DecodeRun(int runEnd, int top)
    {
        if (runEnd <= _cursor)
        {
            return;
        }

        JpxBlockTarget target = new(_arena, Width, _reversible, _roiShift, _style);

        // The store adds the buffer position to each block's position in the band, so these two put the band's first
        // sample at the strip's first sample.
        var placed = _band with { BufferX = _offset, BufferY = -top };
        _job.Run(_tile, _data, target, placed, _order, _cursor, runEnd - _cursor);
        _cursor = runEnd;
    }

    /// <summary>Steps past code-blocks that end above the next row, which a damaged layout could leave behind.</summary>
    private void SkipBlocksAbove()
    {
        while (_cursor < _orderCount && _tile.Blocks[_order[_cursor]].Area.Y1 - _band.Area.Y0 <= _next)
        {
            _cursor++;
        }
    }

    /// <summary>Finds the end of the code-blocks that start on the same row as the one at the cursor.</summary>
    /// <param name="firstTop">The band row the code-blocks start on.</param>
    /// <param name="rows">Receives the rows the strip needs: down to the lowest edge of the run, within the strip's capacity.</param>
    /// <returns>The position after the last code-block of the run.</returns>
    private int FindRunEnd(int firstTop, out int rows)
    {
        var end = _cursor;
        var bottom = 0;
        while (end < _orderCount)
        {
            var area = _tile.Blocks[_order[end]].Area;
            if (area.Y0 - _band.Area.Y0 != firstTop)
            {
                break;
            }

            bottom = Math.Max(bottom, area.Y1 - _band.Area.Y0);
            end++;
        }

        rows = Math.Clamp(bottom - _next, 1, Math.Min(Height - _next, _capacityRows));
        return end;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Rebuilds one resolution level of a tile-component row by row (ISO/IEC 15444-1 annex F, equation F-1 onward). The level
/// comes from the next lower resolution and the HL, LH and HH sub-bands. Each source row is rebuilt horizontally once.
/// The vertical filter then runs over blocks of <see cref="BlockRows"/> output rows, each with the few rows around it
/// that the filter reads: running the filter on that window gives the same numbers as running it down the whole column,
/// because a filter step only looks a fixed distance away and the window edges at the top and bottom of the image mirror
/// as the standard says. The rows both neighbouring windows need are kept raw, so no source row is asked for twice.
/// </summary>
[DebuggerDisplay("JpxLevelRows: {Width}x{Height}, row {_nextOut}")]
internal sealed class JpxLevelRows : JpxRowSource
{
    /// <summary>The output rows built together.</summary>
    internal const int BlockRows = 32;

    /// <summary>The sets of rows a level keeps: low-pass and high-pass.</summary>
    private const int Rings = 2;

    /// <summary>The rows kept for the next window beyond the margin: the margin covers two windows' overlap, and rounding adds up to two.</summary>
    private const int TailSlack = 2;

    /// <summary>The ring for the low-pass rows: the next lower resolution and the HL sub-band.</summary>
    private readonly Ring _lowRing = new();

    /// <summary>The ring for the high-pass rows: the LH and HH sub-bands.</summary>
    private readonly Ring _highRing = new();

    /// <summary>The samples in a row.</summary>
    private int _width;

    /// <summary>The number of rows.</summary>
    private int _height;

    /// <summary>The cache and token of the decode.</summary>
    private JpxBlockWorkers _workers = null!;

    /// <summary>Whether the 5/3 reversible filter is used.</summary>
    private bool _reversible;

    /// <summary>The rows each side of a block that the vertical filter reads.</summary>
    private int _margin;

    /// <summary>The low-pass columns.</summary>
    private int _lowWidth;

    /// <summary>The high-pass columns.</summary>
    private int _highWidth;

    /// <summary>1 when the level starts on an odd column.</summary>
    private int _columnParity;

    /// <summary>1 when the level starts on an odd row.</summary>
    private int _rowParity;

    /// <summary>The array the working rows live in.</summary>
    private int[] _arena = [];

    /// <summary>The rows each of the two work matrices holds.</summary>
    private int _matrixRows;

    /// <summary>The rows each of the two tails holds.</summary>
    private int _tailRows;

    /// <summary>The next output row to give.</summary>
    private int _nextOut;

    /// <summary>The row after the last row of the block built.</summary>
    private int _blockEnd;

    /// <summary>The first row of the window the block was filtered in.</summary>
    private int _windowStart;

    /// <summary>1 when the window starts with a high-pass row.</summary>
    private int _windowParity;

    /// <inheritdoc/>
    internal override int Width => _width;

    /// <inheritdoc/>
    internal override int Height => _height;

    /// <summary>Gets the ints the working rows need in the arena.</summary>
    internal int RequiredInts => Width * Rings * (_matrixRows + _tailRows);

    /// <inheritdoc/>
    internal override bool IsReady => Width == 0 || _nextOut < _blockEnd;

    /// <summary>Points the source at the pieces of a level.</summary>
    /// <param name="workers">The cache and token of the decode.</param>
    /// <param name="shape">The shape of the level.</param>
    /// <param name="reversible">Whether the 5/3 reversible filter is used.</param>
    /// <param name="lower">The next lower resolution.</param>
    /// <param name="details">The HL, LH and HH sub-bands.</param>
    internal void Bind(JpxBlockWorkers workers, in JpxLevelShape shape, bool reversible, JpxRowSource lower, in JpxDetailBands details)
    {
        _workers = workers;
        _reversible = reversible;
        _margin = JpxWavelet.RowMargin(reversible);
        _width = shape.Width;
        _height = shape.Height;
        _lowWidth = shape.LowWidth;
        _highWidth = shape.HighWidth;
        _columnParity = shape.ColumnParity;
        _rowParity = shape.RowParity;
        _lowRing.Bind(lower, details.HorizontalHigh);
        _highRing.Bind(details.VerticalHigh, details.Diagonal);
        _nextOut = 0;
        _blockEnd = 0;
        _windowStart = 0;
        _windowParity = 0;
        var empty = Width == 0 || Height == 0;
        var window = Math.Min(Height, BlockRows + (Rings * _margin));
        _matrixRows = empty ? 0 : ((window + 1) / Rings) + 1;
        _tailRows = empty ? 0 : Math.Min(_matrixRows, _margin + TailSlack);
    }

    /// <summary>Gives the source the memory for its working rows.</summary>
    /// <param name="arena">The array.</param>
    /// <param name="offset">Where the working rows start in the array.</param>
    internal void Attach(int[] arena, int offset)
    {
        _arena = arena;
        var matrix = Width * _matrixRows;
        var tail = Width * _tailRows;
        _lowRing.Place(offset, offset + (Rings * matrix));
        _highRing.Place(offset + matrix, offset + (Rings * matrix) + tail);
    }

    /// <inheritdoc/>
    internal override void Prepare()
    {
        if (Width > 0 && _nextOut >= _blockEnd && _nextOut < Height)
        {
            ProduceBlock();
        }
    }

    /// <inheritdoc/>
    internal override Span<int> NextRow()
    {
        if (_nextOut >= Height)
        {
            throw new InvalidOperationException("The resolution has no more rows.");
        }

        if (Width == 0)
        {
            _nextOut++;
            return default;
        }

        Prepare();
        var t = _nextOut - _windowStart;
        _nextOut++;
        var isLow = ((t + _windowParity) & 1) == 0;
        var ring = isLow ? _lowRing : _highRing;
        var index = (t - (isLow ? _windowParity : 1 - _windowParity)) >> 1;
        return _arena.AsSpan(ring.MatrixOffset + (index * Width), Width);
    }

    /// <inheritdoc/>
    internal override void Release()
    {
        _arena = [];
        _workers = null!;
        _lowRing.Clear();
        _highRing.Clear();
    }

    /// <summary>Gets how many low-pass rows come before an output row.</summary>
    /// <param name="row">The output row.</param>
    /// <returns>The low-pass rows among the rows before it; low-pass rows are the even rows of the reference grid.</returns>
    private int LowsBefore(int row) => (row - _rowParity + 1) >> 1;

    /// <summary>Builds the next block of output rows: gathers the window's rows, keeps the rows the next window shares, and filters.</summary>
    private void ProduceBlock()
    {
        _workers.Token.ThrowIfCancellationRequested();
        var first = _blockEnd;
        var end = Math.Min(Height, first + BlockRows);
        var windowStart = Math.Max(0, first - _margin);
        var windowEnd = Math.Min(Height, end + _margin);
        var lowFrom = LowsBefore(windowStart);
        var lowTo = LowsBefore(windowEnd);
        var highFrom = windowStart - lowFrom;
        var highTo = windowEnd - lowTo;
        FillRows(_lowRing, lowFrom, lowTo);
        FillRows(_highRing, highFrom, highTo);
        if (end < Height)
        {
            var nextStart = Math.Max(0, end - _margin);
            var nextLow = LowsBefore(nextStart);
            _lowRing.SaveTail(_arena, Width, lowFrom, nextLow, lowTo);
            _highRing.SaveTail(_arena, Width, highFrom, nextStart - nextLow, highTo);
        }
        else
        {
            _lowRing.DropTail();
            _highRing.DropTail();
        }

        _windowStart = windowStart;
        _windowParity = (_rowParity + windowStart) & 1;
        Lift(lowTo - lowFrom, highTo - highFrom);
        _blockEnd = end;
    }

    /// <summary>Puts the horizontally rebuilt rows <paramref name="from"/> up to <paramref name="to"/> of a set into its work matrix.</summary>
    /// <param name="ring">The set.</param>
    /// <param name="from">The first row.</param>
    /// <param name="to">The row after the last.</param>
    /// <exception cref="InvalidOperationException">The sources were read out of step with the rows asked for.</exception>
    private void FillRows(Ring ring, int from, int to)
    {
        for (var k = from; k < to; k++)
        {
            var destination = _arena.AsSpan(ring.MatrixOffset + ((k - from) * Width), Width);
            if (ring.HasTailRow(k))
            {
                _arena.AsSpan(ring.TailOffset + ((k - ring.TailStart) * Width), Width).CopyTo(destination);
                continue;
            }

            if (k != ring.Next)
            {
                throw new InvalidOperationException("The sub-band rows are asked for out of order.");
            }

            RebuildRow(ring, destination);
        }
    }

    /// <summary>Rebuilds one row horizontally from the next row of each of a set's two sources (equations F-5 and F-6 along the row).</summary>
    /// <param name="ring">The set.</param>
    /// <param name="destination">Receives the row.</param>
    /// <exception cref="InvalidOperationException">The sources' rows are not the widths of the resolution.</exception>
    private void RebuildRow(Ring ring, Span<int> destination)
    {
        var lows = ring.Lows.NextRow();
        var highs = ring.Highs.NextRow();
        if (lows.Length != _lowWidth || highs.Length != _highWidth)
        {
            throw new InvalidOperationException("The sub-band rows do not have the widths of the resolution.");
        }

        if (_reversible)
        {
            JpxWavelet.SynthesizeRow53(lows, highs, _columnParity, destination);
        }
        else
        {
            JpxWavelet.SynthesizeRow97(MemoryMarshal.Cast<int, float>(lows), MemoryMarshal.Cast<int, float>(highs), _columnParity, MemoryMarshal.Cast<int, float>(destination));
        }

        ring.Next++;
    }

    /// <summary>Runs the vertical filter over the window's rows.</summary>
    /// <param name="lowRows">The low-pass rows in the window.</param>
    /// <param name="highRows">The high-pass rows in the window.</param>
    private void Lift(int lowRows, int highRows)
    {
        var lows = _arena.AsSpan(_lowRing.MatrixOffset, lowRows * Width);
        var highs = _arena.AsSpan(_highRing.MatrixOffset, highRows * Width);
        if (_reversible)
        {
            JpxWavelet.LiftRows53(lows, highs, lowRows, highRows, _windowParity, Width);
            return;
        }

        JpxWavelet.LiftRows97(MemoryMarshal.Cast<int, float>(lows), MemoryMarshal.Cast<int, float>(highs), lowRows, highRows, _windowParity, Width);
    }

    /// <summary>One set of rows of a level: the sources they are rebuilt from, where the window's rows and the rows kept for the next window live, and how far the sources have been read.</summary>
    private sealed class Ring
    {
        /// <summary>Gets the source of the low-pass columns.</summary>
        internal JpxRowSource Lows { get; private set; } = null!;

        /// <summary>Gets the source of the high-pass columns.</summary>
        internal JpxRowSource Highs { get; private set; } = null!;

        /// <summary>Gets or sets the number of rows rebuilt from the sources so far.</summary>
        internal int Next { get; set; }

        /// <summary>Gets the first row kept for the next window.</summary>
        internal int TailStart { get; private set; }

        /// <summary>Gets the rows kept for the next window.</summary>
        internal int TailCount { get; private set; }

        /// <summary>Gets the offset of the window's rows in the arena.</summary>
        internal int MatrixOffset { get; private set; }

        /// <summary>Gets the offset of the kept rows in the arena.</summary>
        internal int TailOffset { get; private set; }

        /// <summary>Points the set at its sources and forgets what it read.</summary>
        /// <param name="lows">The source of the low-pass columns.</param>
        /// <param name="highs">The source of the high-pass columns.</param>
        internal void Bind(JpxRowSource lows, JpxRowSource highs)
        {
            Lows = lows;
            Highs = highs;
            Next = 0;
            TailStart = 0;
            TailCount = 0;
        }

        /// <summary>Sets where the set's rows live.</summary>
        /// <param name="matrix">The offset of the window's rows.</param>
        /// <param name="tail">The offset of the kept rows.</param>
        internal void Place(int matrix, int tail)
        {
            MatrixOffset = matrix;
            TailOffset = tail;
        }

        /// <summary>Determines whether a row is among the rows kept from the last window.</summary>
        /// <param name="row">The row.</param>
        /// <returns><see langword="true"/> when it is kept.</returns>
        internal bool HasTailRow(int row) => row >= TailStart && row < TailStart + TailCount;

        /// <summary>Keeps the window's rows from <paramref name="keepFrom"/> on, for the next window.</summary>
        /// <param name="arena">The array.</param>
        /// <param name="width">The samples in a row.</param>
        /// <param name="windowFrom">The first row of the window.</param>
        /// <param name="keepFrom">The first row to keep.</param>
        /// <param name="windowTo">The row after the window's last.</param>
        internal void SaveTail(int[] arena, int width, int windowFrom, int keepFrom, int windowTo)
        {
            var rows = Math.Max(windowTo - keepFrom, 0);
            if (rows > 0)
            {
                arena.AsSpan(MatrixOffset + ((keepFrom - windowFrom) * width), rows * width).CopyTo(arena.AsSpan(TailOffset, rows * width));
            }

            TailStart = keepFrom;
            TailCount = rows;
        }

        /// <summary>Keeps no rows.</summary>
        internal void DropTail() => TailCount = 0;

        /// <summary>Forgets the sources.</summary>
        internal void Clear()
        {
            Lows = null!;
            Highs = null!;
            TailCount = 0;
        }
    }
}

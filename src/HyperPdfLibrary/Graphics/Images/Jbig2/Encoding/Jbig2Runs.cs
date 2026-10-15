// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>
/// The runs of black pixels on a page, row by row, joined into 8-connected sets with a union-find. A run's parent is never
/// after it, so the first run of a set in reading order is its root.
/// Use one value through a local variable and <see langword="ref"/> parameters; copies would not share the run count.
/// </summary>
internal record struct Jbig2Runs : IDisposable
{
    /// <summary>The most runs a page may have.</summary>
    internal const int MaximumRuns = 1 << 22;

    /// <summary>The smallest number of runs the run arrays start with.</summary>
    private const int MinimumRuns = 256;

    /// <summary>The runs per pixel row the arrays are first sized for.</summary>
    private const int RunsPerRow = 16;

    /// <summary>The value of a byte with every bit set.</summary>
    private const uint AllBits = 0xFF;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The leading zero bits of a byte held in a 32-bit word.</summary>
    private const int WordPadding = 24;

    /// <summary>The factor the run arrays grow by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The index of the first run of each row, with one more entry for the end.</summary>
    private readonly int[] _rowStart;

    /// <summary>The first column of each run.</summary>
    private int[] _start;

    /// <summary>The column after the last of each run.</summary>
    private int[] _end;

    /// <summary>The union-find parent of each run, later the component number of each run.</summary>
    private int[] _parent;

    /// <summary>The number of runs.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Runs"/> struct.</summary>
    /// <param name="height">The page height.</param>
    internal Jbig2Runs(int height)
    {
        // A text page has a few runs per pixel row; starting there avoids regrowing the arrays.
        var initial = Math.Clamp(height * RunsPerRow, MinimumRuns, MaximumRuns);
        _start = ArrayPool<int>.Shared.Rent(initial);
        _end = ArrayPool<int>.Shared.Rent(initial);
        _parent = ArrayPool<int>.Shared.Rent(initial);
        _rowStart = ArrayPool<int>.Shared.Rent(height + 1);
        _count = 0;
    }

    /// <summary>Returns the rented arrays.</summary>
    public void Dispose()
    {
        ArrayPool<int>.Shared.Return(_start);
        ArrayPool<int>.Shared.Return(_end);
        ArrayPool<int>.Shared.Return(_parent);
        ArrayPool<int>.Shared.Return(_rowStart);
    }

    /// <summary>Gets the index of the first run of a row.</summary>
    /// <param name="y">The row, up to the height.</param>
    /// <returns>The run index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int RowStart(int y) => _rowStart[y];

    /// <summary>Gets the first column of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The column.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Start(int run) => _start[run];

    /// <summary>Gets the column after the last of a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The column.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int End(int run) => _end[run];

    /// <summary>Gets the component of a run, once <see cref="Measure"/> has run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The component number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int ComponentOf(int run) => _parent[run];

    /// <summary>Finds the runs of every row and joins runs that touch, diagonals included.</summary>
    /// <param name="ink">The packed rows.</param>
    /// <param name="size">The page size.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns><see langword="false"/> when there are too many runs.</returns>
    internal bool TryFill(ReadOnlySpan<byte> ink, Jbig2Size size, CancellationToken cancellationToken)
    {
        var stride = Jbig2Bits.Stride(size.Width);
        var previousStart = 0;
        for (var y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _rowStart[y] = _count;
            if (!AddRow(ink.Slice(y * stride, stride), size.Width))
            {
                return false;
            }

            Join(previousStart, _rowStart[y], _count);
            previousStart = _rowStart[y];
        }

        _rowStart[size.Height] = _count;

        // A run's parent is never after it, so one pass in order leaves every run pointing at its root.
        for (var run = 0; run < _count; run++)
        {
            _parent[run] = _parent[_parent[run]];
        }

        return true;
    }

    /// <summary>Counts the runs that are the root of a set, once <see cref="TryFill"/> has run.</summary>
    /// <returns>The number of components.</returns>
    internal int CountRoots()
    {
        var roots = 0;
        for (var i = 0; i < _count; i++)
        {
            roots += _parent[i] == i ? 1 : 0;
        }

        return roots;
    }

    /// <summary>
    /// Numbers the components, measures them and replaces each run's parent by its component number. The components
    /// are numbered in the order of their first run, which is reading order.
    /// </summary>
    /// <param name="measures">Receives each component's left edge, top edge, width, height and black pixel count.</param>
    internal void Measure(in Jbig2ComponentMeasures measures)
    {
        var next = 0;
        var y = 0;
        for (var run = 0; run < _count; run++)
        {
            while (_rowStart[y + 1] <= run)
            {
                y++;
            }

            if (_parent[run] == run)
            {
                measures.Start(next, _start[run], y, _end[run]);
                _parent[run] = next;
                next++;
            }
            else
            {
                // The root comes before this run, so it already holds its component number.
                _parent[run] = _parent[_parent[run]];
                measures.Extend(_parent[run], _start[run], _end[run], y);
            }

            measures.AddInk(_parent[run], _end[run] - _start[run]);
        }

        measures.Finish(next);
    }

    /// <summary>Finds the next pixel with a value.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="from">The first column to look at.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="black">Whether to look for a black pixel; otherwise for a white one.</param>
    /// <returns>The column, or the width when there is none.</returns>
    private static int FindNext(ReadOnlySpan<byte> row, int from, int width, bool black)
    {
        if (from >= width)
        {
            return width;
        }

        var index = from >> Jbig2Bits.ByteShift;
        var value = (black ? row[index] : (byte)~row[index]) & (AllBits >> (from & Jbig2Bits.BitMask));
        while (value == 0)
        {
            index++;
            if (index >= row.Length)
            {
                return width;
            }

            value = black ? row[index] : (byte)~row[index];
        }

        return Math.Min(width, (index * ByteBits) + BitOperations.LeadingZeroCount(value) - WordPadding);
    }

    /// <summary>Adds the runs of one row.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="width">The width in pixels.</param>
    /// <returns><see langword="false"/> when the run limit is reached.</returns>
    private bool AddRow(ReadOnlySpan<byte> row, int width)
    {
        var x = FindNext(row, 0, width, true);
        while (x < width)
        {
            var end = FindNext(row, x, width, false);
            if (_count == MaximumRuns)
            {
                return false;
            }

            if (_count == _start.Length)
            {
                Grow();
            }

            _start[_count] = x;
            _end[_count] = end;
            _parent[_count] = _count;
            _count++;
            x = FindNext(row, end, width, true);
        }

        return true;
    }

    /// <summary>Joins the runs of a row to the touching runs of the row above.</summary>
    /// <param name="previous">The index of the first run of the row above.</param>
    /// <param name="current">The index of the first run of the row.</param>
    /// <param name="end">The index after the last run of the row.</param>
    private void Join(int previous, int current, int end)
    {
        var scan = previous;
        for (var run = current; run < end; run++)
        {
            // A previous run that ends left of this one cannot touch it, nor can it touch the runs after.
            while (scan < current && _end[scan] < _start[run])
            {
                scan++;
            }

            for (var other = scan; other < current && _start[other] <= _end[run]; other++)
            {
                Union(run, other);
            }
        }
    }

    /// <summary>Joins the sets of two runs.</summary>
    /// <param name="first">The first run.</param>
    /// <param name="second">The second run.</param>
    private void Union(int first, int second)
    {
        var a = Root(first);
        var b = Root(second);
        if (a != b)
        {
            // The smaller index becomes the root, so a set's root is its first run in reading order.
            _parent[Math.Max(a, b)] = Math.Min(a, b);
        }
    }

    /// <summary>Finds the root of a run's set, halving the path as it goes.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The root run.</returns>
    private int Root(int run)
    {
        while (_parent[run] != run)
        {
            _parent[run] = _parent[_parent[run]];
            run = _parent[run];
        }

        return run;
    }

    /// <summary>Doubles the run arrays.</summary>
    private void Grow()
    {
        var length = Math.Min(MaximumRuns, _start.Length * GrowthFactor);
        _start = Replace(_start, length);
        _end = Replace(_end, length);
        _parent = Replace(_parent, length);
    }

    /// <summary>Copies an array into a larger rented one and returns the old.</summary>
    /// <param name="array">The array.</param>
    /// <param name="length">The new length.</param>
    /// <returns>The larger array.</returns>
    private int[] Replace(int[] array, int length)
    {
        var larger = ArrayPool<int>.Shared.Rent(length);
        array.AsSpan(0, _count).CopyTo(larger);
        ArrayPool<int>.Shared.Return(array);
        return larger;
    }
}

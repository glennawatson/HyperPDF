// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Works out how much of each pixel a shape covers. Every edge adds the signed area it sweeps to the pixels it crosses, and a running
/// sum along each line then gives the coverage, exact for any slope. One pass over the edges and one over the pixels, with no sorting.
/// </summary>
[DebuggerDisplay("CoverageBuffer: {Width}x{Height} at {Left},{Top}")]
internal sealed class CoverageBuffer : IDisposable
{
    /// <summary>The extra cells after each line, so an edge that ends on the right border writes inside the buffer.</summary>
    private const int Padding = 2;

    /// <summary>Half, the mean of two ends.</summary>
    private const float Half = 0.5F;

    /// <summary>One and a half, from the area of the second pixel an edge crosses.</summary>
    private const float OneAndHalf = 1.5F;

    /// <summary>The largest coverage byte.</summary>
    private const float Full = 255F;

    /// <summary>The cells between the first and the last pixel of an edge that crosses exactly three pixels.</summary>
    private const int ThreePixels = 2;

    /// <summary>The signed area each cell gained, a line after another.</summary>
    private float[] _area = [];

    /// <summary>The first cell touched on each line.</summary>
    private int[] _first = [];

    /// <summary>One past the last cell touched on each line.</summary>
    private int[] _last = [];

    /// <summary>Gets the x of the buffer's left edge in device pixels.</summary>
    internal int Left { get; private set; }

    /// <summary>Gets the y of the buffer's top edge in device pixels.</summary>
    internal int Top { get; private set; }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Return();
        Width = 0;
        Height = 0;
    }

    /// <summary>Starts a shape over a rectangle of pixels. Edges outside it still count for the pixels inside.</summary>
    /// <param name="left">The x of the left edge.</param>
    /// <param name="top">The y of the top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    internal void Begin(int left, int top, int width, int height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        var cells = (width + Padding) * height;
        if (_area.Length < cells || _first.Length < height)
        {
            Return();
            _area = ArrayPool<float>.Shared.Rent(cells);
            _first = ArrayPool<int>.Shared.Rent(height);
            _last = ArrayPool<int>.Shared.Rent(height);

            // Rented memory holds old values. Every line read is cleared as it is read, so the buffer stays clean from here on.
            _area.AsSpan().Clear();
        }

        _first.AsSpan(0, height).Fill(int.MaxValue);
        _last.AsSpan(0, height).Clear();
    }

    /// <summary>Adds the edges of the shape.</summary>
    /// <param name="edges">The edges in device pixels.</param>
    internal void Add(EdgeList edges)
    {
        var values = edges.Values;
        for (var i = 0; i < values.Length; i += EdgeList.Stride)
        {
            AddEdge(values[i] - Left, values[i + 1] - Top, values[i + 1 + 1] - Left, values[i + EdgeList.Stride - 1] - Top);
        }
    }

    /// <summary>Reads the coverage of one line and clears the line for the next shape.</summary>
    /// <param name="row">The line, from the top of the buffer.</param>
    /// <param name="evenOdd">Whether the even-odd rule fills the shape rather than the nonzero winding rule.</param>
    /// <param name="coverage">Receives one byte a pixel between the returned bounds, 0 for uncovered and 255 for covered.</param>
    /// <returns>The pixels that may be covered: from the first cell an edge touched to the end of the line, or an empty range.</returns>
    internal PixelRange ReadRow(int row, bool evenOdd, Span<byte> coverage)
    {
        var first = _first[row];
        if (first >= Width)
        {
            // Nothing inside the buffer, but the padding cells may hold what an edge on the right border left there.
            ClearPadding(row);
            return default;
        }

        var last = Math.Min(_last[row], Width);
        var line = _area.AsSpan(row * (Width + Padding), Width + Padding);
        var sum = 0F;
        var x = first;
        while (x < last)
        {
            var cell = line[x];
            if (cell == 0)
            {
                // No edge crosses these cells, so the sum and the coverage stay as they are: the inside of a wide shape is
                // one fill, and its cells are already clear.
                var run = ZeroRun(line[x..last]);
                coverage.Slice(x, run).Fill(ToByte(sum, evenOdd));
                x += run;
                continue;
            }

            sum += cell;
            line[x] = 0;
            coverage[x] = ToByte(sum, evenOdd);
            x++;
        }

        ClearPadding(row);

        // Past the last edge the sum no longer changes. It is not zero when the shape runs on beyond the right border.
        var tail = ToByte(sum, evenOdd);
        if (tail == 0)
        {
            return new(first, last);
        }

        coverage[last..Width].Fill(tail);
        return new(first, Width);
    }

    /// <summary>Counts the cells at the start of a line that no edge added area to.</summary>
    /// <param name="cells">The cells, the first of which is zero.</param>
    /// <returns>The number of cells, at least one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ZeroRun(ReadOnlySpan<float> cells)
    {
        // The bits are compared, which the runtime does many cells at a time. A negative zero has a bit set and ends the run,
        // so the first cell, which the caller found equal to zero, always counts.
        var run = MemoryMarshal.Cast<float, uint>(cells).IndexOfAnyExcept(0U);
        return run < 0 ? cells.Length : Math.Max(run, 1);
    }

    /// <summary>Turns a signed area sum into a coverage byte.</summary>
    /// <param name="sum">The sum: the winding number where a pixel is wholly inside or outside.</param>
    /// <param name="evenOdd">Whether the even-odd rule applies.</param>
    /// <returns>The coverage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToByte(float sum, bool evenOdd)
    {
        var covered = MathF.Abs(sum);
        if (evenOdd)
        {
            // Even winding numbers are outside and odd ones inside, so the coverage folds back every second unit.
            covered -= MathF.Floor(covered * Half) * (1 + 1);
            covered = covered > 1F ? 1 + 1 - covered : covered;
        }

        return (byte)((MathF.Min(covered, 1F) * Full) + Half);
    }

    /// <summary>Adds the area an edge sweeps to the cells it crosses in one line.</summary>
    /// <param name="line">The cells of the line.</param>
    /// <param name="low">The smaller x of the edge in the line.</param>
    /// <param name="high">The larger x.</param>
    /// <param name="area">The height of the edge in the line, signed.</param>
    private static void Accumulate(Span<float> line, float low, float high, float area)
    {
        var lowFloor = MathF.Floor(low);
        var firstCell = (int)lowFloor;
        var highCeiling = MathF.Ceiling(high);
        var lastCell = (int)highCeiling;
        if (lastCell <= firstCell + 1)
        {
            // The edge stays in one pixel: that pixel is covered right of the edge's middle, and the next one fully.
            var middle = (Half * (low + high)) - lowFloor;
            line[firstCell] += area - (area * middle);
            line[firstCell + 1] += area * middle;
            return;
        }

        var perPixel = 1F / (high - low);
        var lowPart = low - lowFloor;
        var firstArea = Half * perPixel * (1F - lowPart) * (1F - lowPart);
        var highPart = high - highCeiling + 1F;
        var lastArea = Half * perPixel * highPart * highPart;
        line[firstCell] += area * firstArea;
        if (lastCell == firstCell + ThreePixels)
        {
            line[firstCell + 1] += area * (1F - firstArea - lastArea);
        }
        else
        {
            var secondArea = perPixel * (OneAndHalf - lowPart);
            line[firstCell + 1] += area * (secondArea - firstArea);
            var whole = area * perPixel;
            foreach (ref var cell in line.Slice(firstCell + ThreePixels, lastCell - 1 - firstCell - ThreePixels))
            {
                // Every pixel between the second and the last is crossed from side to side and gains the same area.
                cell += whole;
            }

            var before = secondArea + ((lastCell - firstCell - ThreePixels - 1) * perPixel);
            line[lastCell - 1] += area * (1F - before - lastArea);
        }

        line[lastCell] += area * lastArea;
    }

    /// <summary>Adds one edge, in the buffer's own coordinates.</summary>
    /// <param name="x0">The x of the start.</param>
    /// <param name="y0">The y of the start.</param>
    /// <param name="x1">The x of the end.</param>
    /// <param name="y1">The y of the end.</param>
    private void AddEdge(float x0, float y0, float x1, float y1)
    {
        // An edge that goes down the page adds, and one that goes up takes away. The work is then done top to bottom.
        var direction = 1F;
        if (y0 > y1)
        {
            direction = -1F;
            var endX = x0;
            var endY = y0;
            x0 = x1;
            y0 = y1;
            x1 = endX;
            y1 = endY;
        }

        if (y1 <= 0 || y0 >= Height || !float.IsFinite(x0 + x1 + y0 + y1))
        {
            return;
        }

        var slope = (x1 - x0) / (y1 - y0);
        var x = y0 < 0 ? x0 - (y0 * slope) : x0;
        var firstRow = Math.Max(0, (int)y0);
        var lastRow = Math.Min(Height, (int)MathF.Ceiling(y1));
        for (var row = firstRow; row < lastRow; row++)
        {
            var dy = MathF.Min(row + 1, y1) - MathF.Max(row, y0);
            var next = x + (slope * dy);
            AddPiece(row, x, next, dy * direction);
            x = next;
        }
    }

    /// <summary>Adds the part of an edge inside one line of pixels, cutting off what lies left or right of the buffer.</summary>
    /// <param name="row">The line.</param>
    /// <param name="from">The x where the edge enters the line.</param>
    /// <param name="to">The x where it leaves.</param>
    /// <param name="area">The height of the part, negative for an edge that goes up.</param>
    private void AddPiece(int row, float from, float to, float area)
    {
        var low = MathF.Min(from, to);
        var high = MathF.Max(from, to);
        if (low >= Width)
        {
            // Wholly right of the buffer: it changes no pixel inside.
            return;
        }

        var line = _area.AsSpan(row * (Width + Padding), Width + Padding);
        if (high <= 0)
        {
            // Wholly left of the buffer: every pixel of the line is to its right, so the line starts with the whole height.
            line[0] += area;
            _first[row] = 0;
            _last[row] = Math.Max(_last[row], 1);
            return;
        }

        var span = high - low;
        if (low < 0)
        {
            // The share left of the border acts as an upright edge on the border.
            var outside = -low / span;
            line[0] += area * outside;
            area -= area * outside;
            low = 0;
        }

        if (high > Width)
        {
            // The share right of the border changes nothing inside.
            area -= area * ((high - Width) / span);
            high = Width;
        }

        Accumulate(line, low, high, area);
        _first[row] = Math.Min(_first[row], (int)low);

        // An edge writes to the pixel it ends in and the one after, so both are read and cleared.
        _last[row] = Math.Max(_last[row], Math.Min(Width, (int)high + 1 + 1));
    }

    /// <summary>Clears the cells after the last pixel of a line.</summary>
    /// <param name="row">The line.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ClearPadding(int row) => _area.AsSpan((row * (Width + Padding)) + Width, Padding).Clear();

    /// <summary>Gives the buffers back to the pool.</summary>
    private void Return()
    {
        if (_area.Length == 0)
        {
            return;
        }

        ArrayPool<float>.Shared.Return(_area);
        ArrayPool<int>.Shared.Return(_first);
        ArrayPool<int>.Shared.Return(_last);
        _area = [];
        _first = [];
        _last = [];
    }
}

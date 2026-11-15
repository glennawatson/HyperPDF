// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Viewer.Text;

/// <summary>
/// Turns closed outlines into per-pixel coverage. Every edge adds its signed area to the pixels it crosses, and a running sum
/// along each row then gives how much of each pixel is inside the outline. One buffer is reused for every piece of text.
/// </summary>
[DebuggerDisplay("CoverageBuffer: {Width} x {Height}")]
internal sealed class CoverageBuffer
{
    /// <summary>The cells kept past the end of a row, so an edge on the right border has somewhere to put its remainder.</summary>
    private const int RowPadding = 2;

    /// <summary>Half, for the area of a triangle and the middle of an edge.</summary>
    private const float Half = 0.5F;

    /// <summary>One and a half, from the area of the second pixel of a wide edge.</summary>
    private const float OneAndHalf = 1.5F;

    /// <summary>The pixels a wide edge covers before its even middle run starts.</summary>
    private const int WideEdgeLead = 2;

    /// <summary>The pixels of a wide edge that are not part of its even middle run.</summary>
    private const int WideEdgeEnds = 3;

    /// <summary>The largest coverage value.</summary>
    private const float Full = 255F;

    /// <summary>The rise below which an edge counts as level.</summary>
    private const float Flat = 1e-6F;

    /// <summary>The signed areas, one row after another.</summary>
    private float[] _cells = [];

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; private set; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; private set; }

    /// <summary>Gets the cells of one row, including the padding.</summary>
    private int RowLength => Width + RowPadding;

    /// <summary>Clears the buffer for a new size.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal void Reset(int width, int height)
    {
        Width = width;
        Height = height;
        var needed = (width + RowPadding) * height;
        if (_cells.Length < needed)
        {
            _cells = new float[needed];
            return;
        }

        _cells.AsSpan(0, needed).Clear();
    }

    /// <summary>Adds one edge of an outline.</summary>
    /// <param name="fromX">The x of the start in pixels.</param>
    /// <param name="fromY">The y of the start in pixels, growing downwards.</param>
    /// <param name="toX">The x of the end.</param>
    /// <param name="toY">The y of the end.</param>
    internal void Line(float fromX, float fromY, float toX, float toY)
    {
        if (MathF.Abs(toY - fromY) < Flat)
        {
            // A level edge covers no height, so it adds nothing.
            return;
        }

        var direction = fromY < toY ? 1F : -1F;
        var topX = fromY < toY ? fromX : toX;
        var topY = Math.Min(fromY, toY);
        var bottomY = Math.Min(Math.Max(fromY, toY), Height);
        var slope = (toX - fromX) / (toY - fromY);
        if (topY < 0)
        {
            topX -= topY * slope;
            topY = 0;
        }

        var x = topX;
        var cells = _cells.AsSpan();
        var lastRow = (int)MathF.Ceiling(bottomY);
        for (var row = (int)topY; row < lastRow; row++)
        {
            var covered = Math.Min(row + 1, bottomY) - Math.Max(row, topY);
            var next = x + (slope * covered);
            AddSpan(cells.Slice(row * RowLength, RowLength), Clamp(x), Clamp(next), covered * direction);
            x = next;
        }
    }

    /// <summary>Writes the coverage of one row as bytes from 0 to 255.</summary>
    /// <param name="row">The row.</param>
    /// <param name="destination">Receives one byte for every pixel of the row.</param>
    internal void ReadRow(int row, Span<byte> destination)
    {
        var cells = _cells.AsSpan(row * RowLength, Width);
        var sum = 0F;
        for (var i = 0; i < cells.Length; i++)
        {
            sum += cells[i];
            destination[i] = (byte)(Math.Min(MathF.Abs(sum), 1F) * Full);
        }
    }

    /// <summary>Adds the part of an edge that lies in one row.</summary>
    /// <param name="row">The cells of the row.</param>
    /// <param name="startX">The x where the edge enters the row.</param>
    /// <param name="endX">The x where the edge leaves the row.</param>
    /// <param name="amount">The height of the row the edge covers, negative for an edge that runs upwards.</param>
    private static void AddSpan(Span<float> row, float startX, float endX, float amount)
    {
        var left = Math.Min(startX, endX);
        var right = Math.Max(startX, endX);
        var leftFloor = MathF.Floor(left);
        var first = (int)leftFloor;
        var rightCeiling = MathF.Ceiling(right);
        var last = (int)rightCeiling;
        if (last <= first + 1)
        {
            var middle = (Half * (startX + endX)) - leftFloor;
            row[first] += amount - (amount * middle);
            row[first + 1] += amount * middle;
            return;
        }

        var perPixel = 1F / (right - left);
        var leftInset = left - leftFloor;
        var firstArea = Half * perPixel * (1F - leftInset) * (1F - leftInset);
        var rightInset = right - rightCeiling + 1F;
        var lastArea = Half * perPixel * rightInset * rightInset;
        row[first] += amount * firstArea;
        if (last == first + WideEdgeLead)
        {
            row[first + 1] += amount * (1F - firstArea - lastArea);
        }
        else
        {
            var secondArea = perPixel * (OneAndHalf - leftInset);
            row[first + 1] += amount * (secondArea - firstArea);
            for (var i = first + WideEdgeLead; i < last - 1; i++)
            {
                row[i] += amount * perPixel;
            }

            var beforeLast = secondArea + ((last - first - WideEdgeEnds) * perPixel);
            row[last - 1] += amount * (1F - beforeLast - lastArea);
        }

        row[last] += amount * lastArea;
    }

    /// <summary>Keeps an x inside the row, so an outline that runs past the border cannot write outside the buffer.</summary>
    /// <param name="x">The x in pixels.</param>
    /// <returns>The x, no less than 0 and no more than the width.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float Clamp(float x) => Math.Clamp(x, 0F, Width);
}

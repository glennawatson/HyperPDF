// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds the columns of a run of text pieces by projecting them onto the horizontal axis. Narrow pieces are added first,
/// so a piece that spans two columns, such as a heading over both, is recognised by touching two columns and is left out.
/// </summary>
internal static class LayoutColumnBands
{
    /// <summary>Finds the columns.</summary>
    /// <param name="span">The lines; only lines of text count.</param>
    /// <param name="bands">Receives the columns, left to right; at least as long as <paramref name="span"/>.</param>
    /// <returns>The number of columns.</returns>
    internal static int Find(ReadOnlySpan<LayoutLine> span, ColumnBand[] bands)
    {
        var widths = ArrayPool<float>.Shared.Rent(span.Length);
        var order = ArrayPool<int>.Shared.Rent(span.Length);
        try
        {
            var count = 0;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].Kind != LayoutLineKind.Text)
                {
                    continue;
                }

                widths[count] = span[i].Bounds.Width;
                order[count] = i;
                count++;
            }

            widths.AsSpan(0, count).Sort(order.AsSpan(0, count));
            var columns = 0;
            for (var k = 0; k < count; k++)
            {
                var bounds = span[order[k]].Bounds;
                columns = Add(bands, columns, bounds.Left, bounds.Right);
            }

            return columns;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(widths);
            ArrayPool<int>.Shared.Return(order);
        }
    }

    /// <summary>Finds the first column a piece touches, or the nearest column when it touches none.</summary>
    /// <param name="bands">The columns.</param>
    /// <param name="piece">The piece's box.</param>
    /// <returns>The column.</returns>
    internal static int First(ReadOnlySpan<ColumnBand> bands, PdfViewerRect piece)
    {
        for (var i = 0; i < bands.Length; i++)
        {
            if (Touches(bands[i], piece))
            {
                return i;
            }
        }

        return Nearest(bands, piece);
    }

    /// <summary>Finds the last column a piece touches, or the nearest column when it touches none.</summary>
    /// <param name="bands">The columns.</param>
    /// <param name="piece">The piece's box.</param>
    /// <returns>The column.</returns>
    internal static int Last(ReadOnlySpan<ColumnBand> bands, PdfViewerRect piece)
    {
        for (var i = bands.Length - 1; i >= 0; i--)
        {
            if (Touches(bands[i], piece))
            {
                return i;
            }
        }

        return Nearest(bands, piece);
    }

    /// <summary>Determines whether a piece overlaps a column.</summary>
    /// <param name="band">The column.</param>
    /// <param name="piece">The piece's box.</param>
    /// <returns><see langword="true"/> when they overlap.</returns>
    private static bool Touches(in ColumnBand band, PdfViewerRect piece) => piece.Left < band.Right && piece.Right > band.Left;

    /// <summary>Finds the column whose middle is nearest a piece's middle.</summary>
    /// <param name="bands">The columns.</param>
    /// <param name="piece">The piece's box.</param>
    /// <returns>The column.</returns>
    private static int Nearest(ReadOnlySpan<ColumnBand> bands, PdfViewerRect piece)
    {
        var best = 0;
        var distance = float.MaxValue;
        for (var i = 0; i < bands.Length; i++)
        {
            var away = Math.Abs(((bands[i].Left + bands[i].Right) / LayoutGeometry.Halves) - piece.CenterX);
            if (away >= distance)
            {
                continue;
            }

            distance = away;
            best = i;
        }

        return best;
    }

    /// <summary>Adds a piece to the columns: it starts a column, widens one, or is skipped when it touches two.</summary>
    /// <param name="bands">The columns, sorted by position.</param>
    /// <param name="count">The columns so far.</param>
    /// <param name="left">The piece's left edge.</param>
    /// <param name="right">The piece's right edge.</param>
    /// <returns>The number of columns after the piece.</returns>
    private static int Add(ColumnBand[] bands, int count, float left, float right)
    {
        var touched = -1;
        var touches = 0;
        var insertAt = count;
        for (var i = 0; i < count; i++)
        {
            if (left < bands[i].Right && right > bands[i].Left)
            {
                touched = i;
                touches++;
            }
            else if (right <= bands[i].Left && insertAt == count)
            {
                insertAt = i;
            }
        }

        if (touches > 1)
        {
            return count;
        }

        if (touches == 1)
        {
            bands[touched] = new(Math.Min(bands[touched].Left, left), Math.Max(bands[touched].Right, right));
            return count;
        }

        Array.Copy(bands, insertAt, bands, insertAt + 1, count - insertAt);
        bands[insertAt] = new(left, right);
        return count + 1;
    }
}

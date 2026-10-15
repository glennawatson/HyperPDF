// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Tells how the lines of a paragraph are aligned from where their edges fall. Lines that all end at one edge, with
/// the other edge ragged, are aligned to that edge; lines that share a centre are centred; lines that reach both edges
/// except the last are justified. One line is compared with the column it sits in.
/// </summary>
internal static class LayoutAlignment
{
    /// <summary>How far edges may differ, in body text heights, and still line up.</summary>
    private const float EdgeTolerance = 0.5F;

    /// <summary>How far, in body text heights, an edge must be from the column's edge for a single line to count as moved in from it.</summary>
    private const float SingleLineMargin = 4F;

    /// <summary>The fewest lines of a justified paragraph, so that two lines that happen to be as long are not taken for justified.</summary>
    private const int MinJustifiedLines = 3;

    /// <summary>The ragged edge's spread, in tolerances, that shows it is ragged.</summary>
    private const float RaggedSpread = 2F;

    /// <summary>Which edge of a line is compared.</summary>
    private enum Edge
    {
        /// <summary>The left edge.</summary>
        Left = 0,

        /// <summary>The right edge.</summary>
        Right = 1,

        /// <summary>The middle.</summary>
        Centre = 2,
    }

    /// <summary>Works out a paragraph's alignment.</summary>
    /// <param name="lines">The paragraph's lines, top to bottom.</param>
    /// <param name="column">The box around the lines of the column or region the paragraph sits in.</param>
    /// <param name="bodyHeight">The body text height.</param>
    /// <returns>The alignment.</returns>
    internal static PdfTextAlignment Detect(ReadOnlySpan<LayoutLine> lines, PdfViewerRect column, float bodyHeight)
    {
        var tolerance = EdgeTolerance * bodyHeight;
        return lines.Length switch
        {
            0 => PdfTextAlignment.Start,
            1 => Single(lines[0].Bounds, column, tolerance, bodyHeight),
            _ => Many(lines, tolerance),
        };
    }

    /// <summary>Compares one line with the column it sits in.</summary>
    /// <param name="line">The line's box.</param>
    /// <param name="column">The column's box.</param>
    /// <param name="tolerance">How far edges may differ and still line up.</param>
    /// <param name="bodyHeight">The body text height.</param>
    /// <returns>The alignment.</returns>
    private static PdfTextAlignment Single(PdfViewerRect line, PdfViewerRect column, float tolerance, float bodyHeight)
    {
        var margin = SingleLineMargin * bodyHeight;
        var leftGap = line.Left - column.Left;
        var rightGap = column.Right - line.Right;
        if (leftGap < margin && rightGap < margin)
        {
            return PdfTextAlignment.Start;
        }

        if (Math.Abs(leftGap - rightGap) <= tolerance)
        {
            return PdfTextAlignment.Center;
        }

        return rightGap <= tolerance && leftGap >= margin ? PdfTextAlignment.End : PdfTextAlignment.Start;
    }

    /// <summary>Compares the edges of several lines.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="tolerance">How far edges may differ and still line up.</param>
    /// <returns>The alignment.</returns>
    private static PdfTextAlignment Many(ReadOnlySpan<LayoutLine> lines, float tolerance)
    {
        var allLeft = Spread(lines, Edge.Left);
        var allRight = Spread(lines, Edge.Right);
        var allCentre = Spread(lines, Edge.Centre);
        if (lines.Length >= MinJustifiedLines && Spread(lines[1..], Edge.Left) <= tolerance && Spread(lines[..^1], Edge.Right) <= tolerance)
        {
            return PdfTextAlignment.Justify;
        }

        if (allLeft <= tolerance)
        {
            return PdfTextAlignment.Start;
        }

        if (allRight <= tolerance && allLeft > RaggedSpread * tolerance)
        {
            return PdfTextAlignment.End;
        }

        return allCentre <= tolerance && allLeft > RaggedSpread * tolerance && allRight > RaggedSpread * tolerance ? PdfTextAlignment.Center : PdfTextAlignment.Start;
    }

    /// <summary>Gets how far apart one edge of the lines falls.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="edge">Which edge.</param>
    /// <returns>The distance between the smallest and the largest position of the edge.</returns>
    private static float Spread(ReadOnlySpan<LayoutLine> lines, Edge edge)
    {
        var low = float.MaxValue;
        var high = float.MinValue;
        foreach (var line in lines)
        {
            var position = edge switch
            {
                Edge.Left => line.Bounds.Left,
                Edge.Right => line.Bounds.Right,
                _ => line.Bounds.CenterX,
            };
            low = Math.Min(low, position);
            high = Math.Max(high, position);
        }

        return high - low;
    }
}

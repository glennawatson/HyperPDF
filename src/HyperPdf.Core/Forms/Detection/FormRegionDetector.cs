// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Forms.Detection;

/// <summary>
/// Finds where to write on a form that has no fillable fields, such as a scan or a printed form: ruled lines,
/// boxes, and rows of character boxes (combs) or lines with evenly spaced ticks. It works on the page's pixels, so it
/// finds the same places whether the form was drawn or scanned. Dark horizontal runs are found row by row and joined
/// into rules; pairs of rules with upright edges become boxes; upright lines inside a box, evenly spaced, make it a
/// comb; other rules with room above become lines to write on. Boxes already holding printed text are left out.
/// The instance keeps its buffers, so detecting again allocates nothing once warm.
/// </summary>
[DebuggerDisplay("FormRegionDetector: {_rules.Count} rules")]
public sealed class FormRegionDetector
{
    /// <summary>The narrowest place to write, in points.</summary>
    private const float MinFieldWidth = 20;

    /// <summary>The thickest rule, in points; thicker dark areas are shapes, not lines.</summary>
    private const float MaxRuleThickness = 3;

    /// <summary>The lowest box, in points.</summary>
    private const float MinBoxHeight = 8;

    /// <summary>The tallest box, in points.</summary>
    private const float MaxBoxHeight = 72;

    /// <summary>How far apart the ends of a box's top and bottom may be, in points.</summary>
    private const float EdgeTolerance = 3;

    /// <summary>The room offered above a line to write on, in points.</summary>
    private const float LineSpace = 16;

    /// <summary>The least room above a line for it to be written on, in points.</summary>
    private const float MinLineSpace = 6;

    /// <summary>The height a tick must rise above its line, in points.</summary>
    private const float TickHeight = 2.5F;

    /// <summary>The shortest line written on, in points.</summary>
    private const float MinLineLength = 30;

    /// <summary>The level below which a pixel counts as ink.</summary>
    private const byte DarkLevel = 160;

    /// <summary>The share of a box's height an upright line must cover.</summary>
    private const float UprightCoverage = 0.75F;

    /// <summary>The share of a box's inside that may be ink before the box counts as already holding text.</summary>
    private const float MaxInk = 0.03F;

    /// <summary>The share of a row above a line that may be ink while it still counts as room.</summary>
    private const float MaxRowInk = 0.02F;

    /// <summary>How far each comb gap may differ from the average gap, as a share of it.</summary>
    private const float SpacingTolerance = 0.2F;

    /// <summary>
    /// How steeply a line may slope on a crooked scan, about one degree: a slanted line crosses more rows the longer
    /// it is, so it may be that much thicker and still be a line.
    /// </summary>
    private const float MaxSlope = 0.0175F;

    /// <summary>The fewest upright lines inside a box that make it a comb.</summary>
    private const int MinCombDividers = 2;

    /// <summary>The fewest ticks on a line that make it a comb.</summary>
    private const int MinCombTicks = 3;

    /// <summary>How many edge tolerances from the counted area's sides a box's upright edges may be.</summary>
    private const int EdgeReach = 2;

    /// <summary>Halves a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The dark runs of the row being read.</summary>
    private readonly List<DarkRun> _runs = [];

    /// <summary>The horizontal rules found.</summary>
    private readonly List<Rule> _rules = [];

    /// <summary>The rules that reached the previous row.</summary>
    private readonly List<int> _open = [];

    /// <summary>The rules that reach the current row.</summary>
    private readonly List<int> _next = [];

    /// <summary>The upright lines found in a box or above a line, as pixel columns.</summary>
    private readonly List<int> _uprights = [];

    /// <summary>The dark pixels counted per column.</summary>
    private ushort[] _counts = [];

    /// <summary>Finds the places to write on a page.</summary>
    /// <param name="image">The page's luminance, one byte per pixel.</param>
    /// <param name="size">The image's width, height and row stride, in pixels and bytes.</param>
    /// <param name="pixelsPerPoint">How many pixels make a point.</param>
    /// <param name="output">Receives the places, top to bottom.</param>
    public void Detect(ReadOnlySpan<byte> image, (int Width, int Height, int Stride) size, float pixelsPerPoint, List<FormRegion> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (pixelsPerPoint <= 0 || size.Width <= 0 || size.Height <= 0 || image.Length < ((size.Height - 1) * size.Stride) + size.Width)
        {
            return;
        }

        var scan = new Scan(size.Width, size.Height, size.Stride, pixelsPerPoint);
        if (_counts.Length < size.Width)
        {
            _counts = new ushort[size.Width];
        }

        FindRules(image, scan);
        var start = output.Count;
        FindBoxes(image, scan, output);
        FindLines(image, scan, output);
        CollectionsMarshal.AsSpan(output)[start..].Sort(static (a, b) => a.Bounds.Top.CompareTo(b.Bounds.Top) is var order and not 0 ? order : a.Bounds.Left.CompareTo(b.Bounds.Left));
    }

    /// <summary>Determines whether the gaps between upright lines are even.</summary>
    /// <param name="positions">The lines' columns, left to right.</param>
    /// <returns><see langword="true"/> when every gap is within the tolerance of the average.</returns>
    private static bool IsEven(ReadOnlySpan<int> positions)
    {
        if (positions.Length < MinCombDividers)
        {
            return false;
        }

        var average = (positions[^1] - positions[0]) / (float)(positions.Length - 1);
        for (var i = 1; i < positions.Length; i++)
        {
            if (Math.Abs(positions[i] - positions[i - 1] - average) > average * SpacingTolerance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether two rules start and end together, as a box's top and bottom do.</summary>
    /// <param name="top">The upper rule.</param>
    /// <param name="bottom">The lower rule.</param>
    /// <param name="scan">The image's size and scale.</param>
    /// <returns><see langword="true"/> when both ends line up.</returns>
    private static bool EndsAlign(Rule top, Rule bottom, in Scan scan)
    {
        var tolerance = scan.Pixels(EdgeTolerance);
        return Math.Abs(top.Left - bottom.Left) <= tolerance && Math.Abs(top.Right - bottom.Right) <= tolerance;
    }

    /// <summary>Counts the dark pixels of a row above a line, leaving out the line's ticks.</summary>
    /// <param name="row">The row, from the line's left end.</param>
    /// <param name="ticks">The ticks' columns, from the line's left end.</param>
    /// <param name="reach">How far either side of a tick its ink may be.</param>
    /// <returns>The dark pixels.</returns>
    private static int InkOutsideTicks(ReadOnlySpan<byte> row, ReadOnlySpan<int> ticks, int reach)
    {
        var ink = 0;
        var next = 0;
        for (var x = 0; x < row.Length; x++)
        {
            while (next < ticks.Length && ticks[next] + reach < x)
            {
                next++;
            }

            var nearTick = next < ticks.Length && Math.Abs(ticks[next] - x) <= reach;
            ink += row[x] < DarkLevel && !nearTick ? 1 : 0;
        }

        return ink;
    }

    /// <summary>Measures the clear room above a rule, up to the room offered, stopping at a row with ink.</summary>
    /// <param name="image">The image.</param>
    /// <param name="scan">The image's size and scale.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The room in pixels.</returns>
    private int RoomAbove(ReadOnlySpan<byte> image, in Scan scan, Rule rule)
    {
        var ticks = CollectionsMarshal.AsSpan(_uprights);
        var reach = Math.Max(1, scan.Pixels(MaxRuleThickness));
        var limit = Math.Min(rule.Top, scan.Pixels(LineSpace));
        var tick = scan.Pixels(TickHeight);
        var width = rule.Right - rule.Left;
        for (var room = 1; room <= limit; room++)
        {
            var row = image.Slice(((rule.Top - room) * scan.Stride) + rule.Left, width);

            // Ticks rise above the line; any other ink above it ends the room.
            if (room > tick && InkOutsideTicks(row, ticks, reach) > width * MaxRowInk)
            {
                return room - 1;
            }
        }

        return limit;
    }

    /// <summary>Reads every row's dark runs and joins runs on following rows into rules no thicker than a line.</summary>
    /// <param name="image">The image.</param>
    /// <param name="scan">The image's size and scale.</param>
    private void FindRules(ReadOnlySpan<byte> image, in Scan scan)
    {
        _rules.Clear();
        _open.Clear();
        var minLength = scan.Pixels(MinFieldWidth);
        for (var y = 0; y < scan.Height; y++)
        {
            _runs.Clear();
            DarkPixels.FindRuns(image.Slice(y * scan.Stride, scan.Width), DarkLevel, minLength, y, _runs);
            _next.Clear();
            foreach (var run in _runs)
            {
                _next.Add(Join(run));
            }

            _open.Clear();
            _open.AddRange(_next);
        }

        DropThickRules(Math.Max(1, scan.Pixels(MaxRuleThickness)));
    }

    /// <summary>
    /// Drops rules thicker than a line, in place: thick dark areas are shapes or pictures, not lines. A long rule may
    /// be thicker by its slope, so a line on a slightly crooked scan stays a line.
    /// </summary>
    /// <param name="maxThickness">The thickest a level line may be, in pixels.</param>
    private void DropThickRules(int maxThickness)
    {
        var kept = 0;
        for (var i = 0; i < _rules.Count; i++)
        {
            var rule = _rules[i];
            if (rule.Bottom - rule.Top + 1 > maxThickness + (int)((rule.Right - rule.Left) * MaxSlope))
            {
                continue;
            }

            _rules[kept] = rule;
            kept++;
        }

        _rules.RemoveRange(kept, _rules.Count - kept);
    }

    /// <summary>Adds a run to the rule it overlaps on the row above, or starts a rule.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The rule's index.</returns>
    private int Join(DarkRun run)
    {
        // Any overlap continues a rule: on a crooked scan a line steps sideways from row to row.
        foreach (var index in _open)
        {
            var rule = _rules[index];
            if (Math.Min(rule.Right, run.End) <= Math.Max(rule.Left, run.Start))
            {
                continue;
            }

            _rules[index] = rule with { Left = Math.Min(rule.Left, run.Start), Right = Math.Max(rule.Right, run.End), Bottom = run.Y };
            return index;
        }

        _rules.Add(new(run.Start, run.End, run.Y, run.Y));
        return _rules.Count - 1;
    }

    /// <summary>Pairs each rule with the nearest rule below it that, with upright edges, closes a box.</summary>
    /// <param name="image">The image.</param>
    /// <param name="scan">The image's size and scale.</param>
    /// <param name="output">Receives the boxes.</param>
    private void FindBoxes(ReadOnlySpan<byte> image, in Scan scan, List<FormRegion> output)
    {
        var (minHeight, maxHeight) = (scan.Pixels(MinBoxHeight), scan.Pixels(MaxBoxHeight));
        for (var t = 0; t < _rules.Count; t++)
        {
            for (var b = t + 1; b < _rules.Count; b++)
            {
                var (top, bottom) = (_rules[t], _rules[b]);
                var gap = bottom.Top - top.Bottom - 1;
                if (gap < minHeight || gap > maxHeight || !EndsAlign(top, bottom, scan) || !TryBox(image, scan, (top, bottom), output))
                {
                    continue;
                }

                _rules[t] = top with { IsEdge = true };
                _rules[b] = bottom with { IsEdge = true };
                break;
            }
        }
    }

    /// <summary>Checks the space between two rules for upright edges, and adds it as a box, a comb or a row of boxes.</summary>
    /// <param name="image">The image.</param>
    /// <param name="scan">The image's size and scale.</param>
    /// <param name="pair">The upper and lower rules.</param>
    /// <param name="output">Receives what was found.</param>
    /// <returns><see langword="true"/> when the rules close a box.</returns>
    private bool TryBox(ReadOnlySpan<byte> image, in Scan scan, (Rule Top, Rule Bottom) pair, List<FormRegion> output)
    {
        var tolerance = scan.Pixels(EdgeTolerance);
        var left = Math.Max(0, Math.Min(pair.Top.Left, pair.Bottom.Left) - tolerance);
        var right = Math.Min(scan.Width, Math.Max(pair.Top.Right, pair.Bottom.Right) + tolerance);
        var (y0, y1) = (pair.Top.Bottom + 1, pair.Bottom.Top);
        var counts = Count(image, scan, (left, right), (y0, y1));
        FindUprights(counts, (int)MathF.Ceiling((y1 - y0) * UprightCoverage), scan);
        var reach = tolerance * EdgeReach;
        if (_uprights.Count < MinCombDividers || _uprights[0] > reach || _uprights[^1] < right - left - reach - 1)
        {
            return false;
        }

        if (HasText(counts, (y1 - y0) * (_uprights[^1] - _uprights[0])))
        {
            // Printed text inside: a label, not a place to write. Its rules still close a box, so they are not lines.
            return true;
        }

        AddBoxes(scan, (left, y0, y1), output);
        return true;
    }

    /// <summary>Adds a box between its outer upright edges: a comb when the lines between are even, else each part as a box.</summary>
    /// <param name="scan">The image's size and scale.</param>
    /// <param name="area">The column the counts start at, and the inside's top and bottom rows.</param>
    /// <param name="output">Receives what was found.</param>
    private void AddBoxes(in Scan scan, (int Left, int Top, int Bottom) area, List<FormRegion> output)
    {
        var uprights = CollectionsMarshal.AsSpan(_uprights);
        if (uprights.Length - 1 > MinCombDividers && IsEven(uprights))
        {
            output.Add(new(scan.ToPage(area.Left + uprights[0], area.Top, area.Left + uprights[^1], area.Bottom), FormRegionKind.Comb, uprights.Length - 1));
            return;
        }

        var minWidth = scan.Pixels(MinFieldWidth);
        for (var i = 1; i < uprights.Length; i++)
        {
            if (uprights[i] - uprights[i - 1] >= minWidth)
            {
                output.Add(new(scan.ToPage(area.Left + uprights[i - 1], area.Top, area.Left + uprights[i], area.Bottom), FormRegionKind.Box, 0));
            }
        }
    }

    /// <summary>Offers the room above each rule that is not part of a box, as a line or, with even ticks, a comb.</summary>
    /// <param name="image">The image.</param>
    /// <param name="scan">The image's size and scale.</param>
    /// <param name="output">Receives the lines.</param>
    private void FindLines(ReadOnlySpan<byte> image, in Scan scan, List<FormRegion> output)
    {
        var minLength = scan.Pixels(MinLineLength);
        foreach (var rule in _rules)
        {
            if (rule.IsEdge || rule.Right - rule.Left < minLength)
            {
                continue;
            }

            // Ticks at a line's ends may stand just past it, so look a line's width beyond each end.
            var tick = Math.Max(1, scan.Pixels(TickHeight));
            var reach = Math.Max(1, scan.Pixels(MaxRuleThickness));
            var (left, right) = (Math.Max(0, rule.Left - reach), Math.Min(scan.Width, rule.Right + reach));
            var counts = Count(image, scan, (left, right), (Math.Max(0, rule.Top - tick), rule.Top));
            FindUprights(counts, tick, scan);
            var room = RoomAbove(image, scan, rule with { Left = left, Right = right });
            if (room < scan.Pixels(MinLineSpace))
            {
                continue;
            }

            var ticks = CollectionsMarshal.AsSpan(_uprights);
            output.Add(ticks.Length >= MinCombTicks && IsEven(ticks)
                ? new(scan.ToPage(left + ticks[0], rule.Top - room, left + ticks[^1], rule.Top), FormRegionKind.Comb, ticks.Length - 1)
                : new(scan.ToPage(rule.Left, rule.Top - room, rule.Right, rule.Top), FormRegionKind.Underline, 0));
        }
    }

    /// <summary>Counts the dark pixels of each column in an area.</summary>
    /// <param name="image">The image.</param>
    /// <param name="scan">The image's size and scale.</param>
    /// <param name="columns">The first column and the column after the last.</param>
    /// <param name="rows">The first row and the row after the last.</param>
    /// <returns>The counts, one per column.</returns>
    private Span<ushort> Count(ReadOnlySpan<byte> image, in Scan scan, (int Start, int End) columns, (int Start, int End) rows)
    {
        var counts = _counts.AsSpan(0, columns.End - columns.Start);
        counts.Clear();
        for (var y = rows.Start; y < rows.End; y++)
        {
            DarkPixels.CountColumns(image.Slice((y * scan.Stride) + columns.Start, counts.Length), DarkLevel, counts);
        }

        return counts;
    }

    /// <summary>Finds the upright lines in column counts: runs of columns dark for at least a given number of rows.</summary>
    /// <param name="counts">The counts.</param>
    /// <param name="need">The dark rows a column needs.</param>
    /// <param name="scan">The image's size and scale.</param>
    private void FindUprights(ReadOnlySpan<ushort> counts, int need, in Scan scan)
    {
        _uprights.Clear();
        var maxWidth = Math.Max(1, scan.Pixels(MaxRuleThickness));
        var start = -1;
        for (var x = 0; x <= counts.Length; x++)
        {
            var dark = x < counts.Length && counts[x] >= need;
            if (dark && start < 0)
            {
                start = x;
            }
            else if (!dark && start >= 0)
            {
                // A wide dark block is a shape or a letter, not a line.
                if (x - start <= maxWidth)
                {
                    _uprights.Add((int)((start + x - 1) * Half));
                }

                start = -1;
            }
        }
    }

    /// <summary>Determines whether a box's inside holds printed text, leaving out its upright lines.</summary>
    /// <param name="counts">The inside's column counts.</param>
    /// <param name="area">The inside's area in pixels.</param>
    /// <returns><see langword="true"/> when there is more ink than lines alone make.</returns>
    private bool HasText(ReadOnlySpan<ushort> counts, int area)
    {
        if (area <= 0)
        {
            return false;
        }

        var ink = 0;
        for (var x = _uprights[0] + 1; x < _uprights[^1]; x++)
        {
            ink += counts[x];
        }

        // Each upright line adds its own ink; take away a generous allowance for them.
        var lines = (_uprights.Count - MinCombDividers) * counts[_uprights[0]] * (int)MaxRuleThickness;
        return ink - lines > area * MaxInk;
    }

    /// <summary>A horizontal rule: dark runs on following rows, in pixels.</summary>
    /// <param name="Left">The first column.</param>
    /// <param name="Right">The column after the last.</param>
    /// <param name="Top">The first row.</param>
    /// <param name="Bottom">The last row.</param>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Rule(int Left, int Right, int Top, int Bottom)
    {
        /// <summary>Gets a value indicating whether the rule is a box's top or bottom, so not a line to write on.</summary>
        public bool IsEdge { get; init; }
    }

    /// <summary>The image's size and its scale.</summary>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    /// <param name="Stride">The bytes per row.</param>
    /// <param name="PixelsPerPoint">How many pixels make a point.</param>
    [StructLayout(LayoutKind.Auto)]
    private readonly record struct Scan(int Width, int Height, int Stride, float PixelsPerPoint)
    {
        /// <summary>Converts a length in points to whole pixels.</summary>
        /// <param name="points">The length.</param>
        /// <returns>The pixels.</returns>
        public int Pixels(float points) => (int)MathF.Round(points * PixelsPerPoint);

        /// <summary>Converts a pixel rectangle to page space.</summary>
        /// <param name="left">The left column.</param>
        /// <param name="top">The top row.</param>
        /// <param name="right">The right column.</param>
        /// <param name="bottom">The bottom row.</param>
        /// <returns>The rectangle in points.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PageRect ToPage(int left, int top, int right, int bottom) =>
            PageRect.FromEdges(left / PixelsPerPoint, top / PixelsPerPoint, right / PixelsPerPoint, bottom / PixelsPerPoint);
    }
}

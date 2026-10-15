// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Joins the straight lines of a page that lie on one another: the shared edge of two cells drawn as separate rectangles
/// becomes one line, and a border drawn in pieces becomes one line, while a real gap stays a gap.
/// </summary>
internal static class LayoutRulingMerger
{
    /// <summary>How far, in points, two lines may be apart across their axis and still be one line.</summary>
    internal const float AxisTolerance = 1.5F;

    /// <summary>How far, in points, two pieces of a line may be apart along it and still be one line.</summary>
    internal const float JoinTolerance = 2F;

    /// <summary>Joins the lines.</summary>
    /// <param name="rulings">The lines as the page draws them.</param>
    /// <param name="merged">Receives the joined lines; at least as long as <paramref name="rulings"/>.</param>
    /// <returns>The number of joined lines.</returns>
    internal static int Merge(ReadOnlySpan<LayoutRuling> rulings, LayoutRuling[] merged)
    {
        rulings.CopyTo(merged);
        var work = merged.AsSpan(0, rulings.Length);
        work.Sort(static (a, b) => a.IsHorizontal != b.IsHorizontal ? b.IsHorizontal.CompareTo(a.IsHorizontal) : Axis(a).CompareTo(Axis(b)));
        var written = 0;
        var start = 0;
        while (start < work.Length)
        {
            var end = start + 1;
            while (end < work.Length && work[end].IsHorizontal == work[start].IsHorizontal && Axis(work[end]) - Axis(work[end - 1]) <= AxisTolerance)
            {
                end++;
            }

            written = MergeCluster(work[start..end], work, written);
            start = end;
        }

        return written;
    }

    /// <summary>Gets where a line sits across its direction: its height for a horizontal line, its position across for a vertical one.</summary>
    /// <param name="ruling">The line.</param>
    /// <returns>The position.</returns>
    internal static float Axis(in LayoutRuling ruling) => ruling.IsHorizontal ? ruling.Bounds.CenterY : ruling.Bounds.CenterX;

    /// <summary>Gets where a line starts along its direction.</summary>
    /// <param name="ruling">The line.</param>
    /// <returns>The start.</returns>
    internal static float Start(in LayoutRuling ruling) => ruling.IsHorizontal ? ruling.Bounds.Left : ruling.Bounds.Top;

    /// <summary>Gets where a line ends along its direction.</summary>
    /// <param name="ruling">The line.</param>
    /// <returns>The end.</returns>
    internal static float End(in LayoutRuling ruling) => ruling.IsHorizontal ? ruling.Bounds.Right : ruling.Bounds.Bottom;

    /// <summary>Joins the lines of one cluster, which lie along one axis, and writes the result at the front of the same array.</summary>
    /// <param name="cluster">The cluster's lines; sorted by start in place.</param>
    /// <param name="output">The array the joined lines are written to, from <paramref name="written"/>.</param>
    /// <param name="written">The joined lines so far.</param>
    /// <returns>The joined lines so far, with this cluster's added.</returns>
    private static int MergeCluster(Span<LayoutRuling> cluster, Span<LayoutRuling> output, int written)
    {
        cluster.Sort(static (a, b) => Start(a).CompareTo(Start(b)));
        var current = cluster[0];
        var kept = 0;
        for (var i = 1; i < cluster.Length; i++)
        {
            if (Start(cluster[i]) <= End(current) + JoinTolerance)
            {
                current = new(current.Bounds.Union(cluster[i].Bounds), current.IsHorizontal);
            }
            else
            {
                // The kept lines never pass the one being read, so writing over the front of the cluster loses nothing.
                cluster[kept] = current;
                kept++;
                current = cluster[i];
            }
        }

        cluster[kept] = current;
        kept++;
        cluster[..kept].CopyTo(output[written..]);
        return written + kept;
    }
}

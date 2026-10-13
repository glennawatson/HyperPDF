// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Groups the glyphs of a shaped run by the characters they show, records what each glyph stands for, and decides which
/// groups need <c>/ActualText</c> (ISO 32000-2, 14.9.4) because <c>/ToUnicode</c> alone cannot say what they mean. The
/// glyphs of a right-to-left run stay in visual order with one group per cluster: readers put such a run into reading
/// order themselves, so only a cluster that is not one glyph for one character is wrapped.
/// </summary>
internal static class RunPlanner
{
    /// <summary>Plans a run.</summary>
    /// <param name="text">The text in logical order.</param>
    /// <param name="glyphs">The glyphs in drawing order.</param>
    /// <param name="policy">When to use <c>/ActualText</c>.</param>
    /// <param name="use">Receives the text of each glyph.</param>
    /// <param name="groups">Receives the groups; the list is not cleared.</param>
    internal static void Plan(ReadOnlySpan<char> text, ReadOnlySpan<PdfShapedGlyph> glyphs, PdfActualTextPolicy policy, EmbeddedGlyphUse use, List<ClusterGroup> groups)
    {
        if (glyphs.IsEmpty)
        {
            return;
        }

        var starts = IsAscending(glyphs, text.Length) ? null : SortedStarts(glyphs, text.Length);
        var first = 0;
        while (first < glyphs.Length)
        {
            var cluster = Math.Clamp(glyphs[first].Cluster, 0, text.Length);
            var end = first + 1;
            while (end < glyphs.Length && glyphs[end].Cluster == glyphs[first].Cluster)
            {
                end++;
            }

            var textEnd = starts is null ? FollowingCluster(glyphs, end, text.Length) : NextStart(starts, cluster, text.Length);
            groups.Add(PlanGroup(glyphs, first, end - first, text.Slice(cluster, Math.Max(textEnd - cluster, 0)), cluster, policy, use));
            first = end;
        }
    }

    /// <summary>Determines whether the clusters of a run never go backwards and stay inside the text.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="length">The text's length.</param>
    /// <returns><see langword="true"/> when the glyphs follow the text in order.</returns>
    private static bool IsAscending(ReadOnlySpan<PdfShapedGlyph> glyphs, int length)
    {
        var previous = 0;
        foreach (var glyph in glyphs)
        {
            if (glyph.Cluster < previous || glyph.Cluster > length)
            {
                return false;
            }

            previous = glyph.Cluster;
        }

        return true;
    }

    /// <summary>Gets where the characters of the cluster before a glyph end, when clusters ascend: the glyph's own cluster.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="next">The index of the glyph after the cluster.</param>
    /// <param name="length">The text's length, which the last cluster runs to.</param>
    /// <returns>The index after the cluster's last character.</returns>
    private static int FollowingCluster(ReadOnlySpan<PdfShapedGlyph> glyphs, int next, int length) =>
        next < glyphs.Length ? glyphs[next].Cluster : length;

    /// <summary>Lists the distinct cluster starts of a run in ascending order.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="length">The text's length.</param>
    /// <returns>The starts.</returns>
    private static int[] SortedStarts(ReadOnlySpan<PdfShapedGlyph> glyphs, int length)
    {
        var starts = new int[glyphs.Length];
        for (var i = 0; i < starts.Length; i++)
        {
            starts[i] = Math.Clamp(glyphs[i].Cluster, 0, length);
        }

        Array.Sort(starts);
        return starts;
    }

    /// <summary>Finds where the characters of a cluster end: the next cluster start above it.</summary>
    /// <param name="starts">The sorted cluster starts.</param>
    /// <param name="cluster">The cluster's start.</param>
    /// <param name="length">The text's length, which the last cluster runs to.</param>
    /// <returns>The index after the cluster's last character.</returns>
    private static int NextStart(int[] starts, int cluster, int length)
    {
        var index = Array.BinarySearch(starts, cluster);
        while (index < starts.Length && starts[index] <= cluster)
        {
            index++;
        }

        return index < starts.Length ? starts[index] : length;
    }

    /// <summary>Plans one cluster of glyphs that shows consecutive characters.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="first">The index of the cluster's first glyph.</param>
    /// <param name="count">The number of glyphs in it.</param>
    /// <param name="clusterText">The characters the cluster shows.</param>
    /// <param name="start">The index of the first character in the run's text.</param>
    /// <param name="policy">When to use <c>/ActualText</c>.</param>
    /// <param name="use">Receives the text of each glyph.</param>
    /// <returns>The group.</returns>
    private static ClusterGroup PlanGroup(ReadOnlySpan<PdfShapedGlyph> glyphs, int first, int count, ReadOnlySpan<char> clusterText, int start, PdfActualTextPolicy policy, EmbeddedGlyphUse use)
    {
        if (clusterText.IsEmpty)
        {
            return new(first, count, start, 0, false);
        }

        var characters = CountRunes(clusterText);
        var recorded = use.RecordText(use.GetCode(glyphs[first].Glyph), clusterText.ToString());
        var mark = count == 1 && characters == 1
            ? !recorded
            : count != 1 || !recorded || policy == PdfActualTextPolicy.Ligatures;
        return new(first, count, start, clusterText.Length, mark && policy != PdfActualTextPolicy.Never);
    }

    /// <summary>Counts the Unicode characters in some text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number of code points.</returns>
    private static int CountRunes(ReadOnlySpan<char> text)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            _ = rune;
            count++;
        }

        return count;
    }
}

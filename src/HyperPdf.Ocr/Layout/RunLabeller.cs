// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Layout;

/// <summary>
/// Finds the connected blobs of ink. Each row is cut into runs of ink; a run joins the runs above it that touch it,
/// including diagonally, so a page is labelled from its runs rather than its pixels.
/// </summary>
public static class RunLabeller
{
    /// <summary>Finds the blobs of ink on a page.</summary>
    /// <param name="mask">The ink.</param>
    /// <param name="components">Receives the box of each blob.</param>
    /// <param name="scratch">The working memory.</param>
    public static void Label(InkMask mask, List<ComponentBox> components, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(scratch);
        var runs = scratch.Runs;
        runs.Clear();
        var starts = runs.Starts;
        var ends = runs.Ends;
        var rows = runs.Rows;
        var parents = runs.Parents;
        var previousFirst = 0;
        var previousCount = 0;
        for (var y = 0; y < mask.Height; y++)
        {
            var first = starts.Count;
            CollectRuns(mask.Pixels.Slice(y * mask.Width, mask.Width), y, starts, ends, rows, parents);
            JoinRows(starts, ends, parents, previousFirst, previousCount, first);
            previousFirst = first;
            previousCount = starts.Count - first;
        }

        Gather(runs, components);
    }

    /// <summary>Adds the runs of ink in a row.</summary>
    /// <param name="row">The row's pixels.</param>
    /// <param name="y">The row number.</param>
    /// <param name="starts">The start of each run, added to.</param>
    /// <param name="ends">The end of each run, which is one past its last pixel, added to.</param>
    /// <param name="rows">The row of each run, added to.</param>
    /// <param name="parents">The union-find parent of each run, added to.</param>
    private static void CollectRuns(ReadOnlySpan<byte> row, int y, List<int> starts, List<int> ends, List<int> rows, List<int> parents)
    {
        var x = 0;
        while (x < row.Length)
        {
            // Both searches are vectorised, so a row that is mostly paper costs a few vector compares.
            var skipped = row[x..].IndexOfAnyExcept((byte)0);
            if (skipped < 0)
            {
                return;
            }

            var start = x + skipped;
            var length = row[start..].IndexOf((byte)0);
            x = length < 0 ? row.Length : start + length;
            parents.Add(starts.Count);
            starts.Add(start);
            ends.Add(x);
            rows.Add(y);
        }
    }

    /// <summary>Joins each run of a row to the touching runs of the row above.</summary>
    /// <param name="starts">The start of each run.</param>
    /// <param name="ends">The end of each run.</param>
    /// <param name="parents">The union-find parent of each run.</param>
    /// <param name="previousFirst">The index of the row above's first run.</param>
    /// <param name="previousCount">The number of runs in the row above.</param>
    /// <param name="first">The index of this row's first run.</param>
    private static void JoinRows(List<int> starts, List<int> ends, List<int> parents, int previousFirst, int previousCount, int first)
    {
        var above = previousFirst;
        var aboveEnd = previousFirst + previousCount;
        for (var run = first; run < starts.Count; run++)
        {
            while (above < aboveEnd && ends[above] < starts[run])
            {
                above++;
            }

            for (var candidate = above; candidate < aboveEnd && starts[candidate] <= ends[run]; candidate++)
            {
                Union(parents, run, candidate);
            }
        }
    }

    /// <summary>Gets the representative of a run's blob.</summary>
    /// <param name="parents">The union-find parents.</param>
    /// <param name="run">The run.</param>
    /// <returns>The representative run.</returns>
    private static int Find(List<int> parents, int run)
    {
        var root = run;
        while (parents[root] != root)
        {
            root = parents[root];
        }

        while (parents[run] != root)
        {
            var next = parents[run];
            parents[run] = root;
            run = next;
        }

        return root;
    }

    /// <summary>Merges the blobs of two runs.</summary>
    /// <param name="parents">The union-find parents.</param>
    /// <param name="a">The first run.</param>
    /// <param name="b">The second run.</param>
    private static void Union(List<int> parents, int a, int b)
    {
        var rootA = Find(parents, a);
        var rootB = Find(parents, b);
        if (rootA != rootB)
        {
            parents[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
        }
    }

    /// <summary>Adds up the runs of each blob into a box.</summary>
    /// <param name="runs">The runs.</param>
    /// <param name="components">Receives the boxes, in the order of each blob's first run.</param>
    private static void Gather(RunLists runs, List<ComponentBox> components)
    {
        var starts = runs.Starts;
        var ends = runs.Ends;
        var rows = runs.Rows;
        var parents = runs.Parents;
        var slots = runs.EmptySlots(starts.Count);
        for (var run = 0; run < starts.Count; run++)
        {
            var root = Find(parents, run);
            if (slots[root] < 0)
            {
                slots[root] = components.Count;
                components.Add(new(starts[run], rows[run], ends[run] - 1, rows[run], ends[run] - starts[run]));
                continue;
            }

            var box = components[slots[root]];
            components[slots[root]] = new(
                Math.Min(box.Left, starts[run]),
                box.Top,
                Math.Max(box.Right, ends[run] - 1),
                rows[run],
                box.Pixels + ends[run] - starts[run]);
        }
    }
}

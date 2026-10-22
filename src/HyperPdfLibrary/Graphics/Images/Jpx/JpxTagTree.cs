// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Tag trees (ISO 15444-1 B.10.2) stored flat: the leaves in raster order, then each coarser level, up to a single root.
/// A tree is named by its first node and its leaf grid size.
/// </summary>
internal static class JpxTagTree
{
    /// <summary>The value of a node not yet decoded.</summary>
    internal const int Unknown = int.MaxValue;

    /// <summary>The most levels a tree can have: enough for a 2^30 leaf grid.</summary>
    private const int MaxLevels = 32;

    /// <summary>Counts the nodes of a tree.</summary>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    /// <returns>The number of nodes.</returns>
    internal static int CountNodes(int width, int height)
    {
        var total = 0;
        var last = false;
        while (!last)
        {
            total += width * height;
            last = width <= 1 && height <= 1;
            width = (width + 1) >> 1;
            height = (height + 1) >> 1;
        }

        return total;
    }

    /// <summary>Adds a tree with every node unknown.</summary>
    /// <param name="nodes">The node store.</param>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    /// <returns>The first node.</returns>
    internal static int Add(JpxList<JpxTagNode> nodes, int width, int height)
    {
        var count = CountNodes(width, height);
        var start = nodes.AddDefault(count);
        foreach (ref var node in nodes.AsSpan().Slice(start, count))
        {
            node.Value = Unknown;
        }

        return start;
    }

    /// <summary>Decodes whether a leaf's value is below a threshold, reading only the bits needed.</summary>
    /// <param name="nodes">The node store.</param>
    /// <param name="tree">The first node of the tree.</param>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    /// <param name="leaf">The leaf index in raster order.</param>
    /// <param name="threshold">The threshold.</param>
    /// <param name="reader">The packet header bits.</param>
    /// <returns><see langword="true"/> when the leaf's value is below <paramref name="threshold"/>.</returns>
    internal static bool Decode(JpxList<JpxTagNode> nodes, int tree, int width, int height, int leaf, int threshold, ref JpxBitReader reader)
    {
        Span<int> path = stackalloc int[MaxLevels];
        var depth = FindPath(tree, width, height, leaf, path);
        var low = 0;
        for (var i = depth - 1; i >= 0; i--)
        {
            ref var node = ref nodes[path[i]];
            low = Math.Max(low, node.Low);
            while (low < threshold && low < node.Value)
            {
                if (reader.ReadBit() != 0)
                {
                    node.Value = low;
                }
                else
                {
                    low++;
                }
            }

            node.Low = low;
        }

        return nodes[path[0]].Value < threshold;
    }

    /// <summary>Finds the nodes from a leaf up to the root.</summary>
    /// <param name="tree">The first node of the tree.</param>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    /// <param name="leaf">The leaf index in raster order.</param>
    /// <param name="path">Receives the node indices, leaf first.</param>
    /// <returns>The number of nodes on the path.</returns>
    private static int FindPath(int tree, int width, int height, int leaf, Span<int> path)
    {
        var x = leaf % width;
        var y = leaf / width;
        var level = tree;
        var depth = 0;
        while (depth < path.Length)
        {
            path[depth] = level + (y * width) + x;
            depth++;
            if (width <= 1 && height <= 1)
            {
                break;
            }

            level += width * height;
            x >>= 1;
            y >>= 1;
            width = (width + 1) >> 1;
            height = (height + 1) >> 1;
        }

        return depth;
    }
}

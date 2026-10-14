// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The tag trees of a tile for encoding (ISO 15444-1 B.10.2): each tree's nodes hold the smallest value of the leaves
/// below them, and the encoder sends, for a leaf and a threshold, just enough bits to tell whether the leaf's value is
/// below the threshold. The nodes of all trees share three pooled stores, so a tile's trees cost no allocation of their own.
/// </summary>
[DebuggerDisplay("JpxTagTrees: {_trees.Count} trees, {_values.Count} nodes")]
internal sealed class JpxTagTrees : IDisposable
{
    /// <summary>The children of a node along each axis.</summary>
    private const int Branching = 2;

    /// <summary>The most levels a tree has: a precinct holds at most 2^22 code-blocks along an axis.</summary>
    private const int MaxLevels = 24;

    /// <summary>The node values; the leaves of a tree first, then each coarser level.</summary>
    private readonly JpxList<int> _values;

    /// <summary>The lower bound already sent for each node.</summary>
    private readonly JpxList<int> _lows;

    /// <summary>Whether each node's value has been sent, as 0 or 1.</summary>
    private readonly JpxList<byte> _known;

    /// <summary>The trees.</summary>
    private readonly JpxList<Tree> _trees;

    /// <summary>Initializes a new instance of the <see cref="JpxTagTrees"/> class.</summary>
    /// <param name="scratchPools">The bounded cache.</param>
    internal JpxTagTrees(ScratchPools scratchPools)
    {
        _values = new(scratchPools);
        _lows = new(scratchPools);
        _known = new(scratchPools);
        _trees = new(scratchPools);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _values.Dispose();
        _lows.Dispose();
        _known.Dispose();
        _trees.Dispose();
    }

    /// <summary>Removes every tree.</summary>
    internal void Clear()
    {
        _values.Clear();
        _lows.Clear();
        _known.Clear();
        _trees.Clear();
    }

    /// <summary>Adds a tree whose leaves all start at the largest value.</summary>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    /// <returns>The tree's index.</returns>
    internal int Add(int width, int height)
    {
        var nodes = 0;
        var w = width;
        var h = height;
        while (true)
        {
            nodes += w * h;
            if (w <= 1 && h <= 1)
            {
                break;
            }

            w = (w + 1) / Branching;
            h = (h + 1) / Branching;
        }

        var start = _values.AddDefault(nodes);
        _ = _lows.AddDefault(nodes);
        _ = _known.AddDefault(nodes);
        _values.AsSpan().Slice(start, nodes).Fill(int.MaxValue);
        return _trees.Add(new(start, width, height));
    }

    /// <summary>Sets a leaf's value.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="leaf">The leaf, in raster order.</param>
    /// <param name="value">The value.</param>
    internal void SetLeaf(int tree, int leaf, int value) => _values[_trees[tree].Start + leaf] = value;

    /// <summary>Sets every coarser node to the smallest of its children, once all leaves are set.</summary>
    /// <param name="tree">The tree.</param>
    internal void Propagate(int tree)
    {
        var (childStart, childWidth, childHeight) = _trees[tree];
        var values = _values.AsSpan();
        while (childWidth > 1 || childHeight > 1)
        {
            var parentWidth = (childWidth + 1) / Branching;
            var parentHeight = (childHeight + 1) / Branching;
            var parentStart = childStart + (childWidth * childHeight);
            for (var y = 0; y < childHeight; y++)
            {
                for (var x = 0; x < childWidth; x++)
                {
                    var parent = parentStart + ((y / Branching) * parentWidth) + (x / Branching);
                    values[parent] = Math.Min(values[parent], values[childStart + (y * childWidth) + x]);
                }
            }

            childStart = parentStart;
            childWidth = parentWidth;
            childHeight = parentHeight;
        }
    }

    /// <summary>Writes the bits that tell whether a leaf's value is below a threshold.</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="tree">The tree.</param>
    /// <param name="leaf">The leaf, in raster order.</param>
    /// <param name="threshold">The threshold.</param>
    internal void Encode(JpxBitWriter writer, int tree, int leaf, int threshold)
    {
        var (start, width, height) = _trees[tree];
        var leafWidth = width;
        Span<int> starts = stackalloc int[MaxLevels];
        Span<int> widths = stackalloc int[MaxLevels];
        var levels = 0;
        while (true)
        {
            starts[levels] = start;
            widths[levels] = width;
            levels++;
            if (width <= 1 && height <= 1)
            {
                break;
            }

            start += width * height;
            width = (width + 1) / Branching;
            height = (height + 1) / Branching;
        }

        var x = leaf % leafWidth;
        var y = leaf / leafWidth;
        var low = 0;
        for (var level = levels - 1; level >= 0; level--)
        {
            low = EncodeNode(writer, starts[level] + ((y >> level) * widths[level]) + (x >> level), low, threshold);
        }
    }

    /// <summary>Writes one node's bits.</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="node">The node.</param>
    /// <param name="inherited">The bound inherited from the parent.</param>
    /// <param name="threshold">The threshold.</param>
    /// <returns>The node's bound, for its child.</returns>
    private int EncodeNode(JpxBitWriter writer, int node, int inherited, int threshold)
    {
        var low = Math.Max(inherited, _lows[node]);
        while (low < threshold)
        {
            if (low >= _values[node])
            {
                if (_known[node] == 0)
                {
                    writer.WriteBit(1);
                    _known[node] = 1;
                }

                break;
            }

            writer.WriteBit(0);
            low++;
        }

        _lows[node] = low;
        return low;
    }

    /// <summary>Where a tree's nodes are and the size of its leaf grid.</summary>
    /// <param name="Start">The index of the first leaf in the node stores.</param>
    /// <param name="Width">The leaves across.</param>
    /// <param name="Height">The leaves down.</param>
    private readonly record struct Tree(int Start, int Width, int Height);
}

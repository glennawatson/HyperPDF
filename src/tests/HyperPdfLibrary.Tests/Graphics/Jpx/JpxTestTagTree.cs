// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>A tag tree encoder (ISO 15444-1 B.10.2): each node holds the smallest value below it.</summary>
internal sealed class JpxTestTagTree
{
    /// <summary>The children of a node along each axis.</summary>
    private const int Branching = 2;

    /// <summary>The node values, leaves first, then each coarser level.</summary>
    private readonly int[] _values;

    /// <summary>The lower bound already sent for each node.</summary>
    private readonly int[] _lows;

    /// <summary>Whether each node's value has been sent.</summary>
    private readonly bool[] _known;

    /// <summary>The first node of each level.</summary>
    private readonly List<int> _levelStarts = [];

    /// <summary>The width of each level.</summary>
    private readonly List<int> _levelWidths = [];

    /// <summary>Initializes a new instance of the <see cref="JpxTestTagTree"/> class.</summary>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    /// <param name="leaves">The leaf values in raster order.</param>
    internal JpxTestTagTree(int width, int height, int[] leaves)
    {
        var total = 0;
        var w = width;
        var h = height;
        var last = false;
        while (!last)
        {
            _levelStarts.Add(total);
            _levelWidths.Add(w);
            total += w * h;
            last = w <= 1 && h <= 1;
            w = (w + 1) / Branching;
            h = (h + 1) / Branching;
        }

        _values = new int[total];
        _lows = new int[total];
        _known = new bool[total];
        Array.Fill(_values, int.MaxValue);
        leaves.CopyTo(_values, 0);
        Propagate(width, height);
    }

    /// <summary>Writes the bits that tell whether a leaf's value is below a threshold.</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="leaf">The leaf index in raster order.</param>
    /// <param name="threshold">The threshold.</param>
    internal void Encode(JpxTestBitWriter writer, int leaf, int threshold)
    {
        var width = _levelWidths[0];
        var x = leaf % width;
        var y = leaf / width;
        var path = new List<int>();
        for (var level = 0; level < _levelStarts.Count; level++)
        {
            path.Add(_levelStarts[level] + ((y >> level) * _levelWidths[level]) + (x >> level));
        }

        var low = 0;
        for (var i = path.Count - 1; i >= 0; i--)
        {
            low = EncodeNode(writer, path[i], low, threshold);
        }
    }

    /// <summary>Writes one node's bits.</summary>
    /// <param name="writer">The header bits.</param>
    /// <param name="node">The node.</param>
    /// <param name="low">The bound inherited from the parent.</param>
    /// <param name="threshold">The threshold.</param>
    /// <returns>The node's bound, for its child.</returns>
    private int EncodeNode(JpxTestBitWriter writer, int node, int low, int threshold)
    {
        low = Math.Max(low, _lows[node]);
        while (low < threshold)
        {
            if (low >= _values[node])
            {
                if (!_known[node])
                {
                    writer.WriteBit(1);
                    _known[node] = true;
                }

                break;
            }

            writer.WriteBit(0);
            low++;
        }

        _lows[node] = low;
        return low;
    }

    /// <summary>Sets each parent to the smallest of its children.</summary>
    /// <param name="width">The leaves across.</param>
    /// <param name="height">The leaves down.</param>
    private void Propagate(int width, int height)
    {
        var w = width;
        var h = height;
        for (var level = 1; level < _levelStarts.Count; level++)
        {
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var child = _values[_levelStarts[level - 1] + (y * w) + x];
                    var parent = _levelStarts[level] + ((y / Branching) * _levelWidths[level]) + (x / Branching);
                    _values[parent] = Math.Min(_values[parent], child);
                }
            }

            w = (w + 1) / Branching;
            h = (h + 1) / Branching;
        }
    }
}

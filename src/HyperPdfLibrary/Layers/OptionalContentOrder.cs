// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// Reads a configuration's /Order array into a tree (ISO 32000-2, 8.11.4.3, Table 99). A layer followed by an array
/// without a label owns the layers of that array as sublayers; an array that starts with a text string is a labelled
/// collection; any other array is an unlabelled collection whose layers join the level they appear in.
/// </summary>
internal static class OptionalContentOrder
{
    /// <summary>Builds the tree of an /Order array.</summary>
    /// <param name="order">The /Order array.</param>
    /// <param name="names">The names of the document's layers by object number; layers not listed here are left out.</param>
    /// <returns>The top-level entries.</returns>
    internal static PdfLayerNode[] Build(PdfArray order, Dictionary<int, string> names)
    {
        var budget = PdfLimits.MaxNesting * PdfLimits.MaxNesting;
        return ReadLevel(order, 0, 0, names, ref budget);
    }

    /// <summary>Builds a flat list of every layer, for a configuration without an /Order.</summary>
    /// <param name="layers">The layers in document order.</param>
    /// <returns>One entry per layer.</returns>
    internal static PdfLayerNode[] Flat(PdfLayer[] layers)
    {
        var nodes = new PdfLayerNode[layers.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            nodes[i] = new(layers[i].Name, layers[i].Id, []);
        }

        return nodes;
    }

    /// <summary>Reads the entries of one array level.</summary>
    /// <param name="array">The array.</param>
    /// <param name="start">The first index to read, after a label.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <param name="names">The names of the document's layers by object number.</param>
    /// <param name="budget">The entries that may still be read, which bounds hostile arrays.</param>
    /// <returns>The entries.</returns>
    private static PdfLayerNode[] ReadLevel(PdfArray array, int start, int depth, Dictionary<int, string> names, ref int budget)
    {
        var nodes = new List<PdfLayerNode>();
        var last = -1;
        for (var i = start; i < array.Count && budget > 0 && depth <= PdfLimits.MaxNesting; i++)
        {
            budget--;
            var item = array.GetRaw(i);
            var id = item.AsReference();
            if (id.IsValid && names.TryGetValue(id.Number, out var name))
            {
                nodes.Add(new(name, id.Number, []));
                last = nodes.Count - 1;
                continue;
            }

            if (array.GetArray(i) is { } nested)
            {
                last = ReadNested(nested, depth, names, nodes, last, ref budget);
            }
        }

        return [.. nodes];
    }

    /// <summary>Reads a nested array and adds it to its level.</summary>
    /// <param name="nested">The nested array.</param>
    /// <param name="depth">The depth of the level that holds it.</param>
    /// <param name="names">The names of the document's layers by object number.</param>
    /// <param name="nodes">The level's entries so far.</param>
    /// <param name="last">The index of the previous layer in the level, or -1.</param>
    /// <param name="budget">The entries that may still be read.</param>
    /// <returns>The index of the previous layer for the next entry.</returns>
    private static int ReadNested(PdfArray nested, int depth, Dictionary<int, string> names, List<PdfLayerNode> nodes, int last, ref int budget)
    {
        var label = nested.Count > 0 && nested.Get(0).Kind == PdfKind.String ? PdfText.Decode(nested.Get(0).AsStringBytes()) : null;
        var children = ReadLevel(nested, label is null ? 0 : 1, depth + 1, names, ref budget);
        if (label is not null)
        {
            nodes.Add(new(label, 0, children));
            return -1;
        }

        if (last >= 0)
        {
            nodes[last] = nodes[last] with
            {
                Children = [.. nodes[last].Children, .. children]
            };
            return last;
        }

        nodes.AddRange(children);
        return -1;
    }
}

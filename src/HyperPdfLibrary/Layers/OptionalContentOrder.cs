// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>Reads the /Order array of a layer configuration into a tree of layers and labels.</summary>
internal static class OptionalContentOrder
{
    /// <summary>Builds the tree of an /Order array.</summary>
    /// <param name="order">The /Order array.</param>
    /// <param name="names">The names of the document's layers; layers not listed there are left out.</param>
    /// <param name="stack">The scratch space that holds the entries of the levels being read.</param>
    /// <returns>The top-level entries.</returns>
    internal static PdfLayerNode[] Build(PdfArray order, in LayerNames names, LayerNodeStack stack)
    {
        var budget = PdfLimits.MaxNesting * PdfLimits.MaxNesting;
        return ReadLevel(order, 0, 0, names, stack, ref budget);
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
    /// <param name="names">The names of the document's layers.</param>
    /// <param name="stack">The scratch space that holds the entries of the levels being read.</param>
    /// <param name="budget">The entries that may still be read, which bounds hostile arrays.</param>
    /// <returns>The entries.</returns>
    private static PdfLayerNode[] ReadLevel(PdfArray array, int start, int depth, in LayerNames names, LayerNodeStack stack, ref int budget)
    {
        var levelStart = stack.Count;
        PendingLayer? pending = null;
        for (var i = start; i < array.Count && budget > 0 && depth <= PdfLimits.MaxNesting; i++)
        {
            budget--;
            var id = array.GetRaw(i).AsReference();
            if (id.IsValid && names.TryGetName(id.Number, out var name))
            {
                Flush(stack, pending);
                pending = new(name, id.Number, []);
                continue;
            }

            if (array.GetArray(i) is { } nested)
            {
                pending = ReadNested(nested, depth, names, stack, pending, ref budget);
            }
        }

        Flush(stack, pending);
        return stack.Take(levelStart);
    }

    /// <summary>Reads a nested array and adds it to its level.</summary>
    /// <param name="nested">The nested array.</param>
    /// <param name="depth">The depth of the level that holds it.</param>
    /// <param name="names">The names of the document's layers.</param>
    /// <param name="stack">The scratch space that holds the entries of the levels being read.</param>
    /// <param name="pending">The previous layer in the level, which has not been added yet, or <see langword="null"/>.</param>
    /// <param name="budget">The entries that may still be read.</param>
    /// <returns>The previous layer for the next entry, which has not been added yet, or <see langword="null"/>.</returns>
    private static PendingLayer? ReadNested(PdfArray nested, int depth, in LayerNames names, LayerNodeStack stack, PendingLayer? pending, ref int budget)
    {
        var label = nested.Count > 0 && nested.Get(0).Kind == PdfKind.String ? PdfText.Decode(nested.Get(0).AsStringBytes()) : null;
        var children = ReadLevel(nested, label is null ? 0 : 1, depth + 1, names, stack, ref budget);
        if (label is not null)
        {
            Flush(stack, pending);
            stack.Push(new(label, 0, children));
            return null;
        }

        if (pending is { } previous)
        {
            return previous with
            {
                Children = previous.Children.Length == 0 ? children : [.. previous.Children, .. children]
            };
        }

        stack.PushRange(children);
        return null;
    }

    /// <summary>Adds a layer that was waiting for its children to the level.</summary>
    /// <param name="stack">The scratch space that holds the entries of the levels being read.</param>
    /// <param name="pending">The layer, or <see langword="null"/>.</param>
    private static void Flush(LayerNodeStack stack, PendingLayer? pending)
    {
        if (pending is { } layer)
        {
            stack.Push(new(layer.Name, layer.Id, layer.Children));
        }
    }

    /// <summary>A layer of an /Order level whose children, from arrays that follow it, are still being read.</summary>
    /// <param name="Name">The layer's name.</param>
    /// <param name="Id">The layer's object number.</param>
    /// <param name="Children">The children found so far.</param>
    private readonly record struct PendingLayer(string Name, int Id, PdfLayerNode[] Children);
}

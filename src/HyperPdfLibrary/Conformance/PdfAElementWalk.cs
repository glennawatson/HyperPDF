// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Lists the structure elements under a structure tree root, skipping marked-content and object references.</summary>
internal static class PdfAElementWalk
{
    /// <summary>The deepest structure tree walked.</summary>
    private const int MaxDepth = 256;

    /// <summary>Lists the structure elements below a root, each once.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="root">The structure tree root.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The elements, not including the root.</returns>
    internal static List<PdfDictionary> Elements(PdfObjectStore store, PdfDictionary root, CancellationToken cancellationToken)
    {
        var elements = new List<PdfDictionary>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<PdfAElementNode>();
        pending.Push(new(root, 0));
        while (pending.TryPop(out var node))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(node.Element) || node.Depth > MaxDepth)
            {
                continue;
            }

            if (node.Depth > 0)
            {
                elements.Add(node.Element);
            }

            PushChildren(store, pending, node);
        }

        return elements;
    }

    /// <summary>Queues the structure elements among a node's /K children.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="pending">The nodes still to visit.</param>
    /// <param name="node">The node.</param>
    private static void PushChildren(PdfObjectStore store, Stack<PdfAElementNode> pending, PdfAElementNode node)
    {
        var kids = node.Element.Get(KnownName.K);
        if (kids.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                PushChild(pending, StoreReading.Resolve(store, array.GetRaw(i)), node.Depth + 1);
            }
        }
        else
        {
            PushChild(pending, kids, node.Depth + 1);
        }
    }

    /// <summary>Queues a child when it is a structure element rather than a marked-content or object reference.</summary>
    /// <param name="pending">The nodes still to visit.</param>
    /// <param name="child">The child.</param>
    /// <param name="depth">Its depth.</param>
    private static void PushChild(Stack<PdfAElementNode> pending, PdfValue child, int depth)
    {
        if (child.AsDictionary() is { } element && !element.IsName(KnownName.Type, KnownName.MCR) && !element.IsName(KnownName.Type, KnownName.OBJR))
        {
            pending.Push(new(element, depth));
        }
    }
}

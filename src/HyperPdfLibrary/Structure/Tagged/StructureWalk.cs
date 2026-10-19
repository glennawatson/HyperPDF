// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Visits every element of a structure tree without allocating a collection for the walk: the pending elements live in one pooled array.</summary>
internal static class StructureWalk
{
    /// <summary>Visits every element, parents before children.</summary>
    /// <typeparam name="TState">The type of the state the visit reads.</typeparam>
    /// <param name="tree">The tree.</param>
    /// <param name="state">The state passed to each visit.</param>
    /// <param name="visit">The visit; use a <see langword="static"/> lambda.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Each<TState>(PdfStructureTree tree, TState state, Action<PdfStructureElement, TState> visit, CancellationToken cancellationToken)
    {
        var stack = ArrayPool<PdfStructureElement>.Shared.Rent(tree.ElementCount + 1);
        try
        {
            var count = 0;
            for (var i = tree.Roots.Count - 1; i >= 0; i--)
            {
                stack[count] = tree.Roots[i];
                count++;
            }

            while (count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count--;
                var element = stack[count];
                visit(element, state);
                count = Push(element, stack, count);
            }
        }
        finally
        {
            ArrayPool<PdfStructureElement>.Shared.Return(stack, clearArray: true);
        }
    }

    /// <summary>Puts an element's child elements on the stack, last first so the first child is visited first.</summary>
    /// <param name="element">The element.</param>
    /// <param name="stack">The stack.</param>
    /// <param name="count">How many elements the stack holds.</param>
    /// <returns>How many it holds afterwards.</returns>
    private static int Push(PdfStructureElement element, PdfStructureElement[] stack, int count)
    {
        var kids = element.KidSpan;
        for (var i = kids.Length - 1; i >= 0; i--)
        {
            if (kids[i].Element is not { } child)
            {
                continue;
            }

            stack[count] = child;
            count++;
        }

        return count;
    }
}

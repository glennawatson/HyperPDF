// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Lists the structure elements of the structure tree as veraPDF reaches them: through <c>/K</c>, taking dictionaries
/// whose <c>/Type</c> is absent or StructElem, once each.
/// </summary>
internal static class PdfAStructureWalk
{
    /// <summary>Lists the structure elements.</summary>
    /// <param name="context">The validation.</param>
    /// <returns>The elements in tree order.</returns>
    /// <exception cref="OperationCanceledException">The validation was cancelled.</exception>
    internal static List<PdfDictionary> Elements(PdfAValidationContext context)
    {
        var elements = new List<PdfDictionary>();
        if (PdfAFontFacts.Dictionary(context.Document.Catalog.Get(KnownName.StructTreeRoot)) is not { } root)
        {
            return elements;
        }

        HashSet<PdfDictionary> seen = [with(ReferenceEqualityComparer.Instance)];
        var pending = new Stack<PdfDictionary>();
        Push(root, pending);
        while (pending.TryPop(out var element))
        {
            if (!seen.Add(element))
            {
                continue;
            }

            context.CancellationToken.ThrowIfCancellationRequested();
            elements.Add(element);
            Push(element, pending);
        }

        return elements;
    }

    /// <summary>Queues the structure element children of a node, last first so they pop in order.</summary>
    /// <param name="node">The root or a structure element.</param>
    /// <param name="pending">The queue.</param>
    private static void Push(PdfDictionary node, Stack<PdfDictionary> pending)
    {
        var children = node.Get(KnownName.K);
        if (IsElement(children) is { } single)
        {
            pending.Push(single);
            return;
        }

        var array = children.AsArray();
        for (var i = (array?.Count ?? 0) - 1; i >= 0; i--)
        {
            if (IsElement(array!.Get(i)) is { } child)
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>Gets a value as a structure element: a dictionary whose /Type is absent or StructElem.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The element, or null.</returns>
    private static PdfDictionary? IsElement(PdfValue value) =>
        PdfAFontFacts.Dictionary(value) is { } dictionary && (!dictionary.Get(KnownName.Type).TryGetName(out var type) || type.Is(KnownName.StructElem)) ? dictionary : null;
}

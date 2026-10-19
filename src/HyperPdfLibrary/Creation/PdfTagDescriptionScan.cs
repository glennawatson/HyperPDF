// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Creation;

/// <summary>Checks description strings before writing a created document's accessibility claim.</summary>
internal static class PdfTagDescriptionScan
{
    /// <summary>Counts figures without descriptions and PDF/UA-1 formulas without Alt.</summary>
    /// <param name="root">The created tree's root, or null for an untagged document.</param>
    /// <param name="pools">The document's reusable work storage.</param>
    /// <param name="part">The requested PDF/UA part.</param>
    /// <param name="cancellationToken">Cancels the tree walk.</param>
    /// <returns>The number of elements missing required description strings.</returns>
    internal static int Count(PdfTagElement? root, ScratchPools pools, PdfUaConformance part, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (root is null)
        {
            return 0;
        }

        var slots = pools.Slots<Stack<PdfTagElement>>();
        var pending = slots.Take() ?? new Stack<PdfTagElement>();
        try
        {
            var count = 0;
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var element = pending.Pop();
                count += HasMissingDescription(element, part) ? 1 : 0;
                foreach (var kid in element.Kids)
                {
                    if (kid.Element is { } child)
                    {
                        pending.Push(child);
                    }
                }
            }

            return count;
        }
        finally
        {
            pending.Clear();
            _ = slots.Offer(pending);
        }
    }

    /// <summary>Distinguishes missing entries from explicitly empty strings.</summary>
    /// <param name="element">The created element.</param>
    /// <param name="part">The requested PDF/UA part.</param>
    /// <returns>Whether a required description string is missing.</returns>
    internal static bool HasMissingDescription(PdfTagElement element, PdfUaConformance part) => element.Type switch
    {
        "Figure" => element.Options.AlternativeText is null && element.Options.ActualText is null,

        // ISO 14289-1 7.7 requires Alt separately; PDF/UA-2 has distinct MathML and scientific-formula rules.
        "Formula" => part == PdfUaConformance.Part1 && element.Options.AlternativeText is null,
        _ => false,
    };
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Finds the structure element of a marked-content id through the structure tree's /ParentTree number tree.</summary>
internal static class PdfAParentTree
{
    /// <summary>The items of a /Nums key and value pair.</summary>
    private const int PairLength = 2;

    /// <summary>Finds the structure element that owns a marked-content id.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="structParents">The page's or stream's /StructParents key.</param>
    /// <param name="mcid">The marked-content id.</param>
    /// <returns>The structure element, or null.</returns>
    internal static PdfDictionary? Element(PdfAValidationContext context, int structParents, int mcid)
    {
        var root = PdfAFontFacts.Dictionary(context.Document.Catalog.Get(KnownName.StructTreeRoot));
        var node = PdfAFontFacts.Dictionary(root?.Get(KnownName.ParentTree) ?? default);
        var parents = node is null ? null : Find(node, structParents, 0)?.AsArray();
        return parents is not null && mcid < parents.Count ? PdfAFontFacts.Dictionary(parents.Get(mcid)) : null;
    }

    /// <summary>Finds a key in a number tree node.</summary>
    /// <param name="node">The node.</param>
    /// <param name="key">The key.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The resolved value, or null.</returns>
    private static PdfValue? Find(PdfDictionary node, int key, int depth)
    {
        if (node.GetArray(KnownName.Nums) is { } numbers)
        {
            for (var i = 0; i + 1 < numbers.Count; i += PairLength)
            {
                if (numbers.Get(i) is { Kind: PdfKind.Integer } number && number.AsInteger() == key)
                {
                    return numbers.Get(i + 1);
                }
            }
        }

        var kids = node.GetArray(KnownName.Kids);
        for (var i = 0; kids is not null && depth < PdfLimits.MaxNesting && i < kids.Count; i++)
        {
            if (PdfAFontFacts.Dictionary(kids.Get(i)) is { } kid && Find(kid, key, depth + 1) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A level A rules on structure types (ISO 19005-1, 6.8.3.4; ISO 19005-2 and ISO 19005-3, 6.7.3.4): every element
/// type maps to a standard type through the role map, without circular mappings, and standard types are not remapped.
/// Like veraPDF, an element is checked when its type does not resolve to a standard type.
/// </summary>
internal static class PdfAStructureElementRules
{
    /// <summary>The PDF/A part based on PDF 1.4, which stops at the first standard type.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part based on PDF 2.0, whose structure namespaces these rules do not cover.</summary>
    private const int Part4 = 4;

    /// <summary>The standard structure types of PDF 1.4.</summary>
    private static readonly string[] Pdf14Types =
    [
        "Document", "Part", "Div", "Caption", "Sect", "NonStruct", "H", "P", "L", "LI", "Lbl", "LBody", "Table", "TR", "TH", "TD",
        "Span", "Link", "Figure", "Formula", "Form", "Art", "BlockQuote", "TOC", "TOCI", "Index", "Private", "Quote", "Note",
        "Reference", "BibEntry", "Code", "H1", "H2", "H3", "H4", "H5", "H6",
    ];

    /// <summary>The standard structure types PDF 1.7 adds to those of PDF 1.4.</summary>
    private static readonly string[] Pdf17Types = ["THead", "TBody", "TFoot", "Annot", "Ruby", "Warichu", "RB", "RT", "RP", "WT", "WP"];

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.StructureCircularMapping,
        PdfARule.StructureStandardType,
        PdfARule.StructureRemappedStandardType,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        if (context.Part == Part4 || context.Document.Catalog.Get(KnownName.StructTreeRoot).Kind != PdfKind.Dictionary)
        {
            return;
        }

        var root = context.Document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        var tree = new PdfAStructureTree(Standard(context), RoleMap(root), context.Part == Part1);
        HashSet<PdfDictionary> visited = [with(ReferenceEqualityComparer.Instance)];
        var pending = new Stack<PdfDictionary>();
        PushChildren(root, pending);
        while (pending.TryPop(out var element))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!visited.Add(element))
            {
                continue;
            }

            CheckElement(context, tree, element);
            PushChildren(element, pending);
        }
    }

    /// <summary>Checks one structure element whose type veraPDF does not resolve to a standard type.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="tree">The role map and standard types.</param>
    /// <param name="element">The element.</param>
    private static void CheckElement(PdfAValidationContext context, PdfAStructureTree tree, PdfDictionary element)
    {
        const string where = "structure element";
        if (!element.Get(KnownName.S).TryGetName(out var type))
        {
            context.Fail(PdfARule.StructureStandardType, where);
            return;
        }

        if (!StandardType(tree, type).IsNone)
        {
            return;
        }

        var circular = HasCircularMapping(tree, type);
        var remapped = tree.Standard.Contains(type);
        context.Require(!circular, PdfARule.StructureCircularMapping, where);
        context.Require(!remapped, PdfARule.StructureRemappedStandardType, where);
        context.Require(remapped || circular, PdfARule.StructureStandardType, where);
    }

    /// <summary>Finds the standard type a structure type maps to through the role map, as veraPDF does.</summary>
    /// <param name="tree">The role map and standard types.</param>
    /// <param name="type">The type.</param>
    /// <returns>The standard type, or none when the chain reaches none or loops.</returns>
    private static PdfName StandardType(PdfAStructureTree tree, PdfName type)
    {
        var visited = new HashSet<PdfName> { type };
        var mapped = tree.RoleMap.TryGetValue(type, out var next);
        if ((tree.StopAtStandard || !mapped || visited.Contains(next)) && tree.Standard.Contains(type))
        {
            return type;
        }

        while (mapped && visited.Add(next))
        {
            if (tree.Standard.Contains(next))
            {
                return next;
            }

            mapped = tree.RoleMap.TryGetValue(next, out next);
        }

        return default;
    }

    /// <summary>Determines whether following the role map from a type comes back to a type already seen.</summary>
    /// <param name="tree">The role map and standard types.</param>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> when the mapping is circular.</returns>
    private static bool HasCircularMapping(PdfAStructureTree tree, PdfName type)
    {
        var visited = new HashSet<PdfName> { type };
        var current = type;
        while (tree.RoleMap.TryGetValue(current, out var next))
        {
            if (!visited.Add(next))
            {
                return true;
            }

            current = next;
        }

        return false;
    }

    /// <summary>Pushes the structure element children of a node: dictionaries whose type is absent or StructElem.</summary>
    /// <param name="node">The structure tree root or an element.</param>
    /// <param name="pending">The elements still to check.</param>
    private static void PushChildren(PdfDictionary node, Stack<PdfDictionary> pending)
    {
        var kids = node.Get(KnownName.K);
        if (AsElement(kids) is { } single)
        {
            pending.Push(single);
            return;
        }

        var array = kids.AsArray();
        for (var i = array is null ? -1 : array.Count - 1; i >= 0; i--)
        {
            if (AsElement(array!.Get(i)) is { } element)
            {
                pending.Push(element);
            }
        }
    }

    /// <summary>Gets a value as a structure element: a dictionary, not a stream, whose type is absent or StructElem.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The element, or null.</returns>
    private static PdfDictionary? AsElement(PdfValue value)
    {
        if (value.Kind != PdfKind.Dictionary)
        {
            return null;
        }

        var dictionary = value.AsDictionary()!;
        var type = dictionary.GetName(KnownName.Type);
        return type.IsNone || type.ToKnownName() == KnownName.StructElem ? dictionary : null;
    }

    /// <summary>Reads the role map: the entries whose value is a name.</summary>
    /// <param name="root">The structure tree root.</param>
    /// <returns>The mapping.</returns>
    private static Dictionary<PdfName, PdfName> RoleMap(PdfDictionary root)
    {
        var map = new Dictionary<PdfName, PdfName>();
        if (root.Get(KnownName.RoleMap).Kind != PdfKind.Dictionary)
        {
            return map;
        }

        var entries = root.GetDictionary(KnownName.RoleMap)!;
        for (var i = 0; i < entries.Count; i++)
        {
            if (StoreReading.Resolve(entries.Owner!, entries.GetValueAt(i)).TryGetName(out var value))
            {
                map[entries.GetKeyAt(i)] = value;
            }
        }

        return map;
    }

    /// <summary>Interns the standard structure types of the part's PDF version.</summary>
    /// <param name="context">The validation.</param>
    /// <returns>The types.</returns>
    private static HashSet<PdfName> Standard(PdfAValidationContext context)
    {
        var names = context.Document.Objects.Names;
        var standard = new HashSet<PdfName>();
        foreach (var spelling in Pdf14Types)
        {
            _ = standard.Add(names.Intern(spelling));
        }

        if (context.Part != Part1)
        {
            foreach (var spelling in Pdf17Types)
            {
                _ = standard.Add(names.Intern(spelling));
            }
        }

        return standard;
    }
}

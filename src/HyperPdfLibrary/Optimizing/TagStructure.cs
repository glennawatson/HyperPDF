// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Writes the inferred structure elements, the parent tree and the catalog entries into the working copy.</summary>
internal static class TagStructure
{
    /// <summary>The entries of a structure element.</summary>
    private const int ElementEntries = 5;

    /// <summary>Creates a page's section and its elements, and the page's parent tree entry.</summary>
    /// <param name="state">The pass.</param>
    /// <param name="page">The page.</param>
    /// <param name="build">The elements, in reading order.</param>
    /// <param name="labels">Each unit's label.</param>
    /// <param name="key">The page's parent tree key.</param>
    internal static void AddSection(TaggerState state, PdfPage page, ref TagBuild build, ReadOnlySpan<TagLabel> labels, int key)
    {
        var store = state.Document.Objects;
        var pageRef = PdfValue.FromReference(page.Id);
        var section = StoreEditing.Add(store, PdfValue.Null);
        var byMcid = ArrayPool<PdfValue>.Shared.Rent(labels.Length);
        byMcid.AsSpan(0, labels.Length).Clear();
        try
        {
            var kids = new PdfArray(store, CountChildren(ref build, 0, build.NodeCount));
            var context = new SectionContext(pageRef, labels, byMcid.AsSpan(0, labels.Length));
            var used = 0;
            var index = 0;
            while (index < build.NodeCount)
            {
                kids.Add(PdfValue.FromReference(AddElement(state, ref build, ref index, section, in context, ref used)));
            }

            StoreEditing.Replace(store, section, PdfValue.FromDictionary(Element(state, OptimizerNames.Sect, PdfValue.FromReference(state.DocumentElement), pageRef, PdfValue.FromArray(kids))));
            state.AddSection(PdfValue.FromReference(section));
            state.NextKey++;
            state.AddParentEntry(PdfValue.FromInteger(key));
            state.AddParentEntry(PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromArray(new(store, byMcid.AsSpan(0, used))))));
        }
        finally
        {
            ArrayPool<PdfValue>.Shared.Return(byMcid, true);
        }
    }

    /// <summary>Creates the document element and the structure tree root, and points the catalog at them.</summary>
    /// <param name="state">The pass.</param>
    internal static void FinishTree(TaggerState state)
    {
        var store = state.Document.Objects;
        var root = StoreEditing.Add(store, PdfValue.Null);
        var sections = new PdfArray(store, state.Sections);
        var document = Element(state, OptimizerNames.Document, PdfValue.FromReference(root), PdfValue.Null, PdfValue.FromArray(sections));
        StoreEditing.Replace(store, state.DocumentElement, PdfValue.FromDictionary(document));
        var parentTree = new PdfDictionary(store, 1);
        parentTree.Set(KnownName.Nums, PdfValue.FromArray(new(store, state.ParentTree)));
        var tree = new PdfDictionary(store, ElementEntries);
        tree.Set(KnownName.Type, PdfValue.FromName(KnownName.StructTreeRoot));
        tree.Set(KnownName.K, PdfValue.FromReference(state.DocumentElement));
        tree.Set(KnownName.ParentTree, PdfValue.FromDictionary(parentTree));
        tree.Set(OptimizerNames.ParentTreeNextKey, PdfValue.FromInteger(state.NextKey));
        StoreEditing.Replace(store, root, PdfValue.FromDictionary(tree));
        var catalogRef = store.Trailer.GetRaw(KnownName.Root);
        var catalog = StoreReading.Resolve(store, catalogRef).AsDictionary()!.Clone();
        catalog.Set(KnownName.StructTreeRoot, PdfValue.FromReference(root));
        var markInfo = catalog.GetDictionary(KnownName.MarkInfo)?.Clone() ?? new PdfDictionary(store, 1);
        markInfo.Set(OptimizerNames.Marked, PdfValue.FromBoolean(true));
        catalog.Set(KnownName.MarkInfo, PdfValue.FromDictionary(markInfo));
        StoreEditing.Replace(store, catalogRef.AsReference(), PdfValue.FromDictionary(catalog));
        PdfDocumentOptimizing.RefreshAfterOptimizerEdit(state.Document);
    }

    /// <summary>Gets the name of a header scope.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The name.</returns>
    private static PdfName ScopeName(PdfTableScope scope) => scope switch
    {
        PdfTableScope.Row => OptimizerNames.ScopeRow,
        PdfTableScope.Both => OptimizerNames.ScopeBoth,
        _ => OptimizerNames.ScopeColumn,
    };

    /// <summary>Counts the elements directly under a range of the element list.</summary>
    /// <param name="build">The elements.</param>
    /// <param name="start">The first index of the range.</param>
    /// <param name="end">The index after the range.</param>
    /// <returns>The number of elements at the top of the range.</returns>
    private static int CountChildren(ref TagBuild build, int start, int end)
    {
        var count = 0;
        for (var index = start; index < end; index += build.Nodes[index].Size)
        {
            count++;
        }

        return count;
    }

    /// <summary>Creates an element and, below it, the elements of its children.</summary>
    /// <param name="state">The pass.</param>
    /// <param name="build">The elements.</param>
    /// <param name="index">The element's index; moved past its subtree.</param>
    /// <param name="parent">The parent element.</param>
    /// <param name="context">The page, labels and parent tree entries.</param>
    /// <param name="used">The marked content ids used so far; updated.</param>
    /// <returns>The element's object.</returns>
    private static PdfObjectId AddElement(TaggerState state, ref TagBuild build, ref int index, PdfObjectId parent, in SectionContext context, ref int used)
    {
        var store = state.Document.Objects;
        var node = build.Nodes[index];
        var end = index + node.Size;
        index++;
        var element = StoreEditing.Add(store, PdfValue.Null);
        var kids = new PdfArray(store, node.UnitCount + CountChildren(ref build, index, end));
        for (var i = 0; i < node.UnitCount; i++)
        {
            var mcid = context.Labels[build.OwnedUnits[node.UnitStart + i]].Mcid;
            kids.Add(PdfValue.FromInteger(mcid));
            context.ByMcid[mcid] = PdfValue.FromReference(element);
            used = Math.Max(used, mcid + 1);
        }

        while (index < end)
        {
            kids.Add(PdfValue.FromReference(AddElement(state, ref build, ref index, element, in context, ref used)));
        }

        var dictionary = Element(state, node.Tag, PdfValue.FromReference(parent), context.Page, PdfValue.FromArray(kids));
        SetDetails(state, dictionary, in node);
        StoreEditing.Replace(store, element, PdfValue.FromDictionary(dictionary));
        state.Figures += node.IsFigure && string.IsNullOrEmpty(node.AlternateText) ? 1 : 0;
        return element;
    }

    /// <summary>Sets an element's description, guessed language and table cell attributes.</summary>
    /// <param name="state">The pass.</param>
    /// <param name="dictionary">The element's dictionary.</param>
    /// <param name="node">The element.</param>
    private static void SetDetails(TaggerState state, PdfDictionary dictionary, in TagNode node)
    {
        if (!string.IsNullOrEmpty(node.AlternateText))
        {
            dictionary.Set(KnownName.Alt, PdfValue.FromString(PdfText.Encode(node.AlternateText)));
        }

        if (!string.IsNullOrEmpty(node.Language))
        {
            dictionary.Set(KnownName.Lang, PdfValue.FromString(PdfText.Encode(node.Language)));
        }

        if (node.Cell is { } cell && CellAttributes(state, in cell) is { } attributes)
        {
            dictionary.Set(KnownName.A, PdfValue.FromDictionary(attributes));
        }
    }

    /// <summary>Builds the table attribute object of a cell (ISO 32000-2 14.8.5.7).</summary>
    /// <param name="state">The pass.</param>
    /// <param name="cell">The cell's scope and spans.</param>
    /// <returns>The attribute dictionary, or <see langword="null"/> for a plain cell.</returns>
    private static PdfDictionary? CellAttributes(TaggerState state, in TagCell cell)
    {
        if (cell.Scope == PdfTableScope.None && cell.RowSpan <= 1 && cell.ColumnSpan <= 1)
        {
            return null;
        }

        var attributes = new PdfDictionary(state.Document.Objects, ElementEntries);
        attributes.Set(OptimizerNames.Owner, PdfValue.FromName(OptimizerNames.Table));
        if (cell.Scope != PdfTableScope.None)
        {
            attributes.Set(OptimizerNames.Scope, PdfValue.FromName(ScopeName(cell.Scope)));
        }

        if (cell.RowSpan > 1)
        {
            attributes.Set(OptimizerNames.RowSpan, PdfValue.FromInteger(cell.RowSpan));
        }

        if (cell.ColumnSpan > 1)
        {
            attributes.Set(OptimizerNames.ColSpan, PdfValue.FromInteger(cell.ColumnSpan));
        }

        return attributes;
    }

    /// <summary>Builds a structure element dictionary.</summary>
    /// <param name="state">The pass.</param>
    /// <param name="type">The structure type.</param>
    /// <param name="parent">The parent element or root.</param>
    /// <param name="page">The page, or null for the document element.</param>
    /// <param name="kids">The children: marked content ids or elements.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Element(TaggerState state, PdfName type, PdfValue parent, PdfValue page, PdfValue kids)
    {
        var element = new PdfDictionary(state.Document.Objects, ElementEntries);
        element.Set(KnownName.Type, PdfValue.FromName(KnownName.StructElem));
        element.Set(KnownName.S, PdfValue.FromName(type));
        element.Set(KnownName.P, parent);
        if (!page.IsNull)
        {
            element.Set(KnownName.Pg, page);
        }

        element.Set(KnownName.K, kids);
        return element;
    }
}

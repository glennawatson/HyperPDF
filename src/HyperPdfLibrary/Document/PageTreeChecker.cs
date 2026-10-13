// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Checks the page tree against the rules of ISO 32000: node types, /Kids, /Count, /Parent and the page boxes.</summary>
/// <param name="objects">The document's objects.</param>
/// <param name="faults">The list receiving each fault.</param>
internal ref struct PageTreeChecker(PdfObjectStore objects, List<PdfDiagnostic> faults)
{
    /// <summary>The bits in a word of the visited set.</summary>
    private const int WordBits = 64;

    /// <summary>The page tree nodes already visited, one bit per object number, so a loop is checked once; a pooled array.</summary>
    private ulong[]? _seen;

    /// <summary>The number of object numbers the visited set holds.</summary>
    private int _seenLimit;

    /// <summary>Checks the tree from the catalog, then gives back the pooled visited set.</summary>
    internal void Check()
    {
        var root = objects.Catalog.GetRaw(KnownName.Pages);
        if (StoreReading.Resolve(objects, root).AsDictionary() is null)
        {
            Add(PdfDiagnosticCode.BadStructure, "The catalog has no page tree (/Pages).", 0);
            return;
        }

        var words = (objects.Size / WordBits) + 1;
        _seen = ArrayPool<ulong>.Shared.Rent(words);
        _seen.AsSpan(0, words).Clear();
        _seenLimit = words * WordBits;
        try
        {
            _ = CheckNode(root, 0, false, 0);
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(_seen);
            _seen = null;
        }
    }

    /// <summary>Marks a node as visited.</summary>
    /// <param name="number">The node's object number.</param>
    /// <returns><see langword="true"/> when the node was not visited before.</returns>
    private readonly bool TryMarkSeen(int number)
    {
        if ((uint)number >= (uint)_seenLimit)
        {
            // A number beyond the table cannot name a stored node; the depth limit still bounds a loop.
            return true;
        }

        ref var word = ref _seen![number / WordBits];
        var bit = 1UL << (number % WordBits);
        if ((word & bit) != 0)
        {
            return false;
        }

        word |= bit;
        return true;
    }

    /// <summary>Checks a node and the nodes below it.</summary>
    /// <param name="raw">The node, usually a reference.</param>
    /// <param name="parentNumber">The object number of the node that lists this one, or 0.</param>
    /// <param name="hasMediaBox">Whether an ancestor gives /MediaBox.</param>
    /// <param name="depth">The depth in the tree.</param>
    /// <returns>The number of pages below the node, counting a page as one.</returns>
    private int CheckNode(PdfValue raw, int parentNumber, bool hasMediaBox, int depth)
    {
        var id = raw.IsReference ? raw.AsReference() : default;
        if (StoreReading.Resolve(objects, raw).AsDictionary() is not { } node)
        {
            Add(PdfDiagnosticCode.BadStructure, "A page tree entry is not a dictionary.", id.Number);
            return 0;
        }

        if (depth > PdfLimits.MaxPageTreeDepth || (id.IsValid && !TryMarkSeen(id.Number)))
        {
            return 0;
        }

        CheckParent(node, id.Number, parentNumber);
        var kids = node.GetArray(KnownName.Kids);
        if (kids is null || node.IsName(KnownName.Type, KnownName.Page))
        {
            CheckPage(node, id.Number, hasMediaBox);
            return 1;
        }

        return CheckInterior(node, kids, id.Number, hasMediaBox || node.ContainsKey(KnownName.MediaBox), depth);
    }

    /// <summary>Checks a node that holds kids.</summary>
    /// <param name="node">The node.</param>
    /// <param name="kids">Its /Kids.</param>
    /// <param name="number">The node's object number.</param>
    /// <param name="hasMediaBox">Whether the node or an ancestor gives /MediaBox.</param>
    /// <param name="depth">The depth of the node.</param>
    /// <returns>The pages below it.</returns>
    private int CheckInterior(PdfDictionary node, PdfArray kids, int number, bool hasMediaBox, int depth)
    {
        if (!node.IsName(KnownName.Type, KnownName.Pages))
        {
            Add(PdfDiagnosticCode.BadStructure, "A page tree node has no /Type /Pages.", number);
        }

        var pages = 0;
        for (var i = 0; i < kids.Count; i++)
        {
            pages += CheckNode(kids.GetRaw(i), number, hasMediaBox, depth + 1);
        }

        if (node.GetInt32(KnownName.Count, -1) != pages)
        {
            Add(PdfDiagnosticCode.BadStructure, "A page tree node's /Count does not match the pages below it.", number);
        }

        return pages;
    }

    /// <summary>Checks a node's /Parent.</summary>
    /// <param name="node">The node.</param>
    /// <param name="number">The node's object number.</param>
    /// <param name="parentNumber">The object number of the node that lists it, or 0 for the root.</param>
    private void CheckParent(PdfDictionary node, int number, int parentNumber)
    {
        var parent = node.GetRaw(KnownName.Parent);
        if (parentNumber == 0)
        {
            return;
        }

        if (!parent.IsReference || parent.AsReference().Number != parentNumber)
        {
            Add(PdfDiagnosticCode.BadStructure, "A page tree node's /Parent is missing or names another node.", number);
        }
    }

    /// <summary>Checks a page's type and boxes.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="number">The page object's number.</param>
    /// <param name="inheritedMediaBox">Whether an ancestor gives /MediaBox.</param>
    private void CheckPage(PdfDictionary page, int number, bool inheritedMediaBox)
    {
        if (!page.IsName(KnownName.Type, KnownName.Page))
        {
            Add(PdfDiagnosticCode.BadStructure, "A page has no /Type /Page.", number);
        }

        if (!inheritedMediaBox && !page.ContainsKey(KnownName.MediaBox))
        {
            Add(PdfDiagnosticCode.BadStructure, "A page has no /MediaBox, here or in its ancestors.", number);
        }

        CheckBox(page, KnownName.MediaBox, number);
        CheckBox(page, KnownName.CropBox, number);
    }

    /// <summary>Reports a box that is swapped or unusable.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="key">The box key.</param>
    /// <param name="number">The page object's number.</param>
    private void CheckBox(PdfDictionary page, KnownName key, int number)
    {
        if (PageBoxes.Inspect(page, key) is PageBoxes.BoxState.Swapped or PageBoxes.BoxState.Unusable)
        {
            Add(PdfDiagnosticCode.BadPageBox, PageBoxes.BadBoxMessage, number);
        }
    }

    /// <summary>Adds a fault.</summary>
    /// <param name="code">The fault.</param>
    /// <param name="message">The description.</param>
    /// <param name="number">The object number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Add(PdfDiagnosticCode code, string message, int number) => faults.Add(new(code, message, number, StoreRepairs.GetEntryLocation(objects, number)));
}

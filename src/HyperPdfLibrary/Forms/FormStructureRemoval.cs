// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Takes removed widgets out of the structure tree: their object references (<c>/OBJR</c>) leave the elements that hold
/// them, an element left with no kid goes too when it is a <c>Form</c> element, and the parent tree forgets the widgets'
/// <c>/StructParent</c> keys, so nothing in the tree points at a deleted object (ISO 32000-2 14.7.2 and 14.7.4.4).
/// </summary>
[DebuggerDisplay("FormStructureRemoval: {_widgets.Count} widgets")]
internal sealed class FormStructureRemoval
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The transaction.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The structure tree root's object.</summary>
    private readonly PdfObjectId _rootId;

    /// <summary>The object numbers of the widgets being removed.</summary>
    private readonly HashSet<int> _widgets = [];

    /// <summary>The parent tree keys of the widgets being removed.</summary>
    private readonly HashSet<int> _keys = [];

    /// <summary>The elements that hold the widgets, by object number, with the widgets they hold.</summary>
    private readonly Dictionary<int, HashSet<int>> _holders = [];

    /// <summary>Copies of the dictionaries being changed, by object number.</summary>
    private readonly Dictionary<int, PdfDictionary> _copies = [];

    /// <summary>The elements taken out of the tree, by object number.</summary>
    private readonly List<int> _deleted = [];

    /// <summary>Initializes a new instance of the <see cref="FormStructureRemoval"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="rootId">The structure tree root's object.</param>
    private FormStructureRemoval(PdfDocument document, PdfEditTransaction transaction, PdfObjectId rootId)
    {
        _document = document;
        _transaction = transaction;
        _rootId = rootId;
    }

    /// <summary>Creates a remover for a tagged document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <returns>The remover; <see langword="null"/> when the document has no structure tree.</returns>
    internal static FormStructureRemoval? Create(PdfDocument document, PdfEditTransaction transaction)
    {
        var rootId = document.Catalog.GetRaw(KnownName.StructTreeRoot).AsReference();
        return rootId.IsValid && StoreReading.GetDictionary(document.Objects, rootId) is not null ? new(document, transaction, rootId) : null;
    }

    /// <summary>Notes a widget that is about to be deleted.</summary>
    /// <param name="widgetId">The widget's object.</param>
    /// <param name="widget">The widget dictionary, as it is before the deletion.</param>
    internal void Add(PdfObjectId widgetId, PdfDictionary widget)
    {
        _ = _widgets.Add(widgetId.Number);
        var key = widget.GetInt32(KnownName.StructParent, -1);
        if (key < 0)
        {
            return;
        }

        _ = _keys.Add(key);
        var tree = StoreReading.Resolve(_document.Objects, StoreReading.GetDictionary(_document.Objects, _rootId)!.GetRaw(KnownName.ParentTree)).AsDictionary();
        if (NameTree.FindNumberRaw(tree, key).AsReference() is not { IsValid: true } element)
        {
            return;
        }

        ref var held = ref CollectionsMarshal.GetValueRefOrAddDefault(_holders, element.Number, out _);
        held ??= [];
        _ = held.Add(widgetId.Number);
    }

    /// <summary>Writes the changes to the elements, the root and the parent tree.</summary>
    internal void Commit()
    {
        if (_widgets.Count == 0)
        {
            return;
        }

        var store = _document.Objects;
        var emptied = new Dictionary<int, HashSet<int>>();
        foreach (var (number, held) in _holders)
        {
            RemoveFromElement(number, held, emptied);
        }

        foreach (var (parent, children) in emptied)
        {
            RemoveChildren(parent, children);
        }

        var root = Copy(_rootId.Number);
        root.Set(KnownName.ParentTree, FormStructureTree.RemoveEntries(_transaction, store, root.GetRaw(KnownName.ParentTree), _keys, 0));
        foreach (var (number, dictionary) in _copies)
        {
            _transaction.Replace(new(number, 0), PdfValue.FromDictionary(dictionary));
        }

        foreach (var number in _deleted)
        {
            _transaction.Delete(new(number, 0));
        }
    }

    /// <summary>Takes widgets' object references out of an element; an element left empty is deleted when it is a Form element.</summary>
    /// <param name="number">The element's object number.</param>
    /// <param name="held">The widgets it holds.</param>
    /// <param name="emptied">Collects the deleted elements by their parent.</param>
    private void RemoveFromElement(int number, HashSet<int> held, Dictionary<int, HashSet<int>> emptied)
    {
        var element = Copy(number);
        var remaining = FormStructureTree.RemoveKids(_document.Objects, element.GetRaw(KnownName.K), held);
        if (remaining is null)
        {
            return;
        }

        if (remaining.Count > 0 || !element.IsName(KnownName.S, KnownName.Form))
        {
            SetKids(element, remaining);
            return;
        }

        _deleted.Add(number);
        _ = _copies.Remove(number);
        var parent = element.GetRaw(KnownName.P).AsReference();
        if (!parent.IsValid)
        {
            return;
        }

        ref var children = ref CollectionsMarshal.GetValueRefOrAddDefault(emptied, parent.Number, out _);
        children ??= [];
        _ = children.Add(number);
    }

    /// <summary>Takes deleted child elements out of their parent's kids.</summary>
    /// <param name="parent">The parent's object number.</param>
    /// <param name="children">The deleted children.</param>
    private void RemoveChildren(int parent, HashSet<int> children)
    {
        var element = Copy(parent);
        var remaining = FormStructureTree.RemoveKids(_document.Objects, element.GetRaw(KnownName.K), children);
        if (remaining is not null)
        {
            SetKids(element, remaining);
        }
    }

    /// <summary>Sets an element's kids, or removes the entry when none is left.</summary>
    /// <param name="element">The element copy.</param>
    /// <param name="kids">The kids.</param>
    private void SetKids(PdfDictionary element, PdfArray kids)
    {
        if (element.GetRaw(KnownName.K).AsReference() is { IsValid: true } id)
        {
            _transaction.Replace(id, PdfValue.FromArray(kids));
            return;
        }

        if (kids.Count == 0)
        {
            _ = element.Remove(KnownName.K);
            return;
        }

        element.Set(KnownName.K, PdfValue.FromArray(kids));
    }

    /// <summary>Gets the copy of an object's dictionary that this removal changes.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary Copy(int number)
    {
        ref var copy = ref CollectionsMarshal.GetValueRefOrAddDefault(_copies, number, out _);
        copy ??= StoreReading.GetDictionary(_document.Objects, new(number, 0))!.Clone();
        return copy;
    }
}

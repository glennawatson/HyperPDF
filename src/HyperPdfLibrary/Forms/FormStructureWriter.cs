// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Tags new widgets in a tagged document. Each widget gets a <c>Form</c> structure element whose kid is an object
/// reference (<c>/OBJR</c>) to the widget, and the widget gets a <c>/StructParent</c> key that the structure tree's
/// <c>/ParentTree</c> maps back to the element (ISO 32000-2 14.7.4.4 and 14.8.4.8.2; ISO 14289-1 7.18.4).
/// </summary>
[DebuggerDisplay("FormStructureWriter: {_widgets.Count} widgets")]
internal sealed class FormStructureWriter
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The transaction.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The structure tree root's object.</summary>
    private readonly PdfObjectId _rootId;

    /// <summary>The widgets to tag.</summary>
    private readonly List<TaggedWidget> _widgets = [];

    /// <summary>The next free key of the parent tree.</summary>
    private int _nextKey;

    /// <summary>Initializes a new instance of the <see cref="FormStructureWriter"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="rootId">The structure tree root's object.</param>
    private FormStructureWriter(PdfDocument document, PdfEditTransaction transaction, PdfObjectId rootId)
    {
        _document = document;
        _transaction = transaction;
        _rootId = rootId;
        _nextKey = FormStructureTree.NextKey(StoreReading.GetDictionary(document.Objects, rootId)!);
    }

    /// <summary>Creates a writer for a tagged document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <returns>The writer; <see langword="null"/> when the document has no structure tree to add to.</returns>
    internal static FormStructureWriter? Create(PdfDocument document, PdfEditTransaction transaction)
    {
        var rootId = document.Catalog.GetRaw(KnownName.StructTreeRoot).AsReference();
        return rootId.IsValid && StoreReading.GetDictionary(document.Objects, rootId) is not null ? new(document, transaction, rootId) : null;
    }

    /// <summary>Queues a widget to be tagged.</summary>
    /// <param name="widgetId">The widget's object.</param>
    /// <param name="pageId">The widget's page; not valid for a page written directly in the tree.</param>
    /// <returns>The <c>/StructParent</c> key the widget must carry.</returns>
    internal int Add(PdfObjectId widgetId, PdfObjectId pageId)
    {
        var key = _nextKey;
        _nextKey = key + 1;
        _widgets.Add(new(widgetId, pageId, key));
        return key;
    }

    /// <summary>Writes the structure elements and the parent tree entries.</summary>
    internal void Commit()
    {
        if (_widgets.Count == 0)
        {
            return;
        }

        var store = _document.Objects;
        var root = StoreReading.GetDictionary(store, _rootId)!.Clone();
        var parent = FormStructureTree.FindContainer(store, root, _rootId);
        var kids = new List<PdfValue>(_widgets.Count);
        var entries = new List<FormStructureTree.ParentEntry>(_widgets.Count);
        foreach (var widget in _widgets)
        {
            var id = _transaction.Add(default);
            _transaction.Replace(id, PdfValue.FromDictionary(BuildElement(widget, parent.Id)));
            kids.Add(PdfValue.FromReference(id));
            entries.Add(new(widget.Key, PdfValue.FromReference(id)));
        }

        if (parent.Id == _rootId)
        {
            FormStructureTree.AppendKids(_transaction, store, root, kids);
        }
        else
        {
            var container = parent.Dictionary.Clone();
            FormStructureTree.AppendKids(_transaction, store, container, kids);
            _transaction.Replace(parent.Id, PdfValue.FromDictionary(container));
        }

        root.Set(KnownName.ParentTree, FormStructureTree.AppendEntries(_transaction, store, root.GetRaw(KnownName.ParentTree), entries));
        root.Set(KnownName.ParentTreeNextKey, PdfValue.FromInteger(_nextKey));
        _transaction.Replace(_rootId, PdfValue.FromDictionary(root));
    }

    /// <summary>Builds the <c>Form</c> element for a widget.</summary>
    /// <param name="widget">The widget.</param>
    /// <param name="parent">The element or root that holds the new element.</param>
    /// <returns>The element dictionary.</returns>
    private PdfDictionary BuildElement(TaggedWidget widget, PdfObjectId parent)
    {
        var store = _document.Objects;
        var element = new PdfDictionary(store);
        element.Set(KnownName.Type, PdfValue.FromName(KnownName.StructElem));
        element.Set(KnownName.S, PdfValue.FromName(KnownName.Form));
        element.Set(KnownName.P, PdfValue.FromReference(parent));
        var reference = new PdfDictionary(store);
        reference.Set(KnownName.Type, PdfValue.FromName(KnownName.OBJR));
        reference.Set(KnownName.Obj, PdfValue.FromReference(widget.WidgetId));
        if (widget.PageId.IsValid)
        {
            element.Set(KnownName.Pg, PdfValue.FromReference(widget.PageId));
            reference.Set(KnownName.Pg, PdfValue.FromReference(widget.PageId));
        }

        element.Set(KnownName.K, PdfValue.FromDictionary(reference));
        return element;
    }

    /// <summary>A widget waiting to be tagged.</summary>
    /// <param name="WidgetId">The widget's object.</param>
    /// <param name="PageId">The widget's page.</param>
    /// <param name="Key">The widget's <c>/StructParent</c> key.</param>
    [DebuggerDisplay("TaggedWidget: {WidgetId} key {Key}")]
    private readonly record struct TaggedWidget(PdfObjectId WidgetId, PdfObjectId PageId, int Key);
}

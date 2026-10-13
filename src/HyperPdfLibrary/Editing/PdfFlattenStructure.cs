// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Keeps the structure tree of a tagged document consistent while annotations are flattened (ISO 32000-2 14.7, 14.8). An
/// annotation that a structure element refers to through an object reference (<c>/OBJR</c>) leaves the element when the
/// annotation goes: the reference becomes a marked content reference (<c>/MCR</c>) to the drawing that took the
/// annotation's place, and the parent tree entry of the annotation is removed. An annotation no element owns is drawn as
/// an artifact.
/// </summary>
[DebuggerDisplay("PdfFlattenStructure: {TaggedContent} tagged, {Artifacts} artifacts")]
internal sealed class PdfFlattenStructure
{
    /// <summary>The transaction the edit belongs to.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The document's parent tree.</summary>
    private readonly PdfParentTree _tree;

    /// <summary>The structure elements being changed, by object number.</summary>
    private readonly Dictionary<int, ElementEdit> _elements = [];

    /// <summary>The marked content ids handed out on each page, by page index.</summary>
    private readonly Dictionary<int, PageMarks> _pages = [];

    /// <summary>The objects the changed elements no longer use, which are deleted when they are no longer reachable.</summary>
    private readonly List<PdfObjectId> _dropped = [];

    /// <summary>Initializes a new instance of the <see cref="PdfFlattenStructure"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="tree">The document's parent tree.</param>
    private PdfFlattenStructure(PdfDocument document, PdfEditTransaction transaction, PdfParentTree tree)
    {
        _document = document;
        _transaction = transaction;
        _tree = tree;
    }

    /// <summary>Gets the number of drawings that took the place of an annotation in the structure.</summary>
    internal int TaggedContent { get; private set; }

    /// <summary>Gets the number of drawings marked as artifacts.</summary>
    internal int Artifacts { get; private set; }

    /// <summary>Gets the objects the changed elements no longer use.</summary>
    internal IReadOnlyList<PdfObjectId> Dropped => _dropped;

    /// <summary>Reads the structure of a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <returns>The structure, or <see langword="null"/> when the document is not tagged.</returns>
    internal static PdfFlattenStructure? Create(PdfDocument document, PdfEditTransaction transaction) =>
        PdfParentTree.Load(transaction, document.Objects) is { } tree ? new(document, transaction, tree) : null;

    /// <summary>
    /// Takes an annotation out of the structure and decides how its drawing is marked: as content of the element that owned
    /// the annotation, or as an artifact.
    /// </summary>
    /// <param name="pageIndex">The zero based index of the annotation's page.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="annotationId">The annotation's object, or an invalid id for an annotation written directly in the page.</param>
    /// <param name="drawn">Whether the annotation is drawn into the page content.</param>
    /// <returns>The marking for the drawing; none when nothing is drawn.</returns>
    internal PdfFlattenMarking Release(int pageIndex, PdfPage page, PdfDictionary annotation, PdfObjectId annotationId, bool drawn)
    {
        var key = annotation.GetInt32(KnownName.StructParent, -1);
        var owner = key >= 0 ? _tree.Get(key) : default;
        if (key >= 0)
        {
            _tree.Remove(key);
        }

        if (owner.IsReference && annotationId.IsValid && Edit(owner.AsReference()) is { } element)
        {
            if (!drawn)
            {
                _ = ReplaceKid(element.Dictionary, annotationId, default);
                return PdfFlattenMarking.None;
            }

            var found = Replace(element, pageIndex, page, annotationId);
            if (found.Kind == PdfFlattenMarkingKind.Content)
            {
                TaggedContent++;
                return found;
            }
        }

        if (!drawn)
        {
            return PdfFlattenMarking.None;
        }

        Artifacts++;
        return PdfFlattenMarking.Artifact;
    }

    /// <summary>Gets the parent tree key to give a page whose drawings were given marked content ids, when it had none.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The new key, or -1 when the page keeps the key it had.</returns>
    internal int GetNewPageKey(int pageIndex) => _pages.TryGetValue(pageIndex, out var marks) && marks.IsNewKey ? marks.Key : -1;

    /// <summary>Writes the changed elements and the parent tree.</summary>
    internal void Finish()
    {
        foreach (var edit in _elements.Values)
        {
            _transaction.Replace(edit.Id, PdfValue.FromDictionary(edit.Dictionary));
        }

        foreach (var marks in _pages.Values)
        {
            marks.Store(_transaction, _tree, _document.Objects);
        }

        _tree.Write();
    }

    /// <summary>Determines whether an element has an object reference to an annotation among its kids.</summary>
    /// <param name="element">The element.</param>
    /// <param name="annotationId">The annotation.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool HasReference(PdfDictionary element, PdfObjectId annotationId)
    {
        var kids = element.Get(KnownName.K);
        if (kids.AsArray() is not { } array)
        {
            return IsReferenceTo(kids.AsDictionary(), annotationId);
        }

        for (var i = 0; i < array.Count; i++)
        {
            if (IsReferenceTo(array.GetDictionary(i), annotationId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a kid is an object reference to an annotation.</summary>
    /// <param name="kid">The kid, or <see langword="null"/>.</param>
    /// <param name="annotationId">The annotation.</param>
    /// <returns><see langword="true"/> for an /OBJR whose /Obj is the annotation.</returns>
    private static bool IsReferenceTo(PdfDictionary? kid, PdfObjectId annotationId) =>
        kid is not null && kid.GetRaw(KnownName.Obj).AsReference().Number == annotationId.Number && !kid.ContainsKey(KnownName.S);

    /// <summary>Replaces the object reference to an annotation with a marked content reference to a new drawing.</summary>
    /// <param name="element">The element that owned the annotation.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotationId">The annotation.</param>
    /// <returns>The marking, or an artifact marking when the element had no reference to the annotation.</returns>
    private PdfFlattenMarking Replace(ElementEdit element, int pageIndex, PdfPage page, PdfObjectId annotationId)
    {
        if (!HasReference(element.Dictionary, annotationId))
        {
            return PdfFlattenMarking.Artifact;
        }

        var mcid = GetMarks(pageIndex, page).Add(PdfValue.FromReference(element.Id));
        var reference = new PdfDictionary(_document.Objects);
        reference.Set(KnownName.Type, PdfValue.FromName(KnownName.MCR));
        if (page.Id.IsValid)
        {
            reference.Set(KnownName.Pg, PdfValue.FromReference(page.Id));
        }

        reference.Set(KnownName.MCID, PdfValue.FromInteger(mcid));
        _ = ReplaceKid(element.Dictionary, annotationId, PdfValue.FromDictionary(reference));
        return new(PdfFlattenMarkingKind.Content, mcid, element.Dictionary.GetName(KnownName.S));
    }

    /// <summary>Gets the marked content ids handed out on a page.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <returns>The page's marks.</returns>
    private PageMarks GetMarks(int pageIndex, PdfPage page)
    {
        if (_pages.TryGetValue(pageIndex, out var existing))
        {
            return existing;
        }

        var key = page.Dictionary.GetInt32(KnownName.StructParents, -1);
        var stored = key >= 0 ? _tree.Get(key) : default;
        var marks = key >= 0 && !stored.IsNull
            ? PageMarks.ForExisting(key, stored, StoreReading.Resolve(_document.Objects, stored).AsArray())
            : PageMarks.ForNew(_tree.TakeKey(), CountMarkedContent(page));
        _pages[pageIndex] = marks;
        return marks;
    }

    /// <summary>Counts the marked content ids a page's content already uses, as one more than the highest.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The first unused id.</returns>
    private int CountMarkedContent(PdfPage page)
    {
        var marks = new List<ScannedMark>();
        _ = MarkedContentScanner.Scan(_document, page, marks);
        var next = 0;
        foreach (var mark in marks)
        {
            next = Math.Max(next, mark.Mcid + 1);
        }

        return next;
    }

    /// <summary>Gets the editable copy of an element.</summary>
    /// <param name="id">The element's object.</param>
    /// <returns>The copy, or <see langword="null"/> when the object is not a dictionary.</returns>
    private ElementEdit? Edit(PdfObjectId id)
    {
        if (_elements.TryGetValue(id.Number, out var existing))
        {
            return existing;
        }

        if (StoreReading.GetDictionary(_document.Objects, id)?.Clone() is not { } copy)
        {
            return null;
        }

        var edit = new ElementEdit(id, copy);
        _elements[id.Number] = edit;
        return edit;
    }

    /// <summary>Replaces the object reference to an annotation among an element's kids, or removes it.</summary>
    /// <param name="element">The element copy.</param>
    /// <param name="annotationId">The annotation.</param>
    /// <param name="replacement">The new kid, or null to remove the reference.</param>
    /// <returns><see langword="true"/> when the element had the reference.</returns>
    private bool ReplaceKid(PdfDictionary element, PdfObjectId annotationId, PdfValue replacement)
    {
        var kids = element.Get(KnownName.K);
        if (kids.AsArray() is not { } array)
        {
            if (!IsReferenceTo(kids.AsDictionary(), annotationId))
            {
                return false;
            }

            DropObject(element.GetRaw(KnownName.K));
            if (replacement.IsNull)
            {
                _ = element.Remove(KnownName.K);
            }
            else
            {
                element.Set(KnownName.K, replacement);
            }

            return true;
        }

        var changed = new PdfArray(_document.Objects, array.Count);
        var found = false;
        for (var i = 0; i < array.Count; i++)
        {
            if (IsReferenceTo(array.GetDictionary(i), annotationId))
            {
                found = true;
                DropObject(array.GetRaw(i));
                if (!replacement.IsNull)
                {
                    changed.Add(replacement);
                }
            }
            else
            {
                changed.Add(array.GetRaw(i));
            }
        }

        if (found)
        {
            DropObject(element.GetRaw(KnownName.K));
            element.Set(KnownName.K, PdfValue.FromArray(changed));
        }

        return found;
    }

    /// <summary>Remembers an indirect object an element stopped using.</summary>
    /// <param name="value">The value as the element held it.</param>
    private void DropObject(PdfValue value)
    {
        if (value.IsReference)
        {
            _dropped.Add(value.AsReference());
        }
    }

    /// <summary>The marked content ids handed out on one page and the parent tree entry that lists their owners.</summary>
    [DebuggerDisplay("PageMarks: key {Key}, {_added.Count} added")]
    private sealed class PageMarks
    {
        /// <summary>The owners added, from marked content id <see cref="_first"/>.</summary>
        private readonly List<PdfValue> _added = [];

        /// <summary>The owners the entry listed before, with the entry as stored.</summary>
        private readonly PdfArray? _existing;

        /// <summary>The entry as stored, or null for a new key.</summary>
        private readonly PdfValue _stored;

        /// <summary>The first marked content id handed out.</summary>
        private readonly int _first;

        /// <summary>Initializes a new instance of the <see cref="PageMarks"/> class.</summary>
        /// <param name="key">The page's parent tree key.</param>
        /// <param name="stored">The entry as stored, or null.</param>
        /// <param name="existing">The array the entry resolves to, or null.</param>
        /// <param name="first">The first marked content id to hand out.</param>
        /// <param name="isNewKey">Whether the page did not have the key before.</param>
        private PageMarks(int key, PdfValue stored, PdfArray? existing, int first, bool isNewKey)
        {
            Key = key;
            _stored = stored;
            _existing = existing;
            _first = first;
            IsNewKey = isNewKey;
        }

        /// <summary>Gets the page's parent tree key.</summary>
        internal int Key { get; }

        /// <summary>Gets a value indicating whether the page did not have the key before.</summary>
        internal bool IsNewKey { get; }

        /// <summary>Starts the marks of a page that has a parent tree entry.</summary>
        /// <param name="key">The key.</param>
        /// <param name="stored">The entry as stored.</param>
        /// <param name="existing">The array the entry resolves to, or null.</param>
        /// <returns>The marks.</returns>
        internal static PageMarks ForExisting(int key, PdfValue stored, PdfArray? existing) => new(key, stored, existing, existing?.Count ?? 0, false);

        /// <summary>Starts the marks of a page that has no parent tree entry.</summary>
        /// <param name="key">The new key.</param>
        /// <param name="first">The first unused marked content id.</param>
        /// <returns>The marks.</returns>
        internal static PageMarks ForNew(int key, int first) => new(key, default, null, first, true);

        /// <summary>Takes the next marked content id for an owner.</summary>
        /// <param name="owner">A reference to the owning element.</param>
        /// <returns>The id.</returns>
        internal int Add(PdfValue owner)
        {
            var mcid = _first + _added.Count;
            _added.Add(owner);
            return mcid;
        }

        /// <summary>Stores the page's entry in the parent tree.</summary>
        /// <param name="transaction">The transaction.</param>
        /// <param name="tree">The parent tree.</param>
        /// <param name="store">The document's objects.</param>
        internal void Store(PdfEditTransaction transaction, PdfParentTree tree, PdfObjectStore store)
        {
            var owners = new PdfArray(store, _first + _added.Count);
            for (var i = 0; i < _first; i++)
            {
                owners.Add(_existing is not null && i < _existing.Count ? _existing.GetRaw(i) : default);
            }

            foreach (var owner in _added)
            {
                owners.Add(owner);
            }

            if (_stored.IsReference)
            {
                transaction.Replace(_stored.AsReference(), PdfValue.FromArray(owners));
                tree.Set(Key, _stored);
                return;
            }

            tree.Set(Key, PdfValue.FromArray(owners));
        }
    }

    /// <summary>A structure element being changed.</summary>
    /// <param name="Id">The element's object.</param>
    /// <param name="Dictionary">The copy being changed.</param>
    [DebuggerDisplay("ElementEdit: {Id}")]
    private sealed record ElementEdit(PdfObjectId Id, PdfDictionary Dictionary);
}

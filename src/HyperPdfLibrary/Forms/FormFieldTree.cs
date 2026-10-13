// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Collects the changes an edit makes to the field hierarchy: fields added to a parent or to the form, fields taken
/// from one, and the group fields made for names with periods or emptied by a removal (ISO 32000-2 12.7.3.2). Each
/// parent's <c>/Kids</c> and the form's <c>/Fields</c> are written once, so the cost grows with the number of changes.
/// </summary>
[DebuggerDisplay("FormFieldTree: {_groups.Count} new groups")]
internal sealed class FormFieldTree
{
    /// <summary>The transaction.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The fields the form had, and those reserved by definitions.</summary>
    private readonly FormFieldIndex _index;

    /// <summary>The group fields made for names with periods, by fully qualified name.</summary>
    private readonly Dictionary<string, NewGroup> _groups = [with(StringComparer.Ordinal)];

    /// <summary>The kids added to each parent, by the parent's object number.</summary>
    private readonly Dictionary<int, List<PdfValue>> _added = [];

    /// <summary>The kids taken from each parent, by the parent's object number.</summary>
    private readonly Dictionary<int, HashSet<int>> _removed = [];

    /// <summary>The root fields taken from the form's <c>/Fields</c> array, by object number.</summary>
    private readonly HashSet<int> _removedRoots = [];

    /// <summary>The group fields emptied by removals, by object number.</summary>
    private readonly HashSet<int> _orphans = [];

    /// <summary>Initializes a new instance of the <see cref="FormFieldTree"/> class.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="index">The form's fields.</param>
    internal FormFieldTree(PdfEditTransaction transaction, PdfObjectStore store, FormFieldIndex index)
    {
        _transaction = transaction;
        _store = store;
        _index = index;
    }

    /// <summary>Gets the fields to add to the form's <c>/Fields</c> array.</summary>
    internal List<PdfValue> Roots { get; } = [];

    /// <summary>Gets the group fields emptied by removals, which the caller deletes after <see cref="Write"/>.</summary>
    internal HashSet<int> Orphans => _orphans;

    /// <summary>Finds or makes the group field for a name, and the groups above it.</summary>
    /// <param name="name">The group's fully qualified name; empty for the form itself.</param>
    /// <returns>The group's object; not valid for the form itself.</returns>
    /// <exception cref="InvalidOperationException">The group exists but is not an object of its own.</exception>
    internal PdfObjectId EnsureGroup(string name)
    {
        if (name.Length == 0)
        {
            return default;
        }

        if (_groups.TryGetValue(name, out var made))
        {
            return made.Id;
        }

        if (_index.Find(name) is { } existing)
        {
            return existing.Id.IsValid
                ? existing.Id
                : throw new InvalidOperationException($"The field '{name}' is written inside its parent, not as an object of its own, so fields cannot be added under it.");
        }

        var parent = EnsureGroup(FormFieldNames.GetParent(name));
        var id = _transaction.Add(default);
        _groups[name] = new(id, FormFieldNames.GetLeaf(name), parent);
        AddKid(parent, id);
        return id;
    }

    /// <summary>Records a field as a kid of a parent, or as a root field.</summary>
    /// <param name="parent">The parent's object; not valid for a root field.</param>
    /// <param name="id">The field's object.</param>
    internal void AddKid(PdfObjectId parent, PdfObjectId id)
    {
        if (!parent.IsValid)
        {
            Roots.Add(PdfValue.FromReference(id));
            return;
        }

        ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(_added, parent.Number, out _);
        list ??= [];
        list.Add(PdfValue.FromReference(id));
    }

    /// <summary>Takes a field from its parent, or from the form when it is a root field. A group left with no kids goes too.</summary>
    /// <param name="node">The field.</param>
    internal void Detach(FormFieldNode node)
    {
        if (node.Parent is not { } parent)
        {
            _ = _removedRoots.Add(node.Id.Number);
            return;
        }

        ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(_removed, parent.Id.Number, out _);
        set ??= [];
        _ = set.Add(node.Id.Number);
    }

    /// <summary>Works out which groups the detached fields leave empty; call it once all the changes are recorded.</summary>
    internal void ResolveOrphans()
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (number, gone) in _removed)
            {
                if (_orphans.Contains(number) || !_index.ByNumber.TryGetValue(number, out var parent) || !IsEmptied(parent, gone))
                {
                    continue;
                }

                _ = _orphans.Add(number);
                Detach(parent);
                changed = true;
                break;
            }
        }
    }

    /// <summary>Writes the group fields made and the kids of the parents that existed.</summary>
    internal void Write()
    {
        foreach (var group in _groups.Values)
        {
            var dictionary = new PdfDictionary(_store);
            dictionary.Set(KnownName.T, FormFieldBuilder.Text(_store, group.Partial));
            if (group.Parent.IsValid)
            {
                dictionary.Set(KnownName.Parent, PdfValue.FromReference(group.Parent));
            }

            dictionary.Set(KnownName.Kids, PdfValue.FromArray(new(_store, _added[group.Id.Number].ToArray())));
            _transaction.Replace(group.Id, PdfValue.FromDictionary(dictionary));
            _ = _added.Remove(group.Id.Number);
        }

        WriteExistingParents();
    }

    /// <summary>Adds the new root fields to the form's <c>/Fields</c> array and takes the removed ones out.</summary>
    /// <param name="form">The form dictionary being rewritten.</param>
    internal void WriteFields(PdfDictionary form)
    {
        if (Roots.Count == 0 && _removedRoots.Count == 0)
        {
            return;
        }

        var current = form.GetArray(KnownName.Fields);
        var fields = new PdfArray(_store, (current?.Count ?? 0) + Roots.Count);
        for (var i = 0; current is not null && i < current.Count; i++)
        {
            var item = current.GetRaw(i);
            if (!item.IsReference || !_removedRoots.Contains(item.AsReference().Number))
            {
                fields.Add(item);
            }
        }

        foreach (var root in Roots)
        {
            fields.Add(root);
        }

        FormAcroForm.SetFields(_transaction, form, fields);
    }

    /// <summary>Determines whether a kid is one of the kids taken from a parent.</summary>
    /// <param name="item">The kid as stored.</param>
    /// <param name="gone">The object numbers taken; <see langword="null"/> for none.</param>
    /// <returns><see langword="true"/> when the kid was taken.</returns>
    private static bool IsTaken(PdfValue item, HashSet<int>? gone) => gone is not null && item.IsReference && gone.Contains(item.AsReference().Number);

    /// <summary>Determines whether a group has no kid left once some are taken and others added.</summary>
    /// <param name="group">The group.</param>
    /// <param name="gone">The object numbers of the kids taken.</param>
    /// <returns><see langword="true"/> when nothing is left in it.</returns>
    private bool IsEmptied(FormFieldNode group, HashSet<int> gone)
    {
        if (group.IsTerminal || _added.ContainsKey(group.Id.Number))
        {
            return false;
        }

        foreach (var child in group.Children)
        {
            if (!gone.Contains(child.Id.Number))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Rewrites the <c>/Kids</c> of the parents that existed: kids added and kids taken.</summary>
    private void WriteExistingParents()
    {
        var numbers = new HashSet<int>(_added.Keys);
        numbers.UnionWith(_removed.Keys);
        foreach (var number in numbers)
        {
            if (_orphans.Contains(number))
            {
                continue;
            }

            var id = new PdfObjectId(number, 0);
            var parent = StoreReading.GetDictionary(_store, id)!.Clone();
            FormAcroForm.SetArray(_transaction, parent, KnownName.Kids, RebuildKids(parent.GetArray(KnownName.Kids), number));
            _transaction.Replace(id, PdfValue.FromDictionary(parent));
        }
    }

    /// <summary>Copies a parent's kids without the ones taken and with the ones added.</summary>
    /// <param name="current">The parent's kids as they are.</param>
    /// <param name="number">The parent's object number.</param>
    /// <returns>The new kids.</returns>
    private PdfArray RebuildKids(PdfArray? current, int number)
    {
        var added = _added.GetValueOrDefault(number) ?? [];
        var gone = _removed.GetValueOrDefault(number);
        var kids = new PdfArray(_store, (current?.Count ?? 0) + added.Count);
        for (var i = 0; current is not null && i < current.Count; i++)
        {
            var item = current.GetRaw(i);
            if (!IsTaken(item, gone))
            {
                kids.Add(item);
            }
        }

        foreach (var kid in added)
        {
            kids.Add(kid);
        }

        return kids;
    }

    /// <summary>A group field made for a name with periods.</summary>
    /// <param name="Id">The group's object.</param>
    /// <param name="Partial">The group's partial name.</param>
    /// <param name="Parent">The group above it; not valid for a root group.</param>
    [DebuggerDisplay("NewGroup: {Partial}")]
    private readonly record struct NewGroup(PdfObjectId Id, string Partial, PdfObjectId Parent);
}

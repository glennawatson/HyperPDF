// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Removes fields inside one transaction: each field and the fields below it, their widgets from the pages, their
/// entries in the form's field and calculation order arrays, their elements in the structure tree and the groups they
/// leave empty. Actions that name a removed field are updated or reported, so nothing is left pointing at a deleted object.
/// </summary>
[DebuggerDisplay("FormFieldRemoval: {_roots.Count} fields")]
internal sealed class FormFieldRemoval
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The transaction.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The form's fields.</summary>
    private readonly FormFieldIndex _index;

    /// <summary>The fields asked for that exist, with the fields above them left out.</summary>
    private readonly List<FormFieldNode> _roots = [];

    /// <summary>The names asked for that were not removed, with reasons.</summary>
    private readonly List<string> _skipped = [];

    /// <summary>Initializes a new instance of the <see cref="FormFieldRemoval"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="index">The form's fields.</param>
    internal FormFieldRemoval(PdfDocument document, PdfEditTransaction transaction, FormFieldIndex index)
    {
        _document = document;
        _transaction = transaction;
        _index = index;
    }

    /// <summary>Picks the fields to remove from the names asked for.</summary>
    /// <param name="names">The fully qualified names.</param>
    internal void Select(IReadOnlyList<string> names)
    {
        var chosen = new HashSet<int>();
        foreach (var name in names)
        {
            if (_index.Find(name) is not { } node)
            {
                _skipped.Add($"{name}: no field has this name.");
            }
            else if (!node.Id.IsValid)
            {
                _skipped.Add($"{name}: the field is written inside its parent, not as an object of its own.");
            }
            else if (chosen.Add(node.Id.Number))
            {
                _roots.Add(node);
            }
        }

        var top = new List<FormFieldNode>(_roots.Count);
        foreach (var root in _roots)
        {
            if (!HasChosenAncestor(root, chosen))
            {
                top.Add(root);
            }
        }

        _roots.Clear();
        _roots.AddRange(top);
    }

    /// <summary>Removes the selected fields.</summary>
    /// <param name="cancellationToken">Cancels the removal.</param>
    /// <returns>What was done.</returns>
    internal PdfFieldRemoval Remove(CancellationToken cancellationToken)
    {
        if (_roots.Count == 0)
        {
            return new([], [.. _skipped], 0, []);
        }

        var subtree = new List<FormFieldNode>();
        foreach (var root in _roots)
        {
            FormFieldIndex.CollectSubtree(root, subtree);
        }

        var change = new FormReferenceChange();
        var widgets = CollectObjects(subtree, change);
        foreach (var root in _roots)
        {
            change.AddRemoved(root.Name);
        }

        var references = FormFieldReferences.Apply(_document, _transaction, _index, change, cancellationToken);
        var structure = FormStructureRemoval.Create(_document, _transaction);
        foreach (var node in subtree)
        {
            TagRemoval(structure, node);
        }

        var tree = new FormFieldTree(_transaction, _document.Objects, _index);
        foreach (var root in _roots)
        {
            tree.Detach(root);
        }

        tree.ResolveOrphans();
        tree.Write();
        WriteForm(tree, change.RemovedObjects);
        FormPageAnnotations.Remove(_document, _transaction, widgets, cancellationToken);
        structure?.Commit();
        DeleteAll(change.RemovedObjects, tree.Orphans);
        return new([.. _roots.ConvertAll(static node => node.Name)], [.. _skipped], widgets.Count, [.. references.Unresolved]);
    }

    /// <summary>Determines whether a field has an ancestor that is also being removed.</summary>
    /// <param name="node">The field.</param>
    /// <param name="chosen">The object numbers of the fields chosen.</param>
    /// <returns><see langword="true"/> when an ancestor is chosen.</returns>
    private static bool HasChosenAncestor(FormFieldNode node, HashSet<int> chosen)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (chosen.Contains(parent.Id.Number))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Notes the objects of the fields to remove, and the widgets among them.</summary>
    /// <param name="subtree">The fields to remove and the fields below them.</param>
    /// <param name="change">Receives every object number to delete.</param>
    /// <returns>The object numbers of the widgets.</returns>
    private static HashSet<int> CollectObjects(List<FormFieldNode> subtree, FormReferenceChange change)
    {
        var widgets = new HashSet<int>();
        foreach (var node in subtree)
        {
            _ = change.RemovedObjects.Add(node.Id.Number);
            foreach (var widget in node.Widgets)
            {
                if (!widget.Id.IsValid)
                {
                    continue;
                }

                _ = widgets.Add(widget.Id.Number);
                _ = change.RemovedObjects.Add(widget.Id.Number);
            }
        }

        return widgets;
    }

    /// <summary>Tells the structure remover about the widgets of a field.</summary>
    /// <param name="structure">The remover; <see langword="null"/> in an untagged document.</param>
    /// <param name="node">The field.</param>
    private static void TagRemoval(FormStructureRemoval? structure, FormFieldNode node)
    {
        foreach (var widget in node.Widgets)
        {
            if (widget.Id.IsValid)
            {
                structure?.Add(widget.Id, widget.Dictionary);
            }
        }
    }

    /// <summary>Writes the form's <c>/Fields</c> and calculation order without the removed fields.</summary>
    /// <param name="tree">The changes to the hierarchy.</param>
    /// <param name="removed">The object numbers of the removed fields.</param>
    private void WriteForm(FormFieldTree tree, HashSet<int> removed)
    {
        var form = FormAcroForm.Ensure(_transaction, _document.Objects).Clone();
        tree.WriteFields(form);
        if (form.GetArray(KnownName.CO) is { } order && FilterOrder(order, removed, tree.Orphans) is { } kept)
        {
            FormAcroForm.SetArray(_transaction, form, KnownName.CO, kept);
        }

        FormAcroForm.Store(_transaction, _document.Objects, form);
    }

    /// <summary>Copies the calculation order without the removed fields.</summary>
    /// <param name="order">The calculation order.</param>
    /// <param name="removed">The object numbers of the removed fields.</param>
    /// <param name="orphans">The object numbers of the groups removed with them.</param>
    /// <returns>The new order; <see langword="null"/> when no removed field was in it.</returns>
    private PdfArray? FilterOrder(PdfArray order, HashSet<int> removed, HashSet<int> orphans)
    {
        PdfArray? kept = null;
        for (var i = 0; i < order.Count; i++)
        {
            var item = order.GetRaw(i);
            if (item.IsReference && (removed.Contains(item.AsReference().Number) || orphans.Contains(item.AsReference().Number)))
            {
                kept ??= CopyPrefix(order, i);
                continue;
            }

            kept?.Add(item);
        }

        return kept;
    }

    /// <summary>Copies the first items of an array into a new one.</summary>
    /// <param name="source">The array.</param>
    /// <param name="count">The number of items to copy.</param>
    /// <returns>The new array.</returns>
    private PdfArray CopyPrefix(PdfArray source, int count)
    {
        var copy = new PdfArray(_document.Objects, source.Count);
        for (var i = 0; i < count; i++)
        {
            copy.Add(source.GetRaw(i));
        }

        return copy;
    }

    /// <summary>Deletes the objects of the removed fields, their widgets and the groups left empty.</summary>
    /// <param name="removed">The object numbers of the fields and widgets.</param>
    /// <param name="orphans">The object numbers of the groups.</param>
    private void DeleteAll(HashSet<int> removed, HashSet<int> orphans)
    {
        foreach (var number in removed)
        {
            _transaction.Delete(new(number, 0));
        }

        foreach (var number in orphans)
        {
            _transaction.Delete(new(number, 0));
        }
    }
}

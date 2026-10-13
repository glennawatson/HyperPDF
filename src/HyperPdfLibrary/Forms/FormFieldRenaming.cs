// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Renames a field (ISO 32000-2 12.7.3.2). A new name with periods moves the field under the group fields of the shorter
/// names, making them when needed, and carries the attributes it inherited from its old parents so it keeps its meaning.
/// Actions that name the field are updated or reported. A clash, or a field that holds a value in the new path, refuses the
/// rename with a plain reason and changes nothing.
/// </summary>
[DebuggerDisplay("FormFieldRenaming: {_node.Name}")]
internal sealed class FormFieldRenaming
{
    /// <summary>The partial name used to keep the field's own name when it is moved.</summary>
    private const string SameName = "The field already has this name.";

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The form's fields.</summary>
    private readonly FormFieldIndex _index;

    /// <summary>The field being renamed.</summary>
    private readonly FormFieldNode _node;

    /// <summary>The new fully qualified name.</summary>
    private readonly string _newName;

    /// <summary>Initializes a new instance of the <see cref="FormFieldRenaming"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The form's fields.</param>
    /// <param name="node">The field to rename.</param>
    /// <param name="newName">The new fully qualified name.</param>
    private FormFieldRenaming(PdfDocument document, FormFieldIndex index, FormFieldNode node, string newName)
    {
        _document = document;
        _index = index;
        _node = node;
        _newName = newName;
    }

    /// <summary>Gets the inheritable field attributes (ISO 32000-2 Table 226 and 12.7.3.3) a field carries on to the new parent.</summary>
    private static ReadOnlySpan<KnownName> Inheritable =>
    [
        KnownName.FT, KnownName.Ff, KnownName.V, KnownName.DV, KnownName.DA, KnownName.Q, KnownName.DS,
        KnownName.MaxLen, KnownName.Opt, KnownName.TI, KnownName.I, KnownName.TU, KnownName.TM, KnownName.AA,
    ];

    /// <summary>Plans a rename.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The form's fields.</param>
    /// <param name="name">The field's fully qualified name.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <param name="refusal">A plain reason the rename is refused; <see langword="null"/> when it can be done.</param>
    /// <returns>The plan; <see langword="null"/> when the rename is refused.</returns>
    internal static FormFieldRenaming? Plan(PdfDocument document, FormFieldIndex index, string name, string newName, out string? refusal)
    {
        refusal = FormFieldNames.Validate(newName);
        if (refusal is not null)
        {
            return null;
        }

        if (index.Find(name) is not { } node)
        {
            refusal = $"No field is named '{name}'.";
            return null;
        }

        refusal = CheckTarget(index, node, newName);
        return refusal is null ? new(document, index, node, newName) : null;
    }

    /// <summary>Renames the field.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">Cancels the rename.</param>
    /// <returns>What was done.</returns>
    internal PdfFieldRename Apply(PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        var change = new FormReferenceChange { OldName = _node.Name, NewName = _newName };
        var references = FormFieldReferences.Apply(_document, transaction, _index, change, cancellationToken);
        var copy = StoreReading.GetDictionary(_document.Objects, _node.Id)!.Clone();
        copy.Set(KnownName.T, FormFieldBuilder.Text(_document.Objects, FormFieldNames.GetLeaf(_newName)));
        FollowTooltips(transaction, copy);
        var oldParent = _node.Parent?.Name ?? string.Empty;
        if (string.Equals(oldParent, FormFieldNames.GetParent(_newName), StringComparison.Ordinal))
        {
            transaction.Replace(_node.Id, PdfValue.FromDictionary(copy));
        }
        else
        {
            Move(transaction, copy);
        }

        return new(true, _node.Name, _newName, null, references.Updated, [.. references.Unresolved]);
    }

    /// <summary>Replaces a tooltip that holds a field's old name.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="field">The field copy, changed in place.</param>
    /// <param name="oldName">The field's old fully qualified name.</param>
    /// <param name="newName">The field's new fully qualified name.</param>
    /// <returns><see langword="true"/> when the tooltip was replaced.</returns>
    private static bool FollowTooltip(PdfObjectStore store, PdfDictionary field, string oldName, string newName)
    {
        if (!string.Equals(field.GetText(KnownName.TU), oldName, StringComparison.Ordinal))
        {
            return false;
        }

        field.Set(KnownName.TU, FormFieldBuilder.Text(store, newName));
        return true;
    }

    /// <summary>Checks that a field can take a new name.</summary>
    /// <param name="index">The form's fields.</param>
    /// <param name="node">The field.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <returns>A plain reason it cannot; <see langword="null"/> when it can.</returns>
    private static string? CheckTarget(FormFieldIndex index, FormFieldNode node, string newName)
    {
        if (!node.Id.IsValid)
        {
            return "The field is written inside its parent, not as an object of its own, so it cannot be renamed.";
        }

        if (string.Equals(node.Name, newName, StringComparison.Ordinal))
        {
            return SameName;
        }

        if (index.ByName.ContainsKey(newName))
        {
            return $"A field named '{newName}' already exists. Field names must be unique in a form.";
        }

        if (FormFieldNames.IsBelow(newName, node.Name))
        {
            return "A field cannot be moved inside itself.";
        }

        var parent = FormFieldNames.GetParent(newName);
        while (parent.Length > 0)
        {
            if (index.Find(parent) is { IsTerminal: true })
            {
                return $"'{parent}' is a field that holds a value, so '{newName}' cannot be placed under it.";
            }

            parent = FormFieldNames.GetParent(parent);
        }

        return null;
    }

    /// <summary>Finds an attribute as a field or its parents store it.</summary>
    /// <param name="start">The first dictionary to look in.</param>
    /// <param name="key">The attribute's key.</param>
    /// <returns>The value as stored, a reference or not; null when none has it.</returns>
    private static PdfValue FindRaw(PdfDictionary? start, KnownName key)
    {
        var node = start;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxNesting; depth++)
        {
            if (node.ContainsKey(key))
            {
                return node.GetRaw(key);
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return default;
    }

    /// <summary>
    /// Gives the new name to the tooltips (<c>/TU</c>) that still hold the old one: the library writes the field name as the
    /// tooltip when none is given, and a screen reader reads it as the field's label.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="copy">The copy of the renamed field, changed in place.</param>
    private void FollowTooltips(PdfEditTransaction transaction, PdfDictionary copy)
    {
        var store = _document.Objects;
        _ = FollowTooltip(store, copy, _node.Name, _newName);
        var subtree = new List<FormFieldNode>();
        FormFieldIndex.CollectSubtree(_node, subtree);
        for (var i = 1; i < subtree.Count; i++)
        {
            var below = subtree[i];
            var renamed = string.Concat(_newName, below.Name.AsSpan(_node.Name.Length));
            var changed = below.Dictionary.Clone();
            if (below.Id.IsValid && FollowTooltip(store, changed, below.Name, renamed))
            {
                transaction.Replace(below.Id, PdfValue.FromDictionary(changed));
            }
        }
    }

    /// <summary>Moves the field under the groups of its new name, and carries the attributes it inherited.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="copy">The copy of the field, with its new partial name.</param>
    private void Move(PdfEditTransaction transaction, PdfDictionary copy)
    {
        var store = _document.Objects;
        var tree = new FormFieldTree(transaction, store, _index);
        var parentId = tree.EnsureGroup(FormFieldNames.GetParent(_newName));
        CarryInheritedAttributes(copy);
        if (parentId.IsValid)
        {
            copy.Set(KnownName.Parent, PdfValue.FromReference(parentId));
        }
        else
        {
            _ = copy.Remove(KnownName.Parent);
        }

        transaction.Replace(_node.Id, PdfValue.FromDictionary(copy));
        tree.Detach(_node);
        tree.AddKid(parentId, _node.Id);
        tree.ResolveOrphans();
        tree.Write();
        var form = FormAcroForm.Ensure(transaction, store).Clone();
        tree.WriteFields(form);
        FormAcroForm.Store(transaction, store, form);
        foreach (var orphan in tree.Orphans)
        {
            transaction.Delete(new(orphan, 0));
        }
    }

    /// <summary>Copies the attributes the field inherited from its old parents onto the field, so a new parent does not change them.</summary>
    /// <param name="copy">The copy of the field, changed in place.</param>
    private void CarryInheritedAttributes(PdfDictionary copy)
    {
        if (_node.Parent is null)
        {
            return;
        }

        var oldParent = StoreReading.GetDictionary(_document.Objects, _node.Parent.Id);
        foreach (var key in Inheritable)
        {
            if (!copy.ContainsKey(key) && FindRaw(oldParent, key) is { IsNull: false } value)
            {
                copy.Set(key, value);
            }
        }
    }
}

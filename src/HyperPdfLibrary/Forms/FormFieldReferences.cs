// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Follows a rename or a removal into the actions that refer to fields by name: the field lists of submit-form,
/// reset-form and import-data actions, and the targets of hide actions (ISO 32000-2 12.7.6 and 12.6.4.10). Entries are
/// updated in place. Scripts are not rewritten; a script that mentions the field is reported, as is an action left with
/// no field.
/// </summary>
[DebuggerDisplay("FormFieldReferences: {Updated} updated, {Unresolved.Count} unresolved")]
internal sealed class FormFieldReferences
{
    /// <summary>The deepest chain of <c>/Next</c> actions followed.</summary>
    private const int MaxChain = 32;

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The transaction.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The change to follow.</summary>
    private readonly FormReferenceChange _change;

    /// <summary>The objects already visited.</summary>
    private readonly HashSet<int> _visited = [];

    /// <summary>The place being read, for reports.</summary>
    private string _location = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="FormFieldReferences"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="change">The change to follow.</param>
    private FormFieldReferences(PdfDocument document, PdfEditTransaction transaction, FormReferenceChange change)
    {
        _document = document;
        _transaction = transaction;
        _change = change;
    }

    /// <summary>Gets the number of entries updated.</summary>
    internal int Updated { get; private set; }

    /// <summary>Gets the references that could not be updated.</summary>
    internal List<PdfFormReference> Unresolved { get; } = [];

    /// <summary>Updates the actions of the fields, the page annotations and the document that refer to changed names.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="index">The form's fields.</param>
    /// <param name="change">The change to follow.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>What was updated and what was not.</returns>
    internal static FormFieldReferences Apply(PdfDocument document, PdfEditTransaction transaction, FormFieldIndex index, FormReferenceChange change, CancellationToken cancellationToken)
    {
        var references = new FormFieldReferences(document, transaction, change);
        foreach (var node in index.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            references.VisitField(node);
        }

        references.VisitPages(cancellationToken);
        references.VisitCatalog();
        return references;
    }

    /// <summary>Visits a field and its widgets.</summary>
    /// <param name="node">The field.</param>
    private void VisitField(FormFieldNode node)
    {
        VisitHolder(node.Id, $"field {node.Name}");
        foreach (var widget in node.Widgets)
        {
            if (widget.Id != node.Id)
            {
                VisitHolder(widget.Id, $"a widget of field {node.Name}");
            }
        }
    }

    /// <summary>Visits the annotations of every page.</summary>
    /// <param name="cancellationToken">Cancels the walk.</param>
    private void VisitPages(CancellationToken cancellationToken)
    {
        for (var page = 0; page < _document.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var annotations = PdfDocumentPages.GetPage(_document, page).Dictionary.GetArray(KnownName.Annots);
            for (var i = 0; annotations is not null && i < annotations.Count; i++)
            {
                VisitHolder(annotations.GetRaw(i).AsReference(), $"page {page + 1}, annotation {i}");
            }
        }
    }

    /// <summary>Visits the document's open action and additional actions.</summary>
    private void VisitCatalog()
    {
        var catalog = _document.Catalog;
        _location = "the document";
        var updated = RewriteHolder(catalog, KnownName.OpenAction);
        updated = RewriteHolder(updated ?? catalog, KnownName.AA) ?? updated;
        if (updated is not null)
        {
            PdfPageTreeWriter.ReplaceCatalog(_transaction, _document.Objects, updated);
        }
    }

    /// <summary>Rewrites the actions of one object that holds actions.</summary>
    /// <param name="id">The object; not valid for one written directly in an array.</param>
    /// <param name="location">Where the object is, for reports.</param>
    private void VisitHolder(PdfObjectId id, string location)
    {
        if (!id.IsValid || _change.RemovedObjects.Contains(id.Number) || !_visited.Add(id.Number) || StoreReading.GetDictionary(_document.Objects, id) is not { } holder)
        {
            return;
        }

        _location = location;
        var updated = RewriteHolder(holder, KnownName.A);
        updated = RewriteHolder(updated ?? holder, KnownName.AA) ?? updated;
        if (updated is not null)
        {
            _transaction.Replace(id, PdfValue.FromDictionary(updated));
        }
    }

    /// <summary>Rewrites one action entry of a dictionary.</summary>
    /// <param name="holder">The dictionary.</param>
    /// <param name="key">The entry: <c>/A</c>, <c>/OpenAction</c> or <c>/AA</c>.</param>
    /// <returns>A copy of the dictionary with the entry changed; <see langword="null"/> when nothing changed.</returns>
    private PdfDictionary? RewriteHolder(PdfDictionary holder, PdfName key)
    {
        var raw = holder.GetRaw(key);
        var changed = key.Is(KnownName.AA) ? RewriteTriggers(raw) : RewriteAction(raw, 0);
        if (changed.Equals(raw))
        {
            return null;
        }

        var copy = holder.Clone();
        copy.Set(key, changed);
        return copy;
    }

    /// <summary>Rewrites the actions in an additional-actions dictionary.</summary>
    /// <param name="raw">The dictionary as stored: a reference or a direct dictionary.</param>
    /// <returns>The value to store; the same value when nothing changed.</returns>
    private PdfValue RewriteTriggers(PdfValue raw)
    {
        if (StoreReading.Resolve(_document.Objects, raw).AsDictionary() is not { } triggers)
        {
            return raw;
        }

        PdfDictionary? copy = null;
        for (var i = 0; i < triggers.Count; i++)
        {
            var value = triggers.GetValueAt(i);
            var changed = RewriteAction(value, 0);
            if (changed.Equals(value))
            {
                continue;
            }

            copy ??= triggers.Clone();
            copy.Set(triggers.GetKeyAt(i), changed);
        }

        if (copy is null)
        {
            return raw;
        }

        if (!raw.IsReference)
        {
            return PdfValue.FromDictionary(copy);
        }

        _transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(copy));
        return raw;
    }

    /// <summary>Rewrites an action and the actions that follow it.</summary>
    /// <param name="raw">The action as stored: a reference or a direct dictionary.</param>
    /// <param name="depth">How many actions came before in the chain.</param>
    /// <returns>The value to store; the same value when nothing changed.</returns>
    private PdfValue RewriteAction(PdfValue raw, int depth)
    {
        if (depth >= MaxChain || StoreReading.Resolve(_document.Objects, raw).AsDictionary() is not { } action)
        {
            return raw;
        }

        var copy = RewriteFields(action);
        var next = action.GetRaw(KnownName.Next);
        var changedNext = RewriteChain(next, depth);
        if (!changedNext.Equals(next))
        {
            copy ??= action.Clone();
            copy.Set(KnownName.Next, changedNext);
        }

        if (copy is null)
        {
            return raw;
        }

        if (!raw.IsReference)
        {
            return PdfValue.FromDictionary(copy);
        }

        _transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(copy));
        return raw;
    }

    /// <summary>Rewrites the actions that follow an action: one action or an array of them.</summary>
    /// <param name="next">The <c>/Next</c> entry as stored.</param>
    /// <param name="depth">How many actions came before.</param>
    /// <returns>The value to store; the same value when nothing changed.</returns>
    private PdfValue RewriteChain(PdfValue next, int depth)
    {
        if (next.IsNull)
        {
            return next;
        }

        if (StoreReading.Resolve(_document.Objects, next).AsArray() is not { } chain)
        {
            return RewriteAction(next, depth + 1);
        }

        PdfArray? copy = null;
        for (var i = 0; i < chain.Count; i++)
        {
            var item = chain.GetRaw(i);
            var changed = RewriteAction(item, depth + 1);
            if (changed.Equals(item))
            {
                continue;
            }

            copy ??= chain.Clone();
            copy.SetAt(i, changed);
        }

        return copy is null ? next : PdfValue.FromArray(copy);
    }

    /// <summary>Rewrites the field names of one action.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>A copy with the names changed; <see langword="null"/> when nothing changed.</returns>
    private PdfDictionary? RewriteFields(PdfDictionary action)
    {
        var type = _document.Objects.Names.GetString(action.GetName(KnownName.S));
        switch (type)
        {
            case "SubmitForm" or "ResetForm" or "ImportData":
            {
                return RewriteNameList(action, KnownName.Fields, "lists no field after the change");
            }

            case "Hide":
            {
                return RewriteNameList(action, KnownName.T, "hides no field after the change");
            }

            case "JavaScript":
            {
                CheckScript(action);
                return null;
            }

            default:
            {
                return null;
            }
        }
    }

    /// <summary>Rewrites the names in a field list entry.</summary>
    /// <param name="action">The action.</param>
    /// <param name="key">The entry: <c>/Fields</c> or <c>/T</c>.</param>
    /// <param name="emptyMessage">What to report when the entry is left with no field.</param>
    /// <returns>A copy of the action; <see langword="null"/> when nothing changed.</returns>
    private PdfDictionary? RewriteNameList(PdfDictionary action, PdfName key, string emptyMessage)
    {
        var raw = action.GetRaw(key);
        var resolved = StoreReading.Resolve(_document.Objects, raw);
        var list = resolved.AsArray();
        var items = list?.Items.ToArray() ?? (raw.IsNull ? [] : [raw]);
        var kept = new List<PdfValue>(items.Length);
        var changed = false;
        foreach (var item in items)
        {
            var next = RewriteName(item);
            changed |= !next.Equals(item);
            if (!next.IsNull)
            {
                kept.Add(next);
            }
        }

        if (!changed)
        {
            return null;
        }

        if (kept.Count == 0)
        {
            Unresolved.Add(new(_location, $"An action {emptyMessage}; it was left as it was."));
            return null;
        }

        var copy = action.Clone();
        copy.Set(key, list is null && kept.Count == 1 ? kept[0] : PdfValue.FromArray(new(_document.Objects, kept.ToArray())));
        return copy;
    }

    /// <summary>Follows the change into one entry of a field list: a name or a reference to a field.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The new entry; the same one when it is not affected; null when the field it names is gone.</returns>
    private PdfValue RewriteName(PdfValue item)
    {
        var bytes = item.AsStringBytes();
        if (item.IsReference || bytes.IsEmpty)
        {
            return DropIfRemoved(item);
        }

        var name = PdfText.Decode(bytes);
        if (_change.IsRemoved(name))
        {
            Updated++;
            return default;
        }

        if (_change.MapName(name) is not { } mapped)
        {
            return item;
        }

        Updated++;
        return FormFieldBuilder.Text(_document.Objects, mapped);
    }

    /// <summary>Drops an entry that is a reference to a removed field.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>The entry; null when it refers to a removed field.</returns>
    private PdfValue DropIfRemoved(PdfValue item)
    {
        if (!item.IsReference || !_change.RemovedObjects.Contains(item.AsReference().Number))
        {
            return item;
        }

        Updated++;
        return default;
    }

    /// <summary>Reports a script that mentions a field the change affects.</summary>
    /// <param name="action">The JavaScript action.</param>
    private void CheckScript(PdfDictionary action)
    {
        var script = FieldAttributes.ReadText(action.Get(KnownName.JS));
        if (_change.FindMention(script) is { } mentioned)
        {
            Unresolved.Add(new(_location, $"A script mentions the field '{mentioned}'. The library does not edit scripts, so it was left as it was."));
        }
    }
}

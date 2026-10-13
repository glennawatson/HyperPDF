// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Takes form fields out of a document after their widgets were removed from the pages (ISO 32000-2 12.7.3): a widget
/// leaves its field's <c>/Kids</c>, a field with no widget left leaves its parent, and the <c>/AcroForm</c> goes when no
/// field is left. A form that stays loses its XFA data and its calculation order entries for removed fields.
/// </summary>
internal static class PdfFieldRemoval
{
    /// <summary>Removes fields whose widgets were removed.</summary>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="widgets">The object numbers of the removed widgets.</param>
    /// <param name="removedObjects">Receives every object that left the form, so unreachable ones can be deleted.</param>
    /// <returns>What was removed.</returns>
    internal static PdfFieldRemovalResult Remove(PdfEditTransaction transaction, PdfObjectStore store, HashSet<int> widgets, List<PdfObjectId> removedObjects)
    {
        var catalog = store.Catalog;
        if (widgets.Count == 0 || catalog.GetDictionary(KnownName.AcroForm) is not { } form)
        {
            return default;
        }

        var run = new Run(transaction, store, widgets, removedObjects);
        var kept = form.GetArray(KnownName.Fields) is { } fields ? run.Prune(fields, 0) : new PdfArray(store);
        var formValue = catalog.GetRaw(KnownName.AcroForm);
        var hadXfa = form.ContainsKey(KnownName.XFA);
        if (kept.Count == 0)
        {
            DropForm(transaction, store, formValue, removedObjects);
            return new(run.FieldsRemoved, true, hadXfa);
        }

        var changed = form.Clone();
        changed.Set(KnownName.Fields, PdfValue.FromArray(kept));
        _ = changed.Remove(KnownName.XFA);
        if (form.GetArray(KnownName.CO) is { } order)
        {
            changed.Set(KnownName.CO, PdfValue.FromArray(run.KeepFields(order)));
        }

        if (formValue.IsReference)
        {
            transaction.Replace(formValue.AsReference(), PdfValue.FromDictionary(changed));
        }
        else
        {
            var edited = catalog.Clone();
            edited.Set(KnownName.AcroForm, PdfValue.FromDictionary(changed));
            PdfPageTreeWriter.ReplaceCatalog(transaction, store, edited);
        }

        return new(run.FieldsRemoved, false, hadXfa);
    }

    /// <summary>Removes the /AcroForm entry from the catalog.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="formValue">The /AcroForm value as the catalog holds it.</param>
    /// <param name="removedObjects">Receives the form's object.</param>
    private static void DropForm(PdfEditTransaction transaction, PdfObjectStore store, PdfValue formValue, List<PdfObjectId> removedObjects)
    {
        if (formValue.IsReference)
        {
            removedObjects.Add(formValue.AsReference());
        }

        var edited = store.Catalog.Clone();
        _ = edited.Remove(KnownName.AcroForm);
        PdfPageTreeWriter.ReplaceCatalog(transaction, store, edited);
    }

    /// <summary>One removal: the widgets to take out and the fields taken out so far.</summary>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="widgets">The object numbers of the widgets to take out.</param>
    /// <param name="removedObjects">Receives every object that leaves the form.</param>
    private sealed class Run(PdfEditTransaction transaction, PdfObjectStore store, HashSet<int> widgets, List<PdfObjectId> removedObjects)
    {
        /// <summary>The object numbers of the fields removed.</summary>
        private readonly HashSet<int> _removedFields = [];

        /// <summary>Gets the number of fields removed.</summary>
        internal int FieldsRemoved { get; private set; }

        /// <summary>Lists the entries of a field array that are still in the form.</summary>
        /// <param name="items">A /Fields or /Kids array.</param>
        /// <param name="depth">The nesting depth.</param>
        /// <returns>The entries that stay, as written; kids changed by the removal are written again.</returns>
        internal PdfArray Prune(PdfArray items, int depth)
        {
            var kept = new PdfArray(store, items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var raw = items.GetRaw(i);
                if (items.GetDictionary(i) is not { } node || depth > PdfLimits.MaxNesting)
                {
                    kept.Add(raw);
                    continue;
                }

                if (raw.IsReference && widgets.Contains(raw.AsReference().Number))
                {
                    Drop(raw, node);
                    continue;
                }

                if (node.GetArray(KnownName.Kids) is { Count: > 0 } kids)
                {
                    PruneNode(kept, raw, node, kids, depth);
                    continue;
                }

                kept.Add(raw);
            }

            return kept;
        }

        /// <summary>Removes the entries of a field array that name removed fields.</summary>
        /// <param name="items">The array, such as /CO.</param>
        /// <returns>The remaining entries.</returns>
        internal PdfArray KeepFields(PdfArray items)
        {
            var kept = new PdfArray(store, items.Count);
            foreach (var raw in items.Items)
            {
                if (!raw.IsReference || !_removedFields.Contains(raw.AsReference().Number))
                {
                    kept.Add(raw);
                }
            }

            return kept;
        }

        /// <summary>Prunes a field's kids and keeps, changes or drops the field.</summary>
        /// <param name="kept">The entries that stay, receiving this field when it stays.</param>
        /// <param name="raw">The field as the array holds it.</param>
        /// <param name="node">The field.</param>
        /// <param name="kids">The field's kids.</param>
        /// <param name="depth">The nesting depth.</param>
        private void PruneNode(PdfArray kept, PdfValue raw, PdfDictionary node, PdfArray kids, int depth)
        {
            var remaining = Prune(kids, depth + 1);
            if (remaining.Count == kids.Count)
            {
                kept.Add(raw);
                return;
            }

            if (remaining.Count == 0)
            {
                Drop(raw, node);
                return;
            }

            var changed = node.Clone();
            changed.Set(KnownName.Kids, PdfValue.FromArray(remaining));
            if (raw.IsReference)
            {
                transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(changed));
                kept.Add(raw);
            }
            else
            {
                kept.Add(PdfValue.FromDictionary(changed));
            }
        }

        /// <summary>Takes a widget or a field out of the form.</summary>
        /// <param name="raw">The entry as the array holds it.</param>
        /// <param name="node">The dictionary.</param>
        private void Drop(PdfValue raw, PdfDictionary node)
        {
            if (!raw.IsReference)
            {
                return;
            }

            var id = raw.AsReference();
            removedObjects.Add(id);
            if (!node.ContainsKey(KnownName.T) && !node.ContainsKey(KnownName.FT))
            {
                return;
            }

            _ = _removedFields.Add(id.Number);
            FieldsRemoved++;
        }
    }
}

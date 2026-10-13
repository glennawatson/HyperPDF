// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Creates fields inside one transaction. Parents, the form's <c>/Fields</c> and <c>/CO</c> arrays, the pages'
/// <c>/Annots</c> arrays and the structure tree are each written once at the end, so creating a thousand fields costs
/// time in proportion to the number of fields.
/// </summary>
[DebuggerDisplay("FormFieldCreation: {_created.Count} fields")]
internal sealed class FormFieldCreation
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The transaction.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>Cancels the creation.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The changes to the field hierarchy.</summary>
    private readonly FormFieldTree _tree;

    /// <summary>The fields to add to the calculation order.</summary>
    private readonly List<PdfValue> _calculation = [];

    /// <summary>The fields made so far.</summary>
    private readonly List<PdfCreatedField> _created = [];

    /// <summary>The widgets added to pages.</summary>
    private readonly FormPageAnnotations _pages;

    /// <summary>Tags the widgets; <see langword="null"/> in an untagged document.</summary>
    private readonly FormStructureWriter? _structure;

    /// <summary>Whether a field was made without an appearance.</summary>
    private bool _needAppearances;

    /// <summary>Initializes a new instance of the <see cref="FormFieldCreation"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="index">The form's fields, with the new names reserved.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    internal FormFieldCreation(PdfDocument document, PdfEditTransaction transaction, FormFieldIndex index, CancellationToken cancellationToken)
    {
        _document = document;
        _transaction = transaction;
        _store = document.Objects;
        _cancellationToken = cancellationToken;
        _tree = new(transaction, _store, index);
        _pages = new(document);
        _structure = FormStructureWriter.Create(document, transaction);
    }

    /// <summary>Gets the fields made so far.</summary>
    internal List<PdfCreatedField> Created => _created;

    /// <summary>Makes the form ready for fields: it exists and its default resources hold the Helvetica font the default appearance names.</summary>
    internal void Prepare()
    {
        _ = FormAcroForm.Ensure(_transaction, _store);
        if (PdfStampConformance.GetPdfAPart(_document) == 0)
        {
            FormAcroForm.EnsureHelvetica(_transaction, _store);
        }
    }

    /// <summary>Creates one field with its widgets.</summary>
    /// <param name="spec">The planned field.</param>
    internal void Create(FormFieldSpec spec)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var definition = spec.Definition;
        var parentId = _tree.EnsureGroup(FormFieldNames.GetParent(definition.Name));
        var fieldId = _transaction.Add(default);
        var field = new PdfDictionary(_store);
        FormFieldBuilder.WriteField(_store, field, spec);
        if (parentId.IsValid)
        {
            field.Set(KnownName.Parent, PdfValue.FromReference(parentId));
        }

        var locations = CreateWidgets(spec, fieldId, field);
        _transaction.Replace(fieldId, PdfValue.FromDictionary(field));
        _tree.AddKid(parentId, fieldId);
        if (definition.Actions?.Calculate is not null)
        {
            _calculation.Add(PdfValue.FromReference(fieldId));
        }

        _created.Add(new(definition.Name, fieldId, locations));
    }

    /// <summary>Writes the parents, the form's arrays, the pages and the structure tree.</summary>
    internal void Finish()
    {
        _tree.Write();
        WriteForm();
        _pages.Commit(_transaction, _structure is not null);
        _structure?.Commit();
    }

    /// <summary>Adds the new root fields and calculated fields to the form, and asks readers to draw appearances when some field has none.</summary>
    private void WriteForm()
    {
        var form = FormAcroForm.Ensure(_transaction, _store).Clone();
        _tree.WriteFields(form);
        if (_calculation.Count > 0)
        {
            var order = form.GetArray(KnownName.CO)?.Clone() ?? new PdfArray(_store, _calculation.Count);
            foreach (var calculated in _calculation)
            {
                order.Add(calculated);
            }

            FormAcroForm.SetArray(_transaction, form, KnownName.CO, order);
        }

        if (_needAppearances)
        {
            form.Set(KnownName.NeedAppearances, PdfValue.FromBoolean(true));
        }

        FormAcroForm.Store(_transaction, _store, form);
    }

    /// <summary>Writes the widgets of a field. One widget is merged into the field; several become its kids.</summary>
    /// <param name="spec">The planned field.</param>
    /// <param name="fieldId">The field's object.</param>
    /// <param name="field">The field dictionary, which receives <c>/Kids</c> when it has several widgets.</param>
    /// <returns>Where the widgets sit.</returns>
    private PdfWidgetLocation[] CreateWidgets(FormFieldSpec spec, PdfObjectId fieldId, PdfDictionary field)
    {
        var count = spec.Definition.Widgets.Length;
        var merged = count == 1;
        var kids = merged ? null : new PdfArray(_store, count);
        var locations = new PdfWidgetLocation[count];
        for (var i = 0; i < locations.Length; i++)
        {
            var id = merged ? fieldId : _transaction.Add(default);
            kids?.Add(PdfValue.FromReference(id));
            var widget = merged ? field : new PdfDictionary(_store);
            locations[i] = CreateWidget(spec, new(i, id, fieldId, field, widget, merged));
        }

        if (kids is not null)
        {
            field.Set(KnownName.Kids, PdfValue.FromArray(kids));
        }

        return locations;
    }

    /// <summary>Writes one widget: its dictionary, appearance, tag and place on its page.</summary>
    /// <param name="spec">The planned field.</param>
    /// <param name="slot">Which widget of the field this is and the dictionaries involved.</param>
    /// <returns>Where the widget sits.</returns>
    private PdfWidgetLocation CreateWidget(FormFieldSpec spec, in WidgetSlot slot)
    {
        var definition = spec.Definition;
        var placement = definition.Widgets[slot.Position];
        var page = PdfDocumentPages.GetPage(_document, placement.PageIndex);
        FormFieldBuilder.WriteWidget(_store, slot.Widget, spec, placement, page.Id, slot.IsMerged ? default : slot.FieldId);
        if (_structure is not null)
        {
            slot.Widget.Set(KnownName.StructParent, PdfValue.FromInteger(_structure.Add(slot.WidgetId, page.Id)));
        }

        if (definition.GenerateAppearances)
        {
            var on = FormFieldValues.GetOnState(spec, slot.Position);
            var request = new FormAppearanceRequest(definition.Type, spec.Flags, slot.Field, on.Name, _cancellationToken);
            FormFieldAppearances.Attach(_document, _transaction, request, slot.Widget, placement.PageIndex, on.IsOn ? on.Name : null);
        }
        else
        {
            _needAppearances = true;
        }

        if (!slot.IsMerged)
        {
            _transaction.Replace(slot.WidgetId, PdfValue.FromDictionary(slot.Widget));
        }

        return new(placement.PageIndex, _pages.Add(placement.PageIndex, PdfValue.FromReference(slot.WidgetId)));
    }

    /// <summary>One widget being written.</summary>
    /// <param name="Position">The widget's position in the field.</param>
    /// <param name="WidgetId">The widget's object.</param>
    /// <param name="FieldId">The field's object.</param>
    /// <param name="Field">The field dictionary.</param>
    /// <param name="Widget">The widget dictionary; the field itself when merged.</param>
    /// <param name="IsMerged">Whether field and widget are one dictionary.</param>
    [DebuggerDisplay("WidgetSlot: {Position}")]
    private readonly record struct WidgetSlot(int Position, PdfObjectId WidgetId, PdfObjectId FieldId, PdfDictionary Field, PdfDictionary Widget, bool IsMerged);
}

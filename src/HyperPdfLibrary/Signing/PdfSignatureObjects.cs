// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Adds the objects of a signature to the open edit: the signature dictionary, its field and widget, the form entries and the permissions.</summary>
internal static class PdfSignatureObjects
{
    /// <summary>The annotation flags of a signature widget: print (4) and locked (128).</summary>
    private const int WidgetFlags = 132;

    /// <summary>The signature flags: the document has signatures (1) and must only be appended to (2).</summary>
    private const int SigFlags = 3;

    /// <summary>The entries a signed widget may gain: appearance, description, rectangle and flags.</summary>
    private const int WidgetEntriesAdded = 4;

    /// <summary>The entries the interactive form may gain: the signature flags and the field list.</summary>
    private const int FormEntriesAdded = 2;

    /// <summary>The entries of a new signature field that is its own widget: type, subtype, field type, name, value, flags, rectangle, description, page and appearance.</summary>
    private const int NewFieldEntries = 10;

    /// <summary>Adds a signature to the open edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The open edit.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="capacity">The room reserved for the signature value, in bytes.</param>
    /// <returns>The object ids of the signature and its widget.</returns>
    /// <exception cref="PdfSigningRefusedException">The page cannot take the widget.</exception>
    internal static PdfCreatedSignature Create(PdfDocument document, PdfEditTransaction transaction, PdfSigningPlan plan, int capacity)
    {
        var dictionary = PdfSignatureDictionaryBuilder.Build(document, plan, capacity);
        var signature = transaction.Add(PdfValue.FromDictionary(dictionary));
        var form = plan.Appearance.IsVisible ? PdfSignatureAppearances.Add(document, transaction, plan) : default;
        var widget = plan.Field is null
            ? AddNewField(document, transaction, plan, signature, form)
            : SignExistingField(document, transaction, plan, plan.Field, signature, form);
        UpdateCatalog(document, transaction, plan, signature, plan.Field is null ? widget : default);
        return new(signature, widget);
    }

    /// <summary>Adds a signature field that is also its own widget, on the page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The open edit.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="signature">The signature dictionary's object id.</param>
    /// <param name="form">The appearance stream's object id, or invalid for none.</param>
    /// <returns>The field's object id.</returns>
    /// <exception cref="PdfSigningRefusedException">The page cannot take the widget.</exception>
    private static PdfObjectId AddNewField(PdfDocument document, PdfEditTransaction transaction, PdfSigningPlan plan, PdfObjectId signature, PdfObjectId form)
    {
        _ = transaction;
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, plan.Appearance.PageIndex);
        var field = new PdfDictionary(store, NewFieldEntries);
        var allowUtf8 = PdfDocumentVersioning.GetVersion(document) >= PdfVersion.Pdf20;
        field.Set(KnownName.Type, PdfValue.FromName(KnownName.Annot));
        field.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Widget));
        field.Set(KnownName.FT, PdfValue.FromName(KnownName.Sig));
        field.Set(KnownName.T, PdfValue.FromString(PdfText.Encode(plan.FieldName, allowUtf8)));
        field.Set(KnownName.V, PdfValue.FromReference(signature));
        field.Set(KnownName.F, PdfValue.FromInteger(WidgetFlags));
        field.Set(KnownName.Rect, PdfValue.FromArray(plan.Appearance.Rectangle.ToArray(store)));
        field.Set(KnownName.TU, PdfValue.FromString(PdfSignatureAppearances.Describe(plan, allowUtf8)));
        if (page.Id.IsValid)
        {
            field.Set(KnownName.P, PdfValue.FromReference(page.Id));
        }

        SetAppearance(store, field, form);
        var index = PdfPageAnnotations.Append(store, page, field);
        if (index < 0)
        {
            throw new PdfSigningRefusedException($"Page {page.Index + 1} is not an object of its own in the file, so a signature cannot be added to it in an update. Choose another page.");
        }

        return PdfPageAnnotations.GetId(store, page, index);
    }

    /// <summary>Signs an existing field: the signature becomes its value and the appearance is drawn in its widget.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The open edit.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="field">The field.</param>
    /// <param name="signature">The signature dictionary's object id.</param>
    /// <param name="form">The appearance stream's object id, or invalid for none.</param>
    /// <returns>The widget's object id.</returns>
    private static PdfObjectId SignExistingField(PdfDocument document, PdfEditTransaction transaction, PdfSigningPlan plan, PdfSigningField field, PdfObjectId signature, PdfObjectId form)
    {
        var store = document.Objects;
        var fieldCopy = field.Dictionary.CloneWithRoom(1);
        fieldCopy.Set(KnownName.V, PdfValue.FromReference(signature));
        if (field.WidgetId == field.Id)
        {
            SetWidgetEntries(store, fieldCopy, plan, form);
            transaction.Replace(field.Id, PdfValue.FromDictionary(fieldCopy));
            return field.Id;
        }

        transaction.Replace(field.Id, PdfValue.FromDictionary(fieldCopy));
        if (field.Widget is { } widget && field.WidgetId.IsValid && form.IsValid)
        {
            var widgetCopy = widget.CloneWithRoom(WidgetEntriesAdded);
            SetWidgetEntries(store, widgetCopy, plan, form);
            transaction.Replace(field.WidgetId, PdfValue.FromDictionary(widgetCopy));
        }

        return field.WidgetId;
    }

    /// <summary>Sets the appearance, the description and the flags a signed widget carries.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="form">The appearance stream's object id, or invalid for none.</param>
    private static void SetWidgetEntries(PdfObjectStore store, PdfDictionary widget, PdfSigningPlan plan, PdfObjectId form)
    {
        SetAppearance(store, widget, form);
        if (!widget.ContainsKey(KnownName.TU))
        {
            widget.Set(KnownName.TU, PdfValue.FromString(PdfSignatureAppearances.Describe(plan, false)));
        }

        if (form.IsValid && plan.Appearance.Rectangle != default)
        {
            widget.Set(KnownName.Rect, PdfValue.FromArray(plan.Appearance.Rectangle.ToArray(store)));
        }

        widget.Set(KnownName.F, PdfValue.FromInteger(widget.GetInt32(KnownName.F, 0) | WidgetFlags));
    }

    /// <summary>Sets the normal appearance of a widget.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="form">The appearance stream's object id, or invalid for none.</param>
    private static void SetAppearance(PdfObjectStore store, PdfDictionary widget, PdfObjectId form)
    {
        if (!form.IsValid)
        {
            return;
        }

        var appearance = new PdfDictionary(store, 1);
        appearance.Set(KnownName.N, PdfValue.FromReference(form));
        widget.Set(KnownName.AP, PdfValue.FromDictionary(appearance));
    }

    /// <summary>Updates the catalog: the form's fields and signature flags, and the permissions of a certification signature.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The open edit.</param>
    /// <param name="plan">The plan.</param>
    /// <param name="signature">The signature dictionary's object id.</param>
    /// <param name="newField">The new field's object id, or invalid when an existing field was signed.</param>
    private static void UpdateCatalog(PdfDocument document, PdfEditTransaction transaction, PdfSigningPlan plan, PdfObjectId signature, PdfObjectId newField)
    {
        var store = document.Objects;
        var catalog = store.Catalog.CloneWithRoom(1);
        var formChanged = UpdateForm(store, transaction, catalog, newField);
        var permissionsChanged = false;
        if (plan.IsCertification)
        {
            var permissions = catalog.GetDictionary(KnownName.Perms)?.CloneWithRoom(1) ?? new PdfDictionary(store, 1);
            permissions.Set(KnownName.DocMDP, PdfValue.FromReference(signature));
            catalog.Set(KnownName.Perms, PdfValue.FromDictionary(permissions));
            permissionsChanged = true;
        }

        if (formChanged || permissionsChanged)
        {
            PdfPageTreeWriter.ReplaceCatalog(transaction, store, catalog);
        }
    }

    /// <summary>Updates the interactive form: adds a new field and sets the signature flags.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The open edit.</param>
    /// <param name="catalog">The copy of the catalog being changed.</param>
    /// <param name="newField">The new field's object id, or invalid when there is none.</param>
    /// <returns><see langword="true"/> when the catalog copy was changed and must be written.</returns>
    private static bool UpdateForm(PdfObjectStore store, PdfEditTransaction transaction, PdfDictionary catalog, PdfObjectId newField)
    {
        var raw = catalog.GetRaw(KnownName.AcroForm);
        var form = catalog.GetDictionary(KnownName.AcroForm)?.CloneWithRoom(FormEntriesAdded) ?? new PdfDictionary(store, FormEntriesAdded);
        form.Set(KnownName.SigFlags, PdfValue.FromInteger(form.GetInt32(KnownName.SigFlags, 0) | SigFlags));
        if (newField.IsValid)
        {
            AddField(store, transaction, form, newField);
        }

        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(form));
            return false;
        }

        catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(form));
        return true;
    }

    /// <summary>Adds a field to the form's /Fields array, writing an indirect array in place.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The open edit.</param>
    /// <param name="form">The copy of the form dictionary.</param>
    /// <param name="field">The field's object id.</param>
    private static void AddField(PdfObjectStore store, PdfEditTransaction transaction, PdfDictionary form, PdfObjectId field)
    {
        var raw = form.GetRaw(KnownName.Fields);
        var fields = form.GetArray(KnownName.Fields)?.CloneWithRoom(1) ?? new PdfArray(store, 1);
        fields.Add(PdfValue.FromReference(field));
        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromArray(fields));
        }
        else
        {
            form.Set(KnownName.Fields, PdfValue.FromArray(fields));
        }
    }
}

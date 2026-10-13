// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Handles the interactive form features PDF 2.0 deprecates (ISO 32000-2 12.7.3 Table 225 and Annex K): the XFA packet, and
/// the request that readers draw field appearances. A hybrid form keeps its AcroForm fields and values, and the fields that
/// only the XFA layout drew, or that readers would draw, get a saved appearance first. A form with no AcroForm fields is
/// refused, as the copy would have no form.
/// </summary>
internal static class Pdf20ConversionForms
{
    /// <summary>The clause of the interactive form dictionary.</summary>
    private const string FormClause = "12.7.3";

    /// <summary>The annex that deprecates XFA.</summary>
    private const string XfaClause = "Annex K";

    /// <summary>Draws the appearances the form needs saved. Runs before the form entries are removed, as drawing reads the form as it was.</summary>
    /// <param name="session">The conversion.</param>
    internal static void DrawAppearances(Pdf20ConversionSession session)
    {
        var document = session.Document;
        if (document.Catalog.GetDictionary(KnownName.AcroForm) is not { } form)
        {
            return;
        }

        var all = form.GetBoolean(KnownName.NeedAppearances);
        if (!all && !form.ContainsKey(KnownName.XFA))
        {
            return;
        }

        var pdfForm = new PdfForm(document);
        var widgets = new List<PdfFormWidget>();
        var drawn = 0;
        for (var page = 0; page < document.PageCount; page++)
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            widgets.Clear();
            PdfFormReading.GetWidgets(pdfForm, page, widgets);
            foreach (var widget in widgets)
            {
                drawn += (all || !HasAppearance(pdfForm, page, widget.Index)) && PdfFormEditing.RegenerateAppearance(pdfForm, page, widget.Index) ? 1 : 0;
            }
        }

        if (drawn > 0)
        {
            session.Changed(FormClause, string.Create(CultureInfo.InvariantCulture, $"Drew and saved the appearance of {drawn} form fields, so readers do not have to."));
        }
    }

    /// <summary>Removes XFA and the request to draw appearances from the catalog's form, or refuses when the form cannot go on without them.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    internal static bool Apply(Pdf20ConversionSession session, PdfDictionary catalog)
    {
        if (catalog.GetDictionary(KnownName.AcroForm) is not { } original)
        {
            return false;
        }

        var form = original.Clone();
        var changed = RemoveXfa(session, form);
        if (form.GetBoolean(KnownName.NeedAppearances))
        {
            _ = form.Remove(KnownName.NeedAppearances);
            session.Changed(FormClause, "Removed the request that readers draw the form fields again, as PDF 2.0 deprecates it. The fields keep their saved appearances.");
            changed = true;
        }

        if (changed)
        {
            catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(form));
            CheckAppearances(session);
        }

        return changed;
    }

    /// <summary>Removes the XFA form data, or refuses when the form has no AcroForm fields to fall back on.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="form">The form copy.</param>
    /// <returns><see langword="true"/> when XFA was removed.</returns>
    private static bool RemoveXfa(Pdf20ConversionSession session, PdfDictionary form)
    {
        if (!form.ContainsKey(KnownName.XFA))
        {
            return false;
        }

        if (form.GetArray(KnownName.Fields) is not { Count: > 0 })
        {
            session.Refuse(XfaClause, "The document is an XFA form with no standard form fields, and PDF 2.0 deprecates XFA, so the copy would have no form.");
            return false;
        }

        _ = form.Remove(KnownName.XFA);
        session.Changed(XfaClause, "Removed the XFA form data, which PDF 2.0 deprecates. The standard form fields, their values and their appearances are kept.");
        return true;
    }

    /// <summary>Refuses when a field would show nothing, as it has no saved appearance and the readers no longer draw one.</summary>
    /// <param name="session">The conversion.</param>
    private static void CheckAppearances(Pdf20ConversionSession session)
    {
        var document = session.Document;
        var pdfForm = new PdfForm(document);
        var widgets = new List<PdfFormWidget>();
        var missing = false;
        for (var page = 0; page < document.PageCount && !missing; page++)
        {
            widgets.Clear();
            PdfFormReading.GetWidgets(pdfForm, page, widgets);
            foreach (var widget in widgets)
            {
                missing |= widget.Type != PdfFieldType.Signature && !HasAppearance(pdfForm, page, widget.Index);
            }
        }

        if (missing)
        {
            session.Refuse(FormClause, "Some form fields have no saved appearance, and they could not be drawn, so they would show nothing in the copy.");
        }
    }

    /// <summary>Determines whether a widget has a normal appearance.</summary>
    /// <param name="form">The form reader.</param>
    /// <param name="page">The page index.</param>
    /// <param name="index">The widget's index on the page.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasAppearance(PdfForm form, int page, int index)
    {
        lock (form.Gate)
        {
            return PdfFormReading.Resolve(form, page, index) is { } widget && widget.Widget.GetDictionary(KnownName.AP)?.ContainsKey(KnownName.N) == true;
        }
    }
}

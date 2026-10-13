// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes the interactive form meet PDF/A: every field keeps a saved appearance instead of asking the reader to draw one
/// (ISO 19005-1 §6.9; ISO 19005-2 and -3 §6.4.1; ISO 19005-4:2020 §6.4.1), and XFA data is removed while the AcroForm
/// fields stay (ISO 19005-2 and -3 §6.4.2; ISO 19005-4:2020 §6.4.2).
/// </summary>
internal static class PdfAConversionForms
{
    /// <summary>
    /// Draws the appearance of every text and choice field from its value when the form asks the reader to draw them,
    /// so the form can stop asking. Runs before the other fixes, as the drawing reads the form as it was.
    /// </summary>
    /// <param name="session">The conversion.</param>
    internal static void DrawAppearances(PdfAConversionSession session)
    {
        var document = session.Document;
        if (document.Catalog.GetDictionary(KnownName.AcroForm) is not { } form || !form.Get(KnownName.NeedAppearances).AsBoolean())
        {
            return;
        }

        var pdfForm = new PdfForm(document);
        var widgets = new List<PdfFormWidget>();
        var drawn = 0;
        var kept = 0;
        for (var page = 0; page < document.PageCount; page++)
        {
            session.CancellationToken.ThrowIfCancellationRequested();
            widgets.Clear();
            PdfFormReading.GetWidgets(pdfForm, page, widgets);
            foreach (var widget in widgets)
            {
                if (KeepsSavedAppearance(session, form, pdfForm, page, widget.Index))
                {
                    kept++;
                    continue;
                }

                drawn += PdfFormEditing.RegenerateAppearance(pdfForm, page, widget.Index, session.CancellationToken) ? 1 : 0;
            }
        }

        if (drawn > 0)
        {
            session.Changed("Drew and saved the appearance of the form fields, so readers do not have to.", PdfARule.NeedAppearances, PdfARule.WidgetAppearance);
        }

        if (kept > 0)
        {
            session.Changed(
                string.Create(CultureInfo.InvariantCulture, $"Kept the saved appearance of {kept} form fields, as their font cannot be embedded to draw them again."),
                PdfARule.NeedAppearances,
                PdfARule.FontEmbedded);
        }
    }

    /// <summary>Removes XFA and the request to draw appearances from the catalog's form.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    internal static bool Apply(PdfAConversionSession session, PdfDictionary catalog)
    {
        if (catalog.GetDictionary(KnownName.AcroForm) is not { } original)
        {
            return false;
        }

        var form = original.Clone();
        var changed = RemoveXfa(session, form);
        if (form.Get(KnownName.NeedAppearances).AsBoolean())
        {
            if (!AllWidgetsDrawn(session))
            {
                session.Refuse("Some form fields have no saved appearance, and they could not be drawn.", PdfARule.NeedAppearances, PdfARule.WidgetAppearance);
            }

            _ = form.Remove(KnownName.NeedAppearances);
            changed = true;
        }

        if (changed)
        {
            catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(form));
        }

        return changed;
    }

    /// <summary>
    /// Determines whether a field keeps its saved appearance instead of being drawn again: it has a normal appearance, and
    /// its text font is neither embedded nor one the conversion can embed, so drawing it again would break the font rules.
    /// </summary>
    /// <param name="session">The conversion.</param>
    /// <param name="form">The interactive form dictionary.</param>
    /// <param name="pdfForm">The form reader.</param>
    /// <param name="page">The page index.</param>
    /// <param name="index">The widget's index on the page.</param>
    /// <returns><see langword="true"/> when the saved appearance is kept.</returns>
    private static bool KeepsSavedAppearance(PdfAConversionSession session, PdfDictionary form, PdfForm pdfForm, int page, int index)
    {
        lock (pdfForm.Gate)
        {
            return PdfFormReading.Resolve(pdfForm, page, index) is { } widget
                && widget.Widget.GetDictionary(KnownName.AP)?.ContainsKey(KnownName.N) == true
                && PdfAConversionFieldFonts.CannotEmbedFont(session, form, widget);
        }
    }

    /// <summary>Removes the XFA form data, or refuses when the form has no AcroForm fields to fall back on.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="form">The form copy.</param>
    /// <returns><see langword="true"/> when XFA was removed.</returns>
    private static bool RemoveXfa(PdfAConversionSession session, PdfDictionary form)
    {
        if (!form.ContainsKey(KnownName.XFA))
        {
            return false;
        }

        if (form.GetArray(KnownName.Fields) is not { Count: > 0 })
        {
            session.Refuse("The form is an XFA form with no standard form fields, and PDF/A does not allow XFA.", PdfARule.XfaForm);
            return false;
        }

        _ = form.Remove(KnownName.XFA);
        session.Changed("Removed the XFA form data; the standard form fields are kept.", PdfARule.XfaForm);
        return true;
    }

    /// <summary>Determines whether every widget annotation has an appearance dictionary.</summary>
    /// <param name="session">The conversion.</param>
    /// <returns><see langword="true"/> when they all do.</returns>
    private static bool AllWidgetsDrawn(PdfAConversionSession session)
    {
        var document = session.Document;
        for (var page = 0; page < document.PageCount; page++)
        {
            var annotations = PdfDocumentPages.GetPage(document, page).Dictionary.GetArray(KnownName.Annots);
            for (var i = 0; annotations is not null && i < annotations.Count; i++)
            {
                if (annotations.GetDictionary(i) is { } annotation && annotation.IsName(KnownName.Subtype, KnownName.Widget) && !annotation.ContainsKey(KnownName.AP))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

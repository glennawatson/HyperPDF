// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Actions operations over shared form state.</summary>
public static class PdfFormActions
{
    /// <summary>The submit flag that makes the field list an exclusion list (ISO 32000-2 12.7.6.2).</summary>
    private const int ExcludeFlag = 1;

    /// <summary>The submit flag that includes fields with no value.</summary>
    private const int IncludeNoValueFlag = 1 << 1;

    /// <summary>
    /// Resets form fields to their default values (<c>/DV</c>, or empty when there is none), as a reset-form action asks.
    /// Push buttons and signature fields are left alone.
    /// </summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action. With no fields listed every field is reset; with the exclude flag set every field but the listed ones is.</param>
    /// <returns>The number of fields reset.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    public static int Reset(PdfForm form, ResetFormAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (form.Gate)
        {
            if (!PdfFormReading.HasForm(form))
            {
                return 0;
            }

            var done = new HashSet<int>();
            var count = 0;
            for (var page = 0; page < form.Document.PageCount; page++)
            {
                count += PdfFormActions.ResetPage(form, page, action, done);
            }

            return count;
        }
    }

    /// <summary>Shows or hides the form fields and annotations a hide action names, by setting or clearing their Hidden flag.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action; its targets are field names or annotation titles.</param>
    /// <returns>The number of annotations changed.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    public static int SetHidden(PdfForm form, HideAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (form.Gate)
        {
            var count = 0;
            for (var page = 0; page < form.Document.PageCount && action.Targets.Length > 0; page++)
            {
                count += PdfFormActions.HidePage(form, page, action);
            }

            return count;
        }
    }

    /// <summary>Collects the data a submit-form action would send. Nothing is sent.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action.</param>
    /// <returns>The submission.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    public static PdfFormSubmission CreateSubmission(PdfForm form, SubmitFormAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var fields = new List<PdfSubmittedField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var widgets = new List<PdfFormWidget>();
        for (var page = 0; PdfFormReading.HasForm(form) && page < form.Document.PageCount; page++)
        {
            PdfCancellation.ThrowIfCancelled();
            widgets.Clear();
            PdfFormReading.GetWidgets(form, page, widgets);
            foreach (var widget in widgets)
            {
                if (PdfFormActions.IsSubmitted(widget, action) && seen.Add(widget.Name))
                {
                    fields.Add(new(widget.Name, widget.Value));
                }
            }
        }

        return new(action.Url, action.Flags, [..fields]);
    }

    /// <summary>Determines whether a field is chosen by a field list.</summary>
    /// <param name = "name">The field's full name.</param>
    /// <param name = "listed">The listed names; empty means every field.</param>
    /// <param name = "exclude">Whether the list names the fields to leave out.</param>
    /// <returns><see langword="true"/> when the field is chosen.</returns>
    internal static bool IsChosen(string name, string[] listed, bool exclude)
    {
        if (listed.Length == 0)
        {
            return true;
        }

        return PdfFormActions.IsListed(name, listed) != exclude;
    }

    /// <summary>Determines whether a name is listed, or lies under a listed field.</summary>
    /// <param name = "name">The full name.</param>
    /// <param name = "listed">The listed names.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    internal static bool IsListed(string name, string[] listed)
    {
        foreach (var entry in listed)
        {
            if (name.Equals(entry, StringComparison.Ordinal) || (name.Length > entry.Length && name[entry.Length] == '.' && name.StartsWith(entry, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a submit action sends a field.</summary>
    /// <param name = "widget">The widget.</param>
    /// <param name = "action">The action.</param>
    /// <returns><see langword="true"/> when the field is sent.</returns>
    internal static bool IsSubmitted(PdfFormWidget widget, SubmitFormAction action)
    {
        if (widget.Type is PdfFieldType.Unknown or PdfFieldType.PushButton or PdfFieldType.Signature || (widget.Flags & PdfFieldFlags.NoExport) != 0)
        {
            return false;
        }

        var hasValue = widget.Value.Length > 0;
        return PdfFormActions.IsChosen(widget.Name, action.Fields, (action.Flags & ExcludeFlag) != 0) && (hasValue || (action.Flags & IncludeNoValueFlag) != 0);
    }

    /// <summary>Sets or clears the Hidden flag of an annotation.</summary>
    /// <param name = "edit">The changes being made.</param>
    /// <param name = "id">The annotation's object id; not valid for a direct dictionary, which cannot be changed.</param>
    /// <param name = "annotation">The annotation as it is now.</param>
    /// <param name = "hide">Whether to hide it.</param>
    /// <returns><see langword="true"/> when the flag changed.</returns>
    internal static bool SetHiddenFlag(FormEdit edit, PdfObjectId id, PdfDictionary annotation, bool hide)
    {
        var flags = annotation.GetInt32(KnownName.F);
        var next = hide ? flags | (int)PdfAnnotationFlags.Hidden : flags & ~(int)PdfAnnotationFlags.Hidden;
        if (next == flags || edit.Edit(id)is not { } copy)
        {
            return false;
        }

        copy.Set(KnownName.F, PdfValue.FromInteger(next));
        return true;
    }

    /// <summary>Resets the chosen fields on one page.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "page">The zero based page index.</param>
    /// <param name = "action">The action.</param>
    /// <param name = "done">The object numbers of the fields already reset.</param>
    /// <returns>The number of fields reset.</returns>
    internal static int ResetPage(PdfForm form, int page, ResetFormAction action, HashSet<int> done)
    {
        if (PdfFormReading.GetAnnotations(form, page)is not { } annotations)
        {
            return 0;
        }

        var count = 0;
        var exclude = (action.Flags & ExcludeFlag) != 0;
        for (var i = 0; i < annotations.Count; i++)
        {
            if (PdfFormReading.Resolve(form, page, annotations, i) is { FieldId.IsValid: true } widget
                && widget.Type is not (PdfFieldType.Unknown or PdfFieldType.PushButton or PdfFieldType.Signature)
                && PdfFormActions.IsChosen(widget.Name, action.Fields, exclude) && done.Add(widget.FieldId.Number)
                && PdfFormActions.ResetField(form, widget))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Resets one field to its default value.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <returns><see langword="true"/> when the field was reset.</returns>
    internal static bool ResetField(PdfForm form, ResolvedWidget widget)
    {
        var edit = new FormEdit(form.Document.Objects);
        if (edit.Edit(widget.FieldId)is not { } field)
        {
            return false;
        }

        var defaultValue = FieldAttributes.Find(widget.Field, KnownName.DV);
        if (widget.IsButton)
        {
            PdfFormActions.ResetButtons(form, edit, widget, field, defaultValue);
            edit.Commit();
            return true;
        }

        if (defaultValue.IsNull)
        {
            _ = field.Remove(KnownName.V);
        }
        else
        {
            field.Set(KnownName.V, defaultValue);
        }

        _ = field.Remove(KnownName.I);
        _ = field.Remove(form.Document.Objects.Names.Intern("RV"));
        edit.Commit();
        PdfFormEditing.RegenerateAppearances(form, widget);
        return true;
    }

    /// <summary>Puts every button of a field into the state its default value names, and sets the field's value to match.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "edit">The changes being made.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "defaultValue">The field's <c>/DV</c>, or null.</param>
    internal static void ResetButtons(PdfForm form, FormEdit edit, ResolvedWidget widget, PdfDictionary field, PdfValue defaultValue)
    {
        var state = FieldAttributes.ReadState(defaultValue, form.Document.Objects.Names);
        var wanted = state.Length == 0 ? FormValues.OffState : state;
        foreach (var control in FormControl.GetControls(widget.Field, widget.FieldId))
        {
            var on = FieldAttributes.GetOnState(control.Dictionary, form.Document.Objects.Names);
            PdfFormEditing.SetControl(form, edit, widget, control, on.Length > 0 && string.Equals(on, wanted, StringComparison.Ordinal));
        }

        field.Set(KnownName.V, PdfValue.FromName(form.Document.Objects.Names.Intern(wanted)));
    }

    /// <summary>Shows or hides the named annotations on one page.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "page">The zero based page index.</param>
    /// <param name = "action">The action.</param>
    /// <returns>The number of annotations changed.</returns>
    internal static int HidePage(PdfForm form, int page, HideAction action)
    {
        if (PdfFormReading.GetAnnotations(form, page)is not { } annotations)
        {
            return 0;
        }

        var edit = new FormEdit(form.Document.Objects);
        var count = 0;
        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation
                && PdfFormActions.IsListed(FieldAttributes.GetFullName(annotation), action.Targets)
                && PdfFormActions.SetHiddenFlag(edit, annotations.GetRaw(i).AsReference(), annotation, action.Hide))
            {
                count++;
            }
        }

        edit.Commit();
        return count;
    }
}

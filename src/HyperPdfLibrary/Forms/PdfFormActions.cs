// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Reset(PdfForm form, ResetFormAction action) =>
        Reset(form, action, CancellationToken.None);

    /// <summary>Resets form fields while observing the caller's token between widgets.</summary>
    /// <param name="form">The shared form state.</param>
    /// <param name="action">The reset action.</param>
    /// <param name="cancellationToken">Cancels the reset.</param>
    /// <returns>The number of fields reset.</returns>
    public static int Reset(PdfForm form, ResetFormAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (form.Gate)
        {
            if (!PdfFormReading.HasForm(form))
            {
                return 0;
            }

            var done = default(IntSet);
            try
            {
                var count = 0;
                for (var page = 0; page < form.Document.PageCount; page++)
                {
                    count += PdfFormActions.ResetPage(form, page, action, ref done, cancellationToken);
                }

                return count;
            }
            finally
            {
                done.Dispose();
            }
        }
    }

    /// <summary>Shows or hides the form fields and annotations a hide action names, by setting or clearing their Hidden flag.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action; its targets are field names or annotation titles.</param>
    /// <returns>The number of annotations changed.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SetHidden(PdfForm form, HideAction action) =>
        SetHidden(form, action, CancellationToken.None);

    /// <summary>Shows or hides named fields while observing the caller's token.</summary>
    /// <param name="form">The shared form state.</param>
    /// <param name="action">The hide action.</param>
    /// <param name="cancellationToken">Cancels the change.</param>
    /// <returns>The number of annotations changed.</returns>
    public static int SetHidden(PdfForm form, HideAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (form.Gate)
        {
            var count = 0;
            for (var page = 0; page < form.Document.PageCount && !action.Targets.IsEmpty; page++)
            {
                count += PdfFormActions.HidePage(form, page, action, cancellationToken);
            }

            return count;
        }
    }

    /// <summary>Collects the data a submit-form action would send. Nothing is sent.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "action">The action.</param>
    /// <returns>The submission.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "action"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFormSubmission CreateSubmission(PdfForm form, SubmitFormAction action) =>
        CreateSubmission(form, action, CancellationToken.None);

    /// <summary>Collects a submission while observing the caller's token.</summary>
    /// <param name="form">The shared form state.</param>
    /// <param name="action">The submit action.</param>
    /// <param name="cancellationToken">Cancels the collection.</param>
    /// <returns>The submission.</returns>
    public static PdfFormSubmission CreateSubmission(PdfForm form, SubmitFormAction action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        var fields = new List<PdfSubmittedField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var widgets = new List<PdfFormWidget>();
        for (var page = 0; PdfFormReading.HasForm(form) && page < form.Document.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
    internal static bool IsChosen(string name, ReadOnlySpan<string> listed, bool exclude)
    {
        if (listed.IsEmpty)
        {
            return true;
        }

        return PdfFormActions.IsListed(name, listed) != exclude;
    }

    /// <summary>Determines whether a name is listed, or lies under a listed field.</summary>
    /// <param name = "name">The full name.</param>
    /// <param name = "listed">The listed names.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    internal static bool IsListed(string name, ReadOnlySpan<string> listed)
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
        return PdfFormActions.IsChosen(widget.Name, action.Fields.AsSpan(), (action.Flags & ExcludeFlag) != 0) && (hasValue || (action.Flags & IncludeNoValueFlag) != 0);
    }

    /// <summary>Sets or clears the Hidden flag of an annotation.</summary>
    /// <param name = "edit">The changes being made.</param>
    /// <param name = "id">The annotation's object id; not valid for a direct dictionary, which cannot be changed.</param>
    /// <param name = "annotation">The annotation as it is now.</param>
    /// <param name = "hide">Whether to hide it.</param>
    /// <returns><see langword="true"/> when the flag changed.</returns>
    internal static bool SetHiddenFlag(ref FormEdit edit, PdfObjectId id, PdfDictionary annotation, bool hide)
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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of fields reset.</returns>
    internal static int ResetPage(PdfForm form, int page, ResetFormAction action, ref IntSet done, CancellationToken cancellationToken)
    {
        if (PdfFormReading.GetAnnotations(form, page)is not { } annotations)
        {
            return 0;
        }

        var count = 0;
        var exclude = (action.Flags & ExcludeFlag) != 0;
        for (var i = 0; i < annotations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PdfFormReading.Resolve(form, page, annotations, i) is { } widget
                && PdfFormActions.IsResettable(widget, action, exclude)
                && done.Add(widget.FieldId.Number)
                && PdfFormActions.ResetField(form, widget))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Determines whether a reset action resets a field: it has a type that holds a value, and the action chooses it.</summary>
    /// <param name = "widget">One widget of the field.</param>
    /// <param name = "action">The action.</param>
    /// <param name = "exclude">Whether the action's field list names the fields to leave out.</param>
    /// <returns><see langword="true"/> when the field is reset.</returns>
    internal static bool IsResettable(in ResolvedWidget widget, ResetFormAction action, bool exclude) =>
        widget.FieldId.IsValid
        && widget.Type is not (PdfFieldType.Unknown or PdfFieldType.PushButton or PdfFieldType.Signature)
        && (action.Fields.IsEmpty || PdfFormActions.IsChosen(widget.Name, action.Fields.AsSpan(), exclude));

    /// <summary>Resets one field to its default value.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <returns><see langword="true"/> when the field was reset.</returns>
    internal static bool ResetField(PdfForm form, in ResolvedWidget widget)
    {
        var edit = new FormEdit(form.Document.Objects);
        try
        {
            if (edit.Edit(widget.FieldId)is not { } field)
            {
                return false;
            }

            var defaultValue = FieldAttributes.Find(widget.Field, KnownName.DV);
            if (widget.IsButton)
            {
                PdfFormActions.ResetButtons(form, ref edit, widget, field, defaultValue);
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
            _ = field.Remove(form.Document.Objects.Names.Intern("RV"u8));
            edit.Commit();
            PdfFormEditing.RegenerateAppearances(form, widget);
            return true;
        }
        finally
        {
            edit.Dispose();
        }
    }

    /// <summary>Puts every button of a field into the state its default value names, and sets the field's value to match.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "edit">The changes being made.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "defaultValue">The field's <c>/DV</c>, or null.</param>
    internal static void ResetButtons(PdfForm form, ref FormEdit edit, in ResolvedWidget widget, PdfDictionary field, PdfValue defaultValue)
    {
        var names = form.Document.Objects.Names;
        var wanted = defaultValue.Kind switch
        {
            PdfKind.Name => defaultValue.AsName(),
            PdfKind.String when !defaultValue.AsStringBytes().IsEmpty => names.Intern(FieldAttributes.ReadState(defaultValue, names)),
            _ => default,
        };
        wanted = wanted.IsNone ? names.Intern("Off"u8) : wanted;
        var controls = FormControls.Read(widget.Field, widget.FieldId);
        try
        {
            for (var i = 0; i < controls.Count; i++)
            {
                var on = FieldAttributes.GetOnStateName(controls[i].Dictionary, names);
                PdfFormEditing.SetControl(form, ref edit, widget, controls[i], !on.IsNone && on.Equals(wanted));
            }
        }
        finally
        {
            controls.Dispose();
        }

        field.Set(KnownName.V, PdfValue.FromName(wanted));
    }

    /// <summary>Shows or hides the named annotations on one page.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "page">The zero based page index.</param>
    /// <param name = "action">The action.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of annotations changed.</returns>
    internal static int HidePage(PdfForm form, int page, HideAction action, CancellationToken cancellationToken)
    {
        if (PdfFormReading.GetAnnotations(form, page)is not { } annotations)
        {
            return 0;
        }

        var edit = new FormEdit(form.Document.Objects);
        try
        {
            var count = 0;
            for (var i = 0; i < annotations.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (annotations.GetDictionary(i) is { } annotation
                    && PdfFormActions.IsListed(FieldAttributes.GetFullName(annotation), action.Targets.AsSpan())
                    && PdfFormActions.SetHiddenFlag(ref edit, annotations.GetRaw(i).AsReference(), annotation, action.Hide))
                {
                    count++;
                }
            }

            edit.Commit();
            return count;
        }
        finally
        {
            edit.Dispose();
        }
    }
}

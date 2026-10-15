// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Editing operations over shared form state.</summary>
public static class PdfFormEditing
{
    /// <summary>The most digits an <see cref="int"/> takes to write.</summary>
    private const int MaxDigits = 11;

    /// <summary>Replaces the text of a text field, or of a combo box that accepts typed text.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <param name = "text">The new text. A text field's character limit cuts it short.</param>
    /// <returns><see langword="true"/> when the value was set; <see langword="false"/> for a missing, read-only or wrong kind of widget.</returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "text"/> is <see langword="null"/>.</exception>
    public static bool SetText(PdfForm form, int pageIndex, int index, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        lock (form.Gate)
        {
            if (!PdfFormReading.HasForm(form) || PdfFormReading.Resolve(form, pageIndex, index) is not { } widget || !PdfFormEditing.CanTypeInto(widget))
            {
                return false;
            }

            var edit = new FormEdit(form.Document.Objects);
            try
            {
                if (edit.Edit(widget.FieldId) is not { } field)
                {
                    return false;
                }

                PdfFormEditing.WriteText(form, field, widget, text);
                edit.Commit();
                PdfFormEditing.RegenerateAppearances(form, widget);
                return true;
            }
            finally
            {
                edit.Dispose();
            }
        }
    }

    /// <summary>Switches a check box or radio button on or off.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <param name = "isChecked">The new state.</param>
    /// <returns>
    /// <see langword="true"/> when the button ends in that state. Switching a radio button off, or changing a read-only
    /// or missing widget, leaves the state as it was and returns <see langword="false"/>.
    /// </returns>
    public static bool SetChecked(PdfForm form, int pageIndex, int index, bool isChecked)
    {
        lock (form.Gate)
        {
            if (!PdfFormReading.HasForm(form) || PdfFormReading.Resolve(form, pageIndex, index) is not { IsButton: true } widget)
            {
                return false;
            }

            var controls = FormControls.Read(widget.Field, widget.FieldId);
            try
            {
                return PdfFormEditing.SetChecked(form, widget, ref controls, isChecked);
            }
            finally
            {
                controls.Dispose();
            }
        }
    }

    /// <summary>Selects an option of a combo box or list box. A list box that allows several selections keeps the others.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <param name = "option">The option's index.</param>
    /// <returns><see langword="true"/> when selected; <see langword="false"/> for a missing, read-only or wrong kind of widget or an option out of range.</returns>
    public static bool SelectOption(PdfForm form, int pageIndex, int index, int option)
    {
        lock (form.Gate)
        {
            if (!PdfFormReading.HasForm(form)
        || PdfFormReading.Resolve(
        form,
        pageIndex,
        index) is not
        {
            IsChoice: true,
            IsReadOnly: false,
            FieldId.IsValid: true
        } widget
        || (uint)option >= (uint)FormChoices.Count(widget.Field))
            {
                return false;
            }

            var edit = new FormEdit(form.Document.Objects);
            try
            {
                if (edit.Edit(widget.FieldId) is not { } field)
                {
                    return false;
                }

                PdfFormEditing.WriteSelection(form, field, widget, option);
                edit.Commit();
                PdfFormEditing.RegenerateAppearances(form, widget);
                return true;
            }
            finally
            {
                edit.Dispose();
            }
        }
    }

    /// <summary>Rebuilds the appearance of a text, choice or push button field from its value and appearance characteristics, for every widget of the field.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <returns><see langword="true"/> when the widget is a text, choice or push button field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RegenerateAppearance(PdfForm form, int pageIndex, int index) =>PdfFormEditing.RegenerateAppearance(form, pageIndex, index, CancellationToken.None);

    /// <summary>Rebuilds the appearance of a text, choice or push button field from its value and appearance characteristics, for every widget of the field.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <param name = "cancellationToken">A token that cancels the drawing; it is checked for every widget, line and row.</param>
    /// <returns><see langword="true"/> when the widget is a text, choice or push button field.</returns>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static bool RegenerateAppearance(PdfForm form, int pageIndex, int index, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (form.Gate)
        {
            if (!PdfFormReading.HasForm(form) || PdfFormReading.Resolve(form, pageIndex, index) is not { } widget || !(widget.IsChoice || widget.Type is PdfFieldType.Text or PdfFieldType.PushButton))
            {
                return false;
            }

            PdfFormEditing.RegenerateAppearances(form, widget, cancellationToken);
            return true;
        }
    }

    /// <summary>Switches one button of a field on or off.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "controls">The field's buttons.</param>
    /// <param name = "isChecked">The new state.</param>
    /// <returns><see langword="true"/> when the button ends in that state.</returns>
    internal static bool SetChecked(PdfForm form, in ResolvedWidget widget, ref FormControls controls, bool isChecked)
    {
        var position = PdfFormEditing.IndexOf(ref controls, widget);
        if (position < 0)
        {
            return false;
        }

        if (controls[position].IsChecked(form.Document.Objects.Names) == isChecked)
        {
            return true;
        }

        // A radio button is switched on by selecting it; selecting another one switches it off.
        if (widget.IsReadOnly || !widget.IsEditable || (!isChecked && widget.Type == PdfFieldType.RadioButton))
        {
            return false;
        }

        return PdfFormEditing.ApplyCheck(form, widget, ref controls, position, isChecked);
    }

    /// <summary>Finds a widget among the buttons of its field.</summary>
    /// <param name = "controls">The field's buttons.</param>
    /// <param name = "widget">The widget.</param>
    /// <returns>The position, or -1.</returns>
    internal static int IndexOf(ref FormControls controls, in ResolvedWidget widget)
    {
        for (var i = 0; i < controls.Count; i++)
        {
            if (ReferenceEquals(controls[i].Dictionary, widget.Widget))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Draws the appearance of a text or choice widget.</summary>
    /// <param name = "context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateAppearance(in AppearanceContext context)
    {
        if (context.Type == PdfFieldType.PushButton)
        {
            return FormPushButtonAppearance.Create(context);
        }

        if (context.Type == PdfFieldType.ListBox)
        {
            return FormTextAppearance.CreateList(context);
        }

        if (context.Type == PdfFieldType.Text)
        {
            return FormTextAppearance.CreateText(context, FieldAttributes.Find(context.Field, KnownName.V));
        }

        var text = FormValues.ReadText(context.Field, context.Type);
        if (context.Type == PdfFieldType.ComboBox)
        {
            var option = FormChoices.FindValue(context.Field, text);
            text = option >= 0 ? FormChoices.GetLabel(context.Field, option) : text;
        }

        return FormTextAppearance.CreateText(context, text);
    }

    /// <summary>Works out what a button in the group becomes when one is switched.</summary>
    /// <param name = "unison">Whether buttons with the same export value switch together.</param>
    /// <param name = "isChecked">The switched button's new state.</param>
    /// <param name = "position">The switched button's position.</param>
    /// <param name = "current">The position of the button being considered.</param>
    /// <param name = "exports">The export values of the considered button and the switched one.</param>
    /// <param name = "states">The on-states of the considered button and the switched one.</param>
    /// <returns>The new state; <see langword="null"/> when the button is left as it is.</returns>
    internal static bool? NextState(bool unison, bool isChecked, int position, int current, ButtonNames exports, ButtonNames states)
    {
        bool? others = isChecked ? false : null;
        if (!unison)
        {
            return current == position ? isChecked : others;
        }

        if (exports.Current != exports.Switched)
        {
            return others;
        }

        return states.Current == states.Switched ? isChecked : others;
    }

    /// <summary>Determines whether text can be put in a field.</summary>
    /// <param name = "widget">The widget.</param>
    /// <returns><see langword="true"/> for a writable text field or editable combo box.</returns>
    internal static bool CanTypeInto(in ResolvedWidget widget) =>
        widget.FieldId.IsValid
        && !widget.IsReadOnly
        && (widget.Type == PdfFieldType.Text
        || (widget.Type == PdfFieldType.ComboBox
        && (widget.Flags & PdfFieldFlags.Edit) != 0));

    /// <summary>Makes a new string value, encoded for the document's version.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "text">The text.</param>
    /// <returns>The PDF string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfValue ToPdfString(PdfForm form, string text) => PdfValue.FromString(PdfText.Encode(text, form.Document.Objects.Version));

    /// <summary>Makes a string value that replaces an entry, encoded the way the entry was.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field copy holding the entry.</param>
    /// <param name = "key">The entry's key.</param>
    /// <param name = "text">The text.</param>
    /// <returns>The PDF string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfValue ToPdfString(
        PdfForm form,
        PdfDictionary field,
        PdfName key,
        string text) =>
        PdfValue.FromString(PdfText.Encode(
        text,
        field.GetStringBytes(key),
        form.Document.Objects.Version));

    /// <summary>Sets a text or combo box value on a copy of the field.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "text">The text.</param>
    internal static void WriteText(PdfForm form, PdfDictionary field, in ResolvedWidget widget, string text)
    {
        if (widget.Type == PdfFieldType.ComboBox)
        {
            var option = FormChoices.FindValue(widget.Field, text);
            option = option < 0 ? FormChoices.FindLabel(widget.Field, text) : option;
            if (option >= 0)
            {
                PdfFormEditing.WriteSelection(form, field, widget, option);
                return;
            }

            field.Set(KnownName.V, PdfFormEditing.ToPdfString(form, field, KnownName.V, text));
            _ = field.Remove(KnownName.I);
            return;
        }

        var maxLength = PdfFormReading.ReadMaxLength(widget.Field);
        var limited = maxLength > 0 && text.Length > maxLength ? text[..maxLength] : text;
        field.Set(KnownName.V, PdfFormEditing.ToPdfString(form, field, KnownName.V, limited));
        PdfFormEditing.WriteRichValue(form, field, widget, limited);
    }

    /// <summary>Sets <c>/RV</c> on a copy of a field that keeps a rich value.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "text">The text.</param>
    internal static void WriteRichValue(PdfForm form, PdfDictionary field, in ResolvedWidget widget, string text)
    {
        if ((widget.Flags & PdfFieldFlags.RichTextOrRadiosInUnison) == 0)
        {
            return;
        }

        // The rich value is XHTML in the field's default style, not the plain text (ISO 32000-2 12.7.4.3).
        var rv = form.Document.Objects.Names.Intern("RV");
        field.Set(rv, PdfFormEditing.ToPdfString(form, field, rv, RichTextWriter.Write(text, FormRichText.GetDefaultStyleText(field))));
    }

    /// <summary>Selects an option on a copy of the field: sets <c>/V</c> and <c>/I</c>.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "option">The option index.</param>
    internal static void WriteSelection(PdfForm form, PdfDictionary field, in ResolvedWidget widget, int option)
    {
        var value = FormChoices.GetValue(widget.Field, option);
        if (widget.Type == PdfFieldType.ListBox && (widget.Flags & PdfFieldFlags.MultiSelect) != 0)
        {
            PdfFormEditing.WriteMultipleSelection(form, field, widget.Field, option);
            return;
        }

        field.Set(KnownName.V, PdfFormEditing.ToPdfString(form, field, KnownName.V, value));
        var indices = new PdfArray(form.Document.Objects);
        indices.Add(PdfValue.FromInteger(option));
        field.Set(KnownName.I, PdfValue.FromArray(indices));
    }

    /// <summary>Selects an option in a list that allows several, keeping the options already selected.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "original">The field before the change.</param>
    /// <param name = "option">The option to add.</param>
    internal static void WriteMultipleSelection(PdfForm form, PdfDictionary field, PdfDictionary original, int option)
    {
        var useIndices = FormChoices.UsesSelectedIndices(original);
        var values = new PdfArray(form.Document.Objects);
        var indices = new PdfArray(form.Document.Objects);
        var count = FormChoices.Count(original);
        for (var i = 0; i < count; i++)
        {
            if (i != option && !FormChoices.IsSelected(original, i, useIndices))
            {
                continue;
            }

            values.Add(PdfFormEditing.ToPdfString(form, FormChoices.GetValue(original, i)));
            indices.Add(PdfValue.FromInteger(i));
        }

        field.Set(KnownName.V, PdfValue.FromArray(values));
        field.Set(KnownName.I, PdfValue.FromArray(indices));
    }

    /// <summary>Rebuilds the appearance of every widget of a field from its stored value.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field, as resolved before the change.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RegenerateAppearances(PdfForm form, in ResolvedWidget widget) =>PdfFormEditing.RegenerateAppearances(form, widget, CancellationToken.None);

    /// <summary>Rebuilds the appearance of every widget of a field from its stored value, and puts the fonts they use in the form's <c>/DR</c>.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field, as resolved before the change.</param>
    /// <param name = "cancellationToken">A token that cancels the drawing.</param>
    internal static void RegenerateAppearances(PdfForm form, in ResolvedWidget widget, CancellationToken cancellationToken)
    {
        if (StoreReading.GetDictionary(form.Document.Objects, widget.FieldId) is not { } field)
        {
            return;
        }

        var resources = HyperPdfLibrary.Document.PdfDocumentPages.GetPage(form.Document, widget.PageIndex).Resources;
        var edit = new FormEdit(form.Document.Objects);
        try
        {
            var fonts = HyperPdfLibrary.Document.PdfDocumentRendering.GetRenderCache(form.Document).Fonts;
            var acroForm = form.Document.Catalog.GetDictionary(KnownName.AcroForm);
            var controls = FormControls.Read(field, widget.FieldId);
            try
            {
                for (var i = 0; i < controls.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!controls[i].Id.IsValid)
                    {
                        continue;
                    }

                    var context = new AppearanceContext(form.Document.Objects, controls[i].Dictionary, field, widget.Type, widget.Flags, acroForm, resources)
                    {
                        Fonts = fonts,
                        CancellationToken = cancellationToken,
                    };
                    var appearance = PdfFormEditing.CreateAppearance(context);
                    PdfFormEditing.StoreAppearance(form, ref edit, controls[i], appearance);
                    FormDefaultResources.AddFonts(form.Document.Objects, appearance);
                }
            }
            finally
            {
                controls.Dispose();
            }

            edit.Commit();
        }
        finally
        {
            edit.Dispose();
        }
    }

    /// <summary>
    /// Makes a stream the widget's normal appearance. An appearance this form drew earlier is replaced in place, so
    /// repeated edits do not pile up objects; any other appearance may be shared, so it is left alone.
    /// </summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "edit">The changes being made.</param>
    /// <param name = "control">The widget.</param>
    /// <param name = "stream">The appearance.</param>
    internal static void StoreAppearance(PdfForm form, ref FormEdit edit, FormControl control, PdfStream stream)
    {
        var current = control.Dictionary.GetDictionary(KnownName.AP)?.GetRaw(KnownName.N) ?? default;
        if (current.IsReference && form.Drawn.Contains(current.AsReference().Number))
        {
            StoreEditing.Replace(form.Document.Objects, current.AsReference(), PdfValue.FromStream(stream), PdfCacheScopes.None);
            return;
        }

        if (edit.Edit(control.Id) is not { } widget)
        {
            return;
        }

        var id = StoreEditing.Add(form.Document.Objects, PdfValue.FromStream(stream), PdfCacheScopes.None);
        _ = form.Drawn.Add(id.Number);
        var appearance = widget.GetDictionary(KnownName.AP)?.Clone() ?? new PdfDictionary(form.Document.Objects);
        appearance.Set(KnownName.N, PdfValue.FromReference(id));
        widget.Set(KnownName.AP, PdfValue.FromDictionary(appearance));
    }

    /// <summary>Changes a button's state and the buttons it affects.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">The widget being switched.</param>
    /// <param name = "controls">The field's buttons.</param>
    /// <param name = "position">The widget's position in the field.</param>
    /// <param name = "isChecked">The new state.</param>
    /// <returns><see langword="true"/> when the change was written.</returns>
    internal static bool ApplyCheck(PdfForm form, in ResolvedWidget widget, ref FormControls controls, int position, bool isChecked)
    {
        var edit = new FormEdit(form.Document.Objects);
        try
        {
            if (edit.Edit(widget.FieldId) is not { } field)
            {
                return false;
            }

            var names = form.Document.Objects.Names;
            var unison = widget.Type == PdfFieldType.CheckBox || (widget.Flags & PdfFieldFlags.RichTextOrRadiosInUnison) != 0;
            var switchedExport = PdfFormReading.GetExportName(form, widget.Field, controls[position].Dictionary, position);
            var switchedState = FieldAttributes.GetOnStateName(controls[position].Dictionary, names);
            for (var i = 0; i < controls.Count; i++)
            {
                var exports = new ButtonNames(PdfFormReading.GetExportName(form, widget.Field, controls[i].Dictionary, i), switchedExport);
                var states = new ButtonNames(FieldAttributes.GetOnStateName(controls[i].Dictionary, names), switchedState);
                if (PdfFormEditing.NextState(unison, isChecked, position, i, exports, states) is { } state)
                {
                    PdfFormEditing.SetControl(form, ref edit, widget, controls[i], state);
                }
            }

            PdfFormEditing.WriteButtonValue(form, field, widget.Field, switchedExport, position, isChecked);
            edit.Commit();
            return true;
        }
        finally
        {
            edit.Dispose();
        }
    }

    /// <summary>Sets a button's appearance state, drawing the appearances first when the widget has none.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "edit">The changes being made.</param>
    /// <param name = "widget">The widget the user acted on, whose field and flags apply to every button.</param>
    /// <param name = "control">The button.</param>
    /// <param name = "isOn">Whether it becomes on.</param>
    internal static void SetControl(PdfForm form, ref FormEdit edit, in ResolvedWidget widget, FormControl control, bool isOn)
    {
        if (edit.Edit(control.Id) is not { } clone)
        {
            return;
        }

        var names = form.Document.Objects.Names;
        var name = isOn ? PdfFormEditing.EnsureOnState(form, clone, widget) : names.Intern("Off"u8);
        if (!PdfFormEditing.StateIs(clone.Get(KnownName.AS), name, names))
        {
            clone.Set(KnownName.AS, PdfValue.FromName(name));
        }
    }

    /// <summary>Determines whether an appearance state value names a state.</summary>
    /// <param name = "state">The value: a name, or a string some writers use.</param>
    /// <param name = "name">The state's name.</param>
    /// <param name = "names">The document's names.</param>
    /// <returns><see langword="true"/> when the value spells the name.</returns>
    internal static bool StateIs(PdfValue state, PdfName name, PdfNameTable names) => state.Kind switch
    {
        PdfKind.Name => state.AsName().Equals(name),
        PdfKind.String => string.Equals(FieldAttributes.ReadState(state, names), names.GetString(name), StringComparison.Ordinal),
        _ => false,
    };

    /// <summary>Finds the name of a button's on-state, drawing the on and off appearances when it has none.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "clone">The widget copy.</param>
    /// <param name = "widget">The widget the user acted on.</param>
    /// <returns>The on-state name.</returns>
    internal static PdfName EnsureOnState(PdfForm form, PdfDictionary clone, in ResolvedWidget widget)
    {
        var existing = FieldAttributes.GetOnStateName(clone, form.Document.Objects.Names);
        if (!existing.IsNone)
        {
            return existing;
        }

        var context = new AppearanceContext(
        form.Document.Objects,
        clone,
        widget.Field,
        widget.Type,
        widget.Flags,
        form.Document.Catalog.GetDictionary(KnownName.AcroForm),
        HyperPdfLibrary.Document.PdfDocumentPages.GetPage(
        form.Document,
        widget.PageIndex).Resources);
        var yes = form.Document.Objects.Names.Intern("Yes"u8);
        var states = new PdfDictionary(form.Document.Objects);
        var on = StoreEditing.Add(form.Document.Objects, PdfValue.FromStream(FormButtonAppearance.CreateButton(context, true)), PdfCacheScopes.None);
        states.Set(yes, PdfValue.FromReference(on));
        var off = StoreEditing.Add(form.Document.Objects, PdfValue.FromStream(FormButtonAppearance.CreateButton(context, false)), PdfCacheScopes.None);
        states.Set(form.Document.Objects.Names.Intern("Off"u8), PdfValue.FromReference(off));
        var appearance = clone.GetDictionary(KnownName.AP)?.Clone() ?? new PdfDictionary(form.Document.Objects);
        appearance.Set(KnownName.N, PdfValue.FromDictionary(states));
        clone.Set(KnownName.AP, PdfValue.FromDictionary(appearance));
        return yes;
    }

    /// <summary>Updates a button field's <c>/V</c> after one of its buttons was switched.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "field">The field copy.</param>
    /// <param name = "original">The field before the change.</param>
    /// <param name = "export">The switched button's export value.</param>
    /// <param name = "position">The switched button's position.</param>
    /// <param name = "isChecked">The new state.</param>
    internal static void WriteButtonValue(PdfForm form, PdfDictionary field, PdfDictionary original, PdfName export, int position, bool isChecked)
    {
        var names = form.Document.Objects.Names;
        if (FieldAttributes.Find(original, KnownName.Opt).AsArray() is not null)
        {
            if (isChecked)
            {
                Span<byte> digits = stackalloc byte[MaxDigits];
                _ = position.TryFormat(digits, out var written, default, System.Globalization.CultureInfo.InvariantCulture);
                field.Set(KnownName.V, PdfValue.FromName(names.Intern(digits[..written])));
            }

            return;
        }

        if (isChecked)
        {
            field.Set(KnownName.V, PdfValue.FromName(export));
        }
        else if (PdfFormEditing.StateIs(FieldAttributes.Find(original, KnownName.V), export, names))
        {
            field.Set(KnownName.V, PdfValue.FromName(names.Intern("Off"u8)));
        }
    }
}

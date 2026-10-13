// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Import operations over shared form state.</summary>
public static class PdfFormImport
{
    /// <summary>Sets the value of the field a widget belongs to from text: a text value, an option's export value or label, or a button's state.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "index">The widget's index in the page's annotations.</param>
    /// <param name = "values">The values; a multiple-choice list takes all of them, any other field the first.</param>
    /// <returns>
    /// <see langword="true"/> when the value was set; <see langword="false"/> for a missing or wrong kind of widget, or a
    /// value the field cannot take. The read-only flag is ignored: it guards typing (ISO 32000-2 Table 227), not imported data.
    /// </returns>
    /// <exception cref = "ArgumentNullException"><paramref name = "values"/> is <see langword="null"/>.</exception>
    public static bool ImportValue(PdfForm form, int pageIndex, int index, string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        lock (form.Gate)
        {
            if (values.Length == 0 || !PdfFormReading.HasForm(form) || PdfFormReading.Resolve(form, pageIndex, index)is not { } widget)
            {
                return false;
            }

            return widget.Type switch
            {
                PdfFieldType.Text => PdfFormImport.ImportText(form, widget, values[0]),
                PdfFieldType.ComboBox or PdfFieldType.ListBox => PdfFormImport.ImportChoice(form, widget, values),
                PdfFieldType.CheckBox or PdfFieldType.RadioButton => PdfFormImport.ImportButton(form, widget, values[0]),
                _ => false,
            };
        }
    }

    /// <summary>Finds the option that a value stands for: by export value first, then by label.</summary>
    /// <param name = "field">The field.</param>
    /// <param name = "value">The value.</param>
    /// <returns>The option's index, or -1.</returns>
    internal static int FindOption(PdfDictionary field, string value)
    {
        var option = FormChoices.FindValue(field, value);
        return option >= 0 ? option : FormChoices.FindLabel(field, value);
    }

    /// <summary>Finds the options that values stand for, each once.</summary>
    /// <param name = "field">The field.</param>
    /// <param name = "values">The values.</param>
    /// <returns>The option indices, in the order the values name them.</returns>
    internal static List<int> FindOptions(PdfDictionary field, string[] values)
    {
        var options = new List<int>(values.Length);
        foreach (var value in values)
        {
            var option = PdfFormImport.FindOption(field, value);
            if (option >= 0 && !options.Contains(option))
            {
                options.Add(option);
            }
        }

        return options;
    }

    /// <summary>Selects options from text.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "values">The values.</param>
    /// <returns><see langword="true"/> when set.</returns>
    internal static bool ImportChoice(PdfForm form, ResolvedWidget widget, string[] values)
    {
        if (!widget.FieldId.IsValid)
        {
            return false;
        }

        var options = PdfFormImport.FindOptions(widget.Field, values);
        if (options.Count == 0 && PdfFormImport.IsEmpty(values) && FormChoices.FirstSelected(widget.Field) < 0)
        {
            // A choice field with nothing selected exports an empty value; importing it leaves the field as it is.
            return true;
        }

        // An editable combo box takes text that is none of its options.
        return options.Count == 0
            ? widget.Type == PdfFieldType.ComboBox && (widget.Flags & PdfFieldFlags.Edit) != 0 && PdfFormImport.ImportText(form, widget, values[0])
            : PdfFormImport.ReplaceSelection(form, widget, options);
    }

    /// <summary>Determines whether every value is an empty string.</summary>
    /// <param name = "values">The values.</param>
    /// <returns><see langword="true"/> when none holds text.</returns>
    internal static bool IsEmpty(string[] values)
    {
        foreach (var value in values)
        {
            if (value.Length > 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sets a text value. Importing data ignores the read-only flag, which only guards typing.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "text">The text.</param>
    /// <returns><see langword="true"/> when written.</returns>
    internal static bool ImportText(PdfForm form, ResolvedWidget widget, string text)
    {
        var edit = new FormEdit(form.Document.Objects);
        if (!widget.FieldId.IsValid || edit.Edit(widget.FieldId)is not { } field)
        {
            return false;
        }

        PdfFormEditing.WriteText(form, field, widget, text);
        edit.Commit();
        PdfFormEditing.RegenerateAppearances(form, widget);
        return true;
    }

    /// <summary>Makes exactly the given options the selection. A field that allows one choice takes the first.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "options">The option indices.</param>
    /// <returns><see langword="true"/> when written.</returns>
    internal static bool ReplaceSelection(PdfForm form, ResolvedWidget widget, List<int> options)
    {
        var edit = new FormEdit(form.Document.Objects);
        if (edit.Edit(widget.FieldId)is not { } field)
        {
            return false;
        }

        if (widget.Type != PdfFieldType.ListBox || (widget.Flags & PdfFieldFlags.MultiSelect) == 0)
        {
            PdfFormEditing.WriteSelection(form, field, widget, options[0]);
            edit.Commit();
            PdfFormEditing.RegenerateAppearances(form, widget);
            return true;
        }

        options.Sort();
        var values = new PdfArray(form.Document.Objects, options.Count);
        var indices = new PdfArray(form.Document.Objects, options.Count);
        foreach (var option in options)
        {
            values.Add(PdfFormEditing.ToPdfString(form, FormChoices.GetValue(widget.Field, option)));
            indices.Add(PdfValue.FromInteger(option));
        }

        field.Set(KnownName.V, PdfValue.FromArray(values));
        field.Set(KnownName.I, PdfValue.FromArray(indices));
        edit.Commit();
        PdfFormEditing.RegenerateAppearances(form, widget);
        return true;
    }

    /// <summary>Switches a field's buttons so the one named by a state is on, or all are off.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <param name = "state">The export value or on-state; <c>Off</c> for none.</param>
    /// <returns><see langword="true"/> when the field ends in that state.</returns>
    internal static bool ImportButton(PdfForm form, ResolvedWidget widget, string state)
    {
        if (!widget.IsEditable)
        {
            return false;
        }

        var controls = FormControl.GetControls(widget.Field, widget.FieldId);
        if (string.Equals(state, FormValues.OffState, StringComparison.Ordinal))
        {
            return PdfFormImport.SwitchAllOff(form, widget, controls);
        }

        var target = PdfFormImport.FindButton(form, widget, controls, state);
        if (target < 0)
        {
            return false;
        }

        return controls[target].IsChecked(form.Document.Objects.Names) || PdfFormEditing.ApplyCheck(form, widget, controls, target, true);
    }

    /// <summary>Finds the button whose export value or on-state is a name.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <param name = "controls">The field's buttons.</param>
    /// <param name = "state">The name.</param>
    /// <returns>The button's position, or -1.</returns>
    internal static int FindButton(PdfForm form, ResolvedWidget widget, List<FormControl> controls, string state)
    {
        for (var i = 0; i < controls.Count; i++)
        {
            if (string.Equals(PdfFormReading.GetExportValue(form, widget.Field, controls, i), state, StringComparison.Ordinal)
                || string.Equals(FieldAttributes.GetOnState(controls[i].Dictionary, form.Document.Objects.Names), state, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Switches off every button of a field that is on.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "widget">One widget of the field.</param>
    /// <param name = "controls">The field's buttons.</param>
    /// <returns><see langword="true"/> when all are off.</returns>
    internal static bool SwitchAllOff(PdfForm form, ResolvedWidget widget, List<FormControl> controls)
    {
        var done = true;
        for (var i = 0; i < controls.Count; i++)
        {
            if (controls[i].IsChecked(form.Document.Objects.Names))
            {
                done &= PdfFormEditing.ApplyCheck(form, widget, controls, i, false);
            }
        }

        return done;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Writes the value, default value and options of a new field (ISO 32000-2 12.7.4.2 to 12.7.4.4).</summary>
internal static class FormFieldValues
{
    /// <summary>Writes the value entries of a field.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The field dictionary.</param>
    /// <param name="spec">The planned field.</param>
    internal static void Write(PdfObjectStore store, PdfDictionary target, FormFieldSpec spec)
    {
        switch (spec.Definition.Type)
        {
            case PdfFieldType.Text:
            {
                WriteText(store, target, spec);
                break;
            }

            case PdfFieldType.CheckBox or PdfFieldType.RadioButton:
            {
                WriteButtons(store, target, spec);
                break;
            }

            case PdfFieldType.ComboBox or PdfFieldType.ListBox:
            {
                WriteChoices(store, target, spec);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Works out a button's on-state name and whether it is on.</summary>
    /// <param name="spec">The planned field.</param>
    /// <param name="position">The widget's position in the field.</param>
    /// <returns>The state; the name is <see langword="null"/> for a field that is not a button.</returns>
    internal static FormButtonOnState GetOnState(FormFieldSpec spec, int position)
    {
        if (spec.Buttons is not { } buttons)
        {
            return new(null, false);
        }

        var isCheckBox = spec.Definition.Type == PdfFieldType.CheckBox;
        var name = buttons.States[isCheckBox ? 0 : position];
        var isOn = isCheckBox
            ? spec.Definition.IsChecked
            : spec.Definition.Value is { } value && string.Equals(buttons.Exports[position], value, StringComparison.Ordinal);
        return new(name, isOn);
    }

    /// <summary>Gets the state name a button group's value takes for a given choice.</summary>
    /// <param name="spec">The planned field.</param>
    /// <param name="exportValue">The export value chosen; <see langword="null"/> or unknown for none.</param>
    /// <returns>The state name; <c>Off</c> when no button has the value.</returns>
    internal static string GetStateFor(FormFieldSpec spec, string? exportValue)
    {
        var buttons = spec.Buttons!;
        var at = exportValue is null ? -1 : Array.IndexOf(buttons.Exports, exportValue);
        return at < 0 ? FormValues.OffState : buttons.States[at];
    }

    /// <summary>Gets the values a choice field starts with.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>The selected values; empty for none.</returns>
    internal static string[] GetSelected(PdfFieldDefinition definition)
    {
        if (definition.Values is { Length: > 0 })
        {
            return definition.Values;
        }

        return definition.Value is null ? [] : [definition.Value];
    }

    /// <summary>Builds the <c>/Opt</c> array: a string for an option that shows its value, a pair for one with its own label.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="options">The options in order.</param>
    /// <returns>The array.</returns>
    internal static PdfArray BuildOptions(PdfObjectStore store, PdfFieldOption[] options)
    {
        var array = new PdfArray(store, options.Length);
        foreach (var option in options)
        {
            if (option.Label.Length == 0 || string.Equals(option.Label, option.Value, StringComparison.Ordinal))
            {
                array.Add(FormFieldBuilder.Text(store, option.Value));
                continue;
            }

            array.Add(PdfValue.FromArray(new(store, [FormFieldBuilder.Text(store, option.Value), FormFieldBuilder.Text(store, option.Label)])));
        }

        return array;
    }

    /// <summary>Finds where the selected values sit among the options.</summary>
    /// <param name="options">The options in order.</param>
    /// <param name="selected">The selected values.</param>
    /// <returns>The positions, ascending.</returns>
    internal static List<int> FindPositions(PdfFieldOption[] options, string[] selected)
    {
        var positions = new List<int>(selected.Length);
        for (var i = 0; i < options.Length; i++)
        {
            if (Array.IndexOf(selected, options[i].Value) >= 0)
            {
                positions.Add(i);
            }
        }

        return positions;
    }

    /// <summary>Writes the value of a text field and, for rich text, its default style and rich value.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The field dictionary.</param>
    /// <param name="spec">The planned field.</param>
    private static void WriteText(PdfObjectStore store, PdfDictionary target, FormFieldSpec spec)
    {
        var definition = spec.Definition;
        if (definition.Value is { Length: > 0 } value)
        {
            target.Set(KnownName.V, FormFieldBuilder.Text(store, value));
        }

        if (definition.DefaultValue is { Length: > 0 } fallback)
        {
            target.Set(KnownName.DV, FormFieldBuilder.Text(store, fallback));
        }

        if ((spec.Flags & PdfFieldFlags.RichTextOrRadiosInUnison) == 0)
        {
            return;
        }

        var size = DefaultAppearance.ReadFontSize(spec.DefaultAppearance);
        var points = size > 0 ? size : FormFieldFonts.ReadableSize;
        var style = string.Create(CultureInfo.InvariantCulture, $"font: Helvetica {points:0.####}pt; color: #000000");
        target.Set(KnownName.DS, FormFieldBuilder.Text(store, style));
        if (definition.Value is { Length: > 0 } rich)
        {
            target.Set(store.Names.Intern("RV"u8), FormFieldBuilder.Text(store, RichTextWriter.Write(rich, style)));
        }
    }

    /// <summary>Writes the value of a check box or radio group, and the export values when they cannot be state names.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The field dictionary.</param>
    /// <param name="spec">The planned field.</param>
    private static void WriteButtons(PdfObjectStore store, PdfDictionary target, FormFieldSpec spec)
    {
        var definition = spec.Definition;
        var buttons = spec.Buttons!;
        if (buttons.UsesOptions)
        {
            var options = new PdfArray(store, buttons.Exports.Length);
            foreach (var export in buttons.Exports)
            {
                options.Add(FormFieldBuilder.Text(store, export));
            }

            target.Set(KnownName.Opt, PdfValue.FromArray(options));
        }

        var chosen = definition.Value;
        if (definition.Type == PdfFieldType.CheckBox)
        {
            chosen = definition.IsChecked ? buttons.Exports[0] : null;
        }

        target.Set(KnownName.V, PdfValue.FromName(store.Names.Intern(GetStateFor(spec, chosen))));
        if (definition.DefaultValue is { } fallback)
        {
            target.Set(KnownName.DV, PdfValue.FromName(store.Names.Intern(GetStateFor(spec, fallback))));
        }
    }

    /// <summary>Writes the options, selected values and selected positions of a combo box or list box.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The field dictionary.</param>
    /// <param name="spec">The planned field.</param>
    private static void WriteChoices(PdfObjectStore store, PdfDictionary target, FormFieldSpec spec)
    {
        var definition = spec.Definition;
        target.Set(KnownName.Opt, PdfValue.FromArray(BuildOptions(store, spec.Options)));
        var selected = GetSelected(definition);
        if (selected.Length > 0)
        {
            var multiple = definition.Type == PdfFieldType.ListBox && (spec.Flags & PdfFieldFlags.MultiSelect) != 0;
            target.Set(KnownName.V, multiple ? PdfValue.FromArray(BuildStrings(store, selected)) : FormFieldBuilder.Text(store, selected[0]));
            var positions = FindPositions(spec.Options, selected);
            if (positions.Count > 0)
            {
                target.Set(KnownName.I, PdfValue.FromArray(BuildIntegers(store, positions)));
            }
        }

        if (definition.DefaultValue is { } fallback)
        {
            target.Set(KnownName.DV, FormFieldBuilder.Text(store, fallback));
        }
    }

    /// <summary>Builds an array of text strings.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="values">The texts.</param>
    /// <returns>The array.</returns>
    private static PdfArray BuildStrings(PdfObjectStore store, string[] values)
    {
        var array = new PdfArray(store, values.Length);
        foreach (var value in values)
        {
            array.Add(FormFieldBuilder.Text(store, value));
        }

        return array;
    }

    /// <summary>Builds an array of integers.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="values">The numbers.</param>
    /// <returns>The array.</returns>
    private static PdfArray BuildIntegers(PdfObjectStore store, List<int> values)
    {
        var array = new PdfArray(store, values.Count);
        foreach (var value in values)
        {
            array.Add(PdfValue.FromInteger(value));
        }

        return array;
    }
}

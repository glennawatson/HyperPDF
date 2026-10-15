// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads the attributes the form design edits from a field and its widgets.</summary>
internal static class FormFieldReader
{
    /// <summary>The width of a border that names none.</summary>
    private const float DefaultBorderWidth = 1;

    /// <summary>The position of the width in a <c>/Border</c> array.</summary>
    private const int BorderWidthSlot = 2;

    /// <summary>The number of parts of an RGB colour.</summary>
    private const int RgbParts = 3;

    /// <summary>The number of parts of a CMYK colour.</summary>
    private const int CmykParts = 4;

    /// <summary>The position of the third part of a colour: blue, or yellow.</summary>
    private const int BlueSlot = 2;

    /// <summary>The position of the black part of a CMYK colour.</summary>
    private const int BlackSlot = 3;

    /// <summary>Finds where every widget on the pages sits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The location of each widget, by object number.</returns>
    internal static Dictionary<int, PdfWidgetLocation> LocateWidgets(PdfDocument document, CancellationToken cancellationToken)
    {
        var locations = new Dictionary<int, PdfWidgetLocation>();
        for (var page = 0; page < document.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var annotations = PdfDocumentPages.GetPage(document, page).Dictionary.GetArray(KnownName.Annots);
            for (var i = 0; annotations is not null && i < annotations.Count; i++)
            {
                if (annotations.GetRaw(i).AsReference() is { IsValid: true } id)
                {
                    _ = locations.TryAdd(id.Number, new(page, i));
                }
            }
        }

        return locations;
    }

    /// <summary>Reads a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="node">The field.</param>
    /// <param name="locations">Where the widgets sit, by object number.</param>
    /// <returns>The details; <see langword="null"/> when the field is not one of the kinds the form design edits or shows no widget.</returns>
    internal static PdfFieldDetails? Read(PdfDocument document, FormFieldNode node, Dictionary<int, PdfWidgetLocation> locations)
    {
        var field = node.Dictionary;
        var names = document.Objects.Names;
        var flags = FieldAttributes.GetFlags(field);
        var type = FormValues.GetType(field, names, flags);
        if (type == PdfFieldType.Unknown || !node.IsTerminal)
        {
            return null;
        }

        var form = document.Catalog.GetDictionary(KnownName.AcroForm);
        using var controls = FormControls.Read(field, node.Id);
        var widgets = ReadWidgets(document, node, controls.AsSpan(), locations);
        var values = type is PdfFieldType.ComboBox or PdfFieldType.ListBox ? ReadSelected(field) : [];
        var details = new PdfFieldDetails(
            node.Name,
            type,
            flags,
            FieldAttributes.ReadText(FieldAttributes.Find(field, KnownName.TU)),
            ReadValue(type, field, widgets, values),
            values,
            ReadDefault(type, field),
            Math.Max(0, FieldAttributes.Find(field, KnownName.MaxLen).AsInt32()),
            ReadQuadding(field, form),
            ReadAppearance(field, form),
            ReadOptions(field),
            widgets);
        return ReadWidgetLook(details, controls.Count > 0 ? controls[0].Dictionary : field, field);
    }

    /// <summary>Reads the widgets of a field.</summary>
    /// <param name="document">The document.</param>
    /// <param name="node">The field.</param>
    /// <param name="controls">The field's widget dictionaries.</param>
    /// <param name="locations">Where the widgets sit, by object number.</param>
    /// <returns>The widgets.</returns>
    private static PdfWidgetDetails[] ReadWidgets(
        PdfDocument document,
        FormFieldNode node,
        ReadOnlySpan<FormControl> controls,
        Dictionary<int, PdfWidgetLocation> locations)
    {
        var form = PdfDocumentForms.GetForm(document);
        var widgets = new PdfWidgetDetails[controls.Length];
        var type = FormValues.GetType(node.Dictionary, document.Objects.Names, FieldAttributes.GetFlags(node.Dictionary));
        var isButton = type is PdfFieldType.CheckBox or PdfFieldType.RadioButton;
        for (var i = 0; i < widgets.Length; i++)
        {
            var control = controls[i];
            var location = control.Id.IsValid && locations.TryGetValue(control.Id.Number, out var found) ? found : new(-1, -1);
            var bounds = control.Dictionary.TryGetRectangle(KnownName.Rect, out var rect) ? rect : default;
            var export = isButton ? PdfFormReading.GetExportValue(form, node.Dictionary, controls[i].Dictionary, i) : null;
            widgets[i] = new(location, bounds, export, isButton && control.IsChecked(document.Objects.Names));
        }

        return widgets;
    }

    /// <summary>Reads a field's value.</summary>
    /// <param name="type">The kind of field.</param>
    /// <param name="field">The field dictionary.</param>
    /// <param name="widgets">The field's widgets.</param>
    /// <param name="selected">The selected values of a choice field.</param>
    /// <returns>The value; <see langword="null"/> for none.</returns>
    private static string? ReadValue(PdfFieldType type, PdfDictionary field, PdfWidgetDetails[] widgets, string[] selected)
    {
        switch (type)
        {
            case PdfFieldType.CheckBox or PdfFieldType.RadioButton:
            {
                return Array.Find(widgets, static w => w.IsChecked)?.ExportValue;
            }

            case PdfFieldType.ComboBox or PdfFieldType.ListBox:
            {
                return selected.Length > 0 ? selected[0] : null;
            }

            case PdfFieldType.Text:
            {
                var text = FormValues.ReadText(field, type);
                return text.Length == 0 ? null : text;
            }

            default:
            {
                return null;
            }
        }
    }

    /// <summary>Reads the default value of a field.</summary>
    /// <param name="type">The kind of field.</param>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The text; <see langword="null"/> for none, and for buttons, whose default is a state.</returns>
    private static string? ReadDefault(PdfFieldType type, PdfDictionary field)
    {
        if (type is PdfFieldType.CheckBox or PdfFieldType.RadioButton or PdfFieldType.PushButton or PdfFieldType.Signature)
        {
            return null;
        }

        var value = FieldAttributes.Find(field, KnownName.DV);
        var array = value.AsArray();
        var text = array is { Count: > 0 } ? FieldAttributes.ReadText(array.Get(0)) : FieldAttributes.ReadText(value);
        return text.Length == 0 ? null : text;
    }

    /// <summary>Reads the export values of the options a choice field has selected.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The values; empty for none.</returns>
    private static string[] ReadSelected(PdfDictionary field)
    {
        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.AsArray() is { } array)
        {
            var list = new string[array.Count];
            for (var i = 0; i < list.Length; i++)
            {
                list[i] = FieldAttributes.ReadText(array.Get(i));
            }

            return list;
        }

        var single = FieldAttributes.ReadText(value);
        return single.Length == 0 ? [] : [single];
    }

    /// <summary>Reads the text alignment.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="form">The form dictionary, or <see langword="null"/>.</param>
    /// <returns>The alignment.</returns>
    private static int ReadQuadding(PdfDictionary field, PdfDictionary? form)
    {
        var inherited = FieldAttributes.Find(field, KnownName.Q);
        return inherited.IsNull ? form?.GetInt32(KnownName.Q) ?? 0 : inherited.AsInt32();
    }

    /// <summary>Reads the default appearance string.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="form">The form dictionary, or <see langword="null"/>.</param>
    /// <returns>The string; empty when neither the field nor the form has one.</returns>
    private static string ReadAppearance(PdfDictionary field, PdfDictionary? form)
    {
        var inherited = FieldAttributes.Find(field, KnownName.DA);
        if (inherited.IsNull)
        {
            return form is null ? string.Empty : Encoding.Latin1.GetString(form.GetStringBytes(KnownName.DA));
        }

        return Encoding.Latin1.GetString(inherited.AsStringBytes());
    }

    /// <summary>Reads the options of a choice field.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The options; empty for none.</returns>
    private static PdfFieldOption[] ReadOptions(PdfDictionary field)
    {
        var count = FormChoices.Count(field);
        if (count == 0 || FieldAttributes.Find(field, KnownName.Opt).AsArray() is null)
        {
            return [];
        }

        var options = new PdfFieldOption[count];
        for (var i = 0; i < options.Length; i++)
        {
            var value = FormChoices.GetValue(field, i);
            var label = FormChoices.GetLabel(field, i);
            options[i] = new(value, string.Equals(value, label, StringComparison.Ordinal) ? string.Empty : label);
        }

        return options;
    }

    /// <summary>Adds the look and actions of the first widget to a field's details.</summary>
    /// <param name="details">The details so far.</param>
    /// <param name="widget">The first widget dictionary.</param>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The details with the look.</returns>
    private static PdfFieldDetails ReadWidgetLook(PdfFieldDetails details, PdfDictionary widget, PdfDictionary field)
    {
        var characteristics = widget.GetDictionary(KnownName.MK);
        var style = widget.GetDictionary(KnownName.BS);
        var width = style is not null && style.ContainsKey(KnownName.W) ? style.GetSingle(KnownName.W) : ReadBorderWidth(widget);
        var captionBytes = characteristics is null ? default : characteristics.GetStringBytes(KnownName.CA);
        var caption = captionBytes.IsEmpty ? null : PdfText.Decode(captionBytes);
        return details with
        {
            BorderColor = ReadColor(characteristics?.GetArray(KnownName.BC)),
            BackgroundColor = ReadColor(characteristics?.GetArray(KnownName.BG)),
            BorderWidth = width,
            BorderStyle = ReadStyle(style),
            Caption = caption,
            Actions = ReadActions(widget, field),
        };
    }

    /// <summary>Reads the width from a <c>/Border</c> array.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <returns>The width; 1 when the widget gives none.</returns>
    private static float ReadBorderWidth(PdfDictionary widget) =>
        widget.GetArray(KnownName.Border) is { } border && border.Count > BorderWidthSlot ? border.GetSingle(BorderWidthSlot) : DefaultBorderWidth;

    /// <summary>Reads a border style dictionary's line style.</summary>
    /// <param name="style">The <c>/BS</c> dictionary, or <see langword="null"/>.</param>
    /// <returns>The style; solid when missing.</returns>
    private static PdfFieldBorderStyle ReadStyle(PdfDictionary? style)
    {
        var name = style?.GetName(KnownName.S) ?? default;
        if (name.Is(KnownName.D))
        {
            return PdfFieldBorderStyle.Dashed;
        }

        if (name.Is(KnownName.B))
        {
            return PdfFieldBorderStyle.Beveled;
        }

        if (name.Is(KnownName.I))
        {
            return PdfFieldBorderStyle.Inset;
        }

        return name.Is(KnownName.U) ? PdfFieldBorderStyle.Underline : PdfFieldBorderStyle.Solid;
    }

    /// <summary>Reads a colour array of one, three or four numbers, or an empty one for no colour.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The colour; <see langword="null"/> when the entry is missing.</returns>
    private static PdfFieldColor? ReadColor(PdfArray? array)
    {
        if (array is null)
        {
            return null;
        }

        switch (array.Count)
        {
            case 1:
            {
                var grey = array.GetSingle(0);
                return PdfFieldColor.FromRgb(grey, grey, grey);
            }

            case RgbParts:
            {
                return PdfFieldColor.FromRgb(array.GetSingle(0), array.GetSingle(1), array.GetSingle(BlueSlot));
            }

            case CmykParts:
            {
                var black = 1 - array.GetSingle(BlackSlot);
                return PdfFieldColor.FromRgb((1 - array.GetSingle(0)) * black, (1 - array.GetSingle(1)) * black, (1 - array.GetSingle(BlueSlot)) * black);
            }

            default:
            {
                return PdfFieldColor.Transparent;
            }
        }
    }

    /// <summary>Reads the scripts of a field's additional actions and the action of its widget.</summary>
    /// <param name="widget">The first widget dictionary.</param>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The actions; <see langword="null"/> when there are none.</returns>
    private static PdfFieldActions? ReadActions(PdfDictionary widget, PdfDictionary field)
    {
        var triggers = FieldAttributes.Find(field, KnownName.AA).AsDictionary() ?? widget.GetDictionary(KnownName.AA);
        var action = widget.GetDictionary(KnownName.A);
        var uri = action is not null && action.IsName(KnownName.S, KnownName.URI) ? Encoding.Latin1.GetString(action.GetStringBytes(KnownName.URI)) : null;
        var script = action is not null && action.IsName(KnownName.S, KnownName.JavaScript) ? FieldAttributes.ReadText(action.Get(KnownName.JS)) : null;
        var actions = new PdfFieldActions
        {
            Keystroke = ReadTrigger(triggers, KnownName.K),
            Format = ReadTrigger(triggers, KnownName.F),
            Validate = ReadTrigger(triggers, KnownName.V),
            Calculate = ReadTrigger(triggers, KnownName.C),
            Activate = NullIfEmpty(script),
            ActivateUri = NullIfEmpty(uri),
        };
        return actions.IsEmpty ? null : actions;
    }

    /// <summary>Reads the script of one additional action.</summary>
    /// <param name="triggers">The additional-actions dictionary, or <see langword="null"/>.</param>
    /// <param name="key">The trigger key.</param>
    /// <returns>The script; <see langword="null"/> when there is none.</returns>
    private static string? ReadTrigger(PdfDictionary? triggers, KnownName key) =>
        triggers is null ? null : NullIfEmpty(PdfFormReading.ReadScript(triggers, key));

    /// <summary>Turns an empty string into null.</summary>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    /// <returns>The text; <see langword="null"/> when it is null or empty.</returns>
    private static string? NullIfEmpty(string? text) => string.IsNullOrEmpty(text) ? null : text;
}

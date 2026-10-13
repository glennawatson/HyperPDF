// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Joins what a field has with the changes asked for, checks the result with the rules used to create a field, and plans which
/// parts of the field to rewrite.
/// </summary>
internal static class FormFieldMerge
{
    /// <summary>The width of a rectangle used to check a widget that has none.</summary>
    private const float FallbackSize = 1;

    /// <summary>The text that starts a parameter name in an argument exception's message.</summary>
    private const string ParameterMarker = " (Parameter";

    /// <summary>Plans a change.</summary>
    /// <param name="document">The document.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes to make.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">The changes cannot be made; the message is a plain reason.</exception>
    internal static FormFieldChange Plan(PdfDocument document, PdfFieldDetails details, PdfFieldChanges changes)
    {
        var parts = GetParts(details.Type, changes);
        var flags = MergeFlags(details, changes);
        var definition = Build(details, changes, flags);
        Validate(document, details, changes, definition);
        var spec = FormFieldSpec.Create(definition) with { Flags = flags };
        return new(spec, parts, flags);
    }

    /// <summary>Works out which parts of the field a change rewrites.</summary>
    /// <param name="type">The kind of field.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The parts.</returns>
    internal static FormFieldParts GetParts(PdfFieldType type, PdfFieldChanges changes)
    {
        var parts = GetFieldParts(changes) | GetLookParts(changes);
        if (type is PdfFieldType.CheckBox or PdfFieldType.RadioButton)
        {
            // The on-states, /V and /Opt are planned together, so a button always rewrites them.
            parts |= FormFieldParts.Value;
        }

        return parts;
    }

    /// <summary>Gets the parts of the field dictionary that change.</summary>
    /// <param name="changes">The changes.</param>
    /// <returns>The parts.</returns>
    private static FormFieldParts GetFieldParts(PdfFieldChanges changes) =>
        Part(changes.Tooltip is not null, FormFieldParts.Tooltip)
        | Part((changes.AddFlags | changes.RemoveFlags) != PdfFieldFlags.None, FormFieldParts.Flags)
        | Part(IsValueChange(changes), FormFieldParts.Value)
        | Part(changes.DefaultValue is not null, FormFieldParts.Default)
        | Part(changes.MaxLength is not null, FormFieldParts.MaxLength)
        | Part(changes.Quadding is not null, FormFieldParts.Quadding)
        | Part(changes.DefaultAppearance is not null, FormFieldParts.Appearance)
        | Part(changes.Options is not null, FormFieldParts.Options)
        | Part(changes.Actions is not null, FormFieldParts.Actions);

    /// <summary>Gets the parts of the widgets that change.</summary>
    /// <param name="changes">The changes.</param>
    /// <returns>The parts.</returns>
    private static FormFieldParts GetLookParts(PdfFieldChanges changes) =>
        Part(changes.BorderColor is not null, FormFieldParts.BorderColor)
        | Part(changes.BackgroundColor is not null, FormFieldParts.Background)
        | Part(changes.BorderWidth is not null || changes.BorderStyle is not null, FormFieldParts.Border)
        | Part(changes.Caption is not null, FormFieldParts.Caption);

    /// <summary>Picks a part when a change touches it.</summary>
    /// <param name="touched">Whether the change touches the part.</param>
    /// <param name="part">The part.</param>
    /// <returns>The part, or none.</returns>
    private static FormFieldParts Part(bool touched, FormFieldParts part) => touched ? part : FormFieldParts.None;

    /// <summary>Determines whether a change sets the value, the selection, the check state or the export values.</summary>
    /// <param name="changes">The changes.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsValueChange(PdfFieldChanges changes) =>
        changes.Value is not null || changes.Values is not null || changes.IsChecked is not null || changes.ExportValues is not null;

    /// <summary>Checks the field after the change with the rules used to create a field, and the rules of a PDF/A claim.</summary>
    /// <param name="document">The document.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="definition">The field after the change.</param>
    /// <exception cref="ArgumentException">The change breaks a rule; the message is a plain reason.</exception>
    private static void Validate(PdfDocument document, PdfFieldDetails details, PdfFieldChanges changes, PdfFieldDefinition definition)
    {
        try
        {
            FormFieldArguments.Check(document, definition);
            CheckConformance(document, details, changes, definition);
        }
        catch (ArgumentException exception)
        {
            var at = exception.Message.IndexOf(ParameterMarker, StringComparison.Ordinal);
            throw new ArgumentException(at < 0 ? exception.Message : exception.Message[..at]);
        }
    }

    /// <summary>Applies the flag changes.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The flags the field will have, with the bits that decide its kind kept.</returns>
    /// <exception cref="ArgumentException">A flag does not apply to the field, or decides its kind.</exception>
    private static PdfFieldFlags MergeFlags(PdfFieldDetails details, PdfFieldChanges changes)
    {
        if (((changes.AddFlags | changes.RemoveFlags) & FormFieldFlags.TypeBits) != 0)
        {
            throw new ArgumentException("The flags that decide a field's kind cannot be changed. Create a new field of the kind you need.");
        }

        var allowed = FormFieldFlags.GetAllowed(details.Type);
        if ((changes.AddFlags & ~allowed) != 0)
        {
            throw new ArgumentException($"The flags {changes.AddFlags & ~allowed} do not apply to a {details.Type} field.");
        }

        return (details.Flags | changes.AddFlags) & ~changes.RemoveFlags;
    }

    /// <summary>Builds the definition of the field after the change, for checking and for writing.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="flags">The flags after the change.</param>
    /// <returns>The definition.</returns>
    private static PdfFieldDefinition Build(PdfFieldDetails details, PdfFieldChanges changes, PdfFieldFlags flags)
    {
        var exports = GetExports(details, changes);
        var definition = new PdfFieldDefinition(details.Type, details.Name, GetPlacements(details, exports))
        {
            Tooltip = GetTooltip(details, changes),
            Flags = flags & FormFieldFlags.GetAllowed(details.Type),
            MaxLength = changes.MaxLength ?? details.MaxLength,
            Quadding = changes.Quadding ?? details.Quadding,
            DefaultAppearance = changes.DefaultAppearance ?? (details.DefaultAppearance.Length > 0 ? details.DefaultAppearance : null),
            Options = changes.Options ?? details.Options,
            DefaultValue = GetDefault(details, changes),
            IsChecked = changes.IsChecked ?? details.IsChecked,
            ExportValue = details.Type == PdfFieldType.CheckBox ? exports[0] : null,
        };
        definition = SetBorder(SetLook(definition, details, changes), details, changes);
        var choice = details.Type is PdfFieldType.ComboBox or PdfFieldType.ListBox;
        return choice ? SetSelection(definition, details, changes) : SetValue(definition, details, changes);
    }

    /// <summary>Sets the caption, colours and actions.</summary>
    /// <param name="definition">The definition so far.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The definition.</returns>
    private static PdfFieldDefinition SetLook(PdfFieldDefinition definition, PdfFieldDetails details, PdfFieldChanges changes) => definition with
    {
        Caption = changes.Caption ?? details.Caption,
        BorderColor = changes.BorderColor ?? details.BorderColor,
        BackgroundColor = changes.BackgroundColor ?? details.BackgroundColor,
        Actions = changes.Actions ?? details.Actions,
    };

    /// <summary>Gets the tooltip after the change.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The tooltip; <see langword="null"/> falls back to the field name when written.</returns>
    private static string? GetTooltip(PdfFieldDetails details, PdfFieldChanges changes)
    {
        var tooltip = changes.Tooltip ?? details.Tooltip;
        return tooltip.Length == 0 ? null : tooltip;
    }

    /// <summary>Gets the export value of each widget after the change.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The export values; one per widget.</returns>
    /// <exception cref="ArgumentException">The change names the wrong number of export values.</exception>
    private static string?[] GetExports(PdfFieldDetails details, PdfFieldChanges changes)
    {
        var widgets = details.Widgets;
        if (changes.ExportValues is { } given && (given.Length != widgets.Length || details.Type is not (PdfFieldType.CheckBox or PdfFieldType.RadioButton)))
        {
            throw new ArgumentException("Export values go with the buttons of a check box or a radio group: give one for each button.");
        }

        var exports = new string?[widgets.Length];
        for (var i = 0; i < exports.Length; i++)
        {
            exports[i] = changes.ExportValues is { } list ? list[i] : widgets[i].ExportValue;
        }

        return exports;
    }

    /// <summary>Makes the widget placements the checks need.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="exports">The export values after the change.</param>
    /// <returns>The placements.</returns>
    private static PdfWidgetPlacement[] GetPlacements(PdfFieldDetails details, string?[] exports)
    {
        var placements = new PdfWidgetPlacement[details.Widgets.Length];
        var radio = details.Type == PdfFieldType.RadioButton;
        for (var i = 0; i < placements.Length; i++)
        {
            var widget = details.Widgets[i];
            var bounds = widget.Bounds.IsEmpty ? new PdfRectangle(0, 0, FallbackSize, FallbackSize) : widget.Bounds;
            placements[i] = new(Math.Max(0, widget.Location.PageIndex), bounds) { ExportValue = radio ? exports[i] : null };
        }

        return placements;
    }

    /// <summary>Gets the default value after the change.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The default; <see langword="null"/> for none, and for buttons, whose default is a state.</returns>
    private static string? GetDefault(PdfFieldDetails details, PdfFieldChanges changes)
    {
        var keepsState = details.Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton or PdfFieldType.PushButton or PdfFieldType.Signature;
        var text = changes.DefaultValue ?? (keepsState ? null : details.DefaultValue);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Sets the border width and style, which are written together.</summary>
    /// <param name="definition">The definition so far.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The definition.</returns>
    private static PdfFieldDefinition SetBorder(PdfFieldDefinition definition, PdfFieldDetails details, PdfFieldChanges changes) =>
        changes.BorderWidth is null && changes.BorderStyle is null
            ? definition
            : definition with { BorderWidth = changes.BorderWidth ?? details.BorderWidth, BorderStyle = changes.BorderStyle ?? details.BorderStyle };

    /// <summary>Sets the value of a text, button or other field.</summary>
    /// <param name="definition">The definition so far.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The definition.</returns>
    private static PdfFieldDefinition SetValue(PdfFieldDefinition definition, PdfFieldDetails details, PdfFieldChanges changes)
    {
        var value = changes.Value ?? details.Value;
        return definition with { Value = string.IsNullOrEmpty(value) ? null : value };
    }

    /// <summary>Sets the selection of a choice field, dropping what the options no longer hold.</summary>
    /// <param name="definition">The definition so far.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The definition.</returns>
    private static PdfFieldDefinition SetSelection(PdfFieldDefinition definition, PdfFieldDetails details, PdfFieldChanges changes)
    {
        var chosen = GetChosen(details, changes);
        var editable = details.Type == PdfFieldType.ComboBox && (definition.Flags & PdfFieldFlags.Edit) != 0;
        if (changes.Options is { } options && !editable)
        {
            chosen = KeepOptions(chosen, options);
        }

        return definition with { Values = chosen };
    }

    /// <summary>Gets the values selected after the change.</summary>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>The values; an empty text in <see cref="PdfFieldChanges.Value"/> clears the selection.</returns>
    private static string[] GetChosen(PdfFieldDetails details, PdfFieldChanges changes)
    {
        if (changes.Values is { } many)
        {
            return many;
        }

        if (changes.Value is null)
        {
            return details.Values;
        }

        return changes.Value.Length == 0 ? [] : [changes.Value];
    }

    /// <summary>Keeps the selected values that are still options.</summary>
    /// <param name="selected">The selected values.</param>
    /// <param name="options">The options.</param>
    /// <returns>The values that are options.</returns>
    private static string[] KeepOptions(string[] selected, PdfFieldOption[] options)
    {
        var kept = new List<string>(selected.Length);
        foreach (var value in selected)
        {
            if (HasOption(options, value))
            {
                kept.Add(value);
            }
        }

        return [.. kept];
    }

    /// <summary>Determines whether an option has a value.</summary>
    /// <param name="options">The options.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when an option has the value.</returns>
    private static bool HasOption(PdfFieldOption[] options, string value)
    {
        foreach (var option in options)
        {
            if (string.Equals(option.Value, value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks the rules a PDF/A claim adds to the parts the change touches.</summary>
    /// <param name="document">The document.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="definition">The definition after the change.</param>
    private static void CheckConformance(PdfDocument document, PdfFieldDetails details, PdfFieldChanges changes, PdfFieldDefinition definition)
    {
        if (changes.Actions is null && changes.DefaultAppearance is null)
        {
            return;
        }

        FormFieldValidation.CheckConformance(document, definition with { Type = details.Type }, PdfStampConformance.GetPdfAPart(document));
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Forms;

/// <summary>Checks the arguments of a field definition that do not depend on the other fields in the form.</summary>
internal static class FormFieldArguments
{
    /// <summary>The largest <c>/Q</c> value: right aligned.</summary>
    private const int MaxQuadding = 2;

    /// <summary>The value that shows the buttons of a group are all off.</summary>
    private const string Unchecked = "Off";

    /// <summary>Gets the export value of each button of a radio group, naming the buttons that name none.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>One export value per widget.</returns>
    internal static string[] GetRadioExports(PdfFieldDefinition definition)
    {
        var exports = new string[definition.Widgets.Length];
        for (var i = 0; i < exports.Length; i++)
        {
            exports[i] = definition.Widgets[i].ExportValue ?? FormButtonStates.DefaultExport(i);
        }

        return exports;
    }

    /// <summary>Checks everything about a definition that does not depend on the other fields.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">The definition is not valid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    internal static void Check(PdfDocument document, PdfFieldDefinition definition)
    {
        if (FormFieldNames.Validate(definition.Name) is { } reason)
        {
            throw new ArgumentException(reason, nameof(definition));
        }

        if (definition.Type is PdfFieldType.Unknown || !Enum.IsDefined(definition.Type))
        {
            throw new ArgumentException("The field type is not one the library can create.", nameof(definition));
        }

        CheckWidgets(document, definition);
        CheckFlags(definition);
        CheckLimits(definition);
        CheckValues(definition);
        CheckColors(definition);
        if (definition.DefaultAppearance is { Length: > 0 } appearance)
        {
            FormFieldFonts.RequireFont(document, appearance);
        }
    }

    /// <summary>Checks the widgets of a definition.</summary>
    /// <param name="document">The document.</param>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">A widget has no usable rectangle, or an export value is given for a field that takes none per widget.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A widget's page does not exist.</exception>
    private static void CheckWidgets(PdfDocument document, PdfFieldDefinition definition)
    {
        if (definition.Widgets is not { Length: > 0 })
        {
            throw new ArgumentException("A field needs at least one widget to show it.", nameof(definition));
        }

        var perWidgetExports = false;
        foreach (var widget in definition.Widgets)
        {
            ArgumentNullException.ThrowIfNull(widget);
            ArgumentOutOfRangeException.ThrowIfNegative(widget.PageIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(widget.PageIndex, document.PageCount);
            if (widget.Bounds.IsEmpty || !float.IsFinite(widget.Bounds.Left + widget.Bounds.Bottom + widget.Bounds.Right + widget.Bounds.Top))
            {
                throw new ArgumentException("A widget needs a rectangle with a width and a height.", nameof(definition));
            }

            perWidgetExports |= widget.ExportValue is not null;
        }

        if (perWidgetExports && definition.Type != PdfFieldType.RadioButton)
        {
            throw new ArgumentException("Only the buttons of a radio group take their own export value. A check box has one export value for the field.", nameof(definition));
        }
    }

    /// <summary>Checks the flags against the field's kind.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">A flag does not apply to the kind of field, or flags contradict each other.</exception>
    private static void CheckFlags(PdfFieldDefinition definition)
    {
        var allowed = FormFieldFlags.GetAllowed(definition.Type);
        if ((definition.Flags & ~allowed) != 0)
        {
            throw new ArgumentException($"The flags {definition.Flags & ~allowed} do not apply to a {definition.Type} field.", nameof(definition));
        }

        if (definition.Type == PdfFieldType.Text)
        {
            CheckTextFlags(definition);
        }
    }

    /// <summary>Checks the flags of a text field against each other.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">Flags contradict each other.</exception>
    private static void CheckTextFlags(PdfFieldDefinition definition)
    {
        var flags = definition.Flags;
        if ((flags & PdfFieldFlags.Comb) != 0 && (definition.MaxLength <= 0 || (flags & (PdfFieldFlags.Multiline | PdfFieldFlags.Password | PdfFieldFlags.FileSelect)) != 0))
        {
            throw new ArgumentException("A comb field needs a maximum length and cannot be multiline, a password or a file selection.", nameof(definition));
        }

        var rich = (flags & PdfFieldFlags.RichTextOrRadiosInUnison) != 0;
        if ((flags & PdfFieldFlags.Password) != 0 && (rich || definition.Value is { Length: > 0 }))
        {
            throw new ArgumentException("A password field is not rich text and does not keep a value in the file.", nameof(definition));
        }
    }

    /// <summary>Checks the alignment and the length limit.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">The limit does not suit the field or the value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A number is out of range.</exception>
    private static void CheckLimits(PdfFieldDefinition definition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(definition.MaxLength);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.Quadding);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(definition.Quadding, MaxQuadding);
        if (definition.MaxLength > 0 && definition.Type != PdfFieldType.Text)
        {
            throw new ArgumentException("Only a text field has a maximum length.", nameof(definition));
        }

        if (definition.Value is { } value && definition.MaxLength > 0 && value.Length > definition.MaxLength)
        {
            throw new ArgumentException("The value is longer than the maximum length.", nameof(definition));
        }
    }

    /// <summary>Checks the options and values of a field.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">The options, values or export values do not suit the field.</exception>
    private static void CheckValues(PdfFieldDefinition definition)
    {
        var choice = definition.Type is PdfFieldType.ComboBox or PdfFieldType.ListBox;
        if (choice)
        {
            CheckChoices(definition);
        }
        else if (definition.Options is { Length: > 0 })
        {
            throw new ArgumentException("Only a combo box or list box has options.", nameof(definition));
        }

        if (definition.Type == PdfFieldType.RadioButton)
        {
            CheckRadio(definition);
        }

        CheckOtherValues(definition);
    }

    /// <summary>Checks the values that only some kinds of field take.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">A value is given to a field that cannot hold it.</exception>
    private static void CheckOtherValues(PdfFieldDefinition definition)
    {
        var holdsNoValue = definition.Type is PdfFieldType.Signature or PdfFieldType.PushButton;
        if (holdsNoValue && (definition.Value is not null || definition.DefaultValue is not null))
        {
            throw new ArgumentException("A push button or signature field holds no value.", nameof(definition));
        }

        if (definition.ExportValue is not null && (definition.Type != PdfFieldType.CheckBox || definition.ExportValue.Length == 0))
        {
            throw new ArgumentException("Only a check box takes one export value for the field, and it cannot be empty.", nameof(definition));
        }

        if (definition.LockedFields is { Length: > 0 } && definition.Type != PdfFieldType.Signature)
        {
            throw new ArgumentException("Only a signature field locks other fields.", nameof(definition));
        }
    }

    /// <summary>Checks the options and the selected values of a combo box or list box.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">The options or the selection are not valid.</exception>
    private static void CheckChoices(PdfFieldDefinition definition)
    {
        if (definition.Options is not { Length: > 0 })
        {
            throw new ArgumentException("A combo box or list box needs at least one option.", nameof(definition));
        }

        var values = new HashSet<string>(definition.Options.Length, StringComparer.Ordinal);
        foreach (var option in definition.Options)
        {
            if (option.Value is null || !values.Add(option.Value))
            {
                throw new ArgumentException("Every option needs its own value.", nameof(definition));
            }
        }

        CheckSelection(definition, values);
    }

    /// <summary>Checks the values a choice field starts with against its options.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="values">The options' values.</param>
    /// <exception cref="ArgumentException">More values are selected than the field takes, or a value is not an option.</exception>
    private static void CheckSelection(PdfFieldDefinition definition, HashSet<string> values)
    {
        var selected = FormFieldValues.GetSelected(definition);
        var multiple = definition.Type == PdfFieldType.ListBox && (definition.Flags & PdfFieldFlags.MultiSelect) != 0;
        if (selected.Length > 1 && !multiple)
        {
            throw new ArgumentException("Only a list box that allows several selections can start with more than one.", nameof(definition));
        }

        var editable = definition.Type == PdfFieldType.ComboBox && (definition.Flags & PdfFieldFlags.Edit) != 0;
        foreach (var value in selected)
        {
            if (!editable && !values.Contains(value))
            {
                throw new ArgumentException($"The value '{value}' is not one of the options.", nameof(definition));
            }
        }
    }

    /// <summary>Checks the export values and the selection of a radio group.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">Export values clash or are not usable, or the selected value matches no button.</exception>
    private static void CheckRadio(PdfFieldDefinition definition)
    {
        var exports = GetRadioExports(definition);
        var unison = (definition.Flags & PdfFieldFlags.RichTextOrRadiosInUnison) != 0;
        var distinct = new HashSet<string>(exports, StringComparer.Ordinal);
        if (distinct.Count != exports.Length && !unison)
        {
            throw new ArgumentException("Two buttons of a radio group share an export value. Give each its own, or set the flag that switches such buttons together.", nameof(definition));
        }

        if (distinct.Contains(string.Empty) || distinct.Contains(Unchecked))
        {
            throw new ArgumentException("A radio button's export value cannot be empty or Off.", nameof(definition));
        }

        if (definition.Value is { } value && !distinct.Contains(value))
        {
            throw new ArgumentException($"No button of the group has the export value '{value}'.", nameof(definition));
        }
    }

    /// <summary>Checks the colours and the border.</summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentException">A colour part is out of range, or the border is not valid.</exception>
    private static void CheckColors(PdfFieldDefinition definition)
    {
        CheckColor(definition.BorderColor);
        CheckColor(definition.BackgroundColor);
        if (definition.BorderWidth is { } width && (width < 0 || !float.IsFinite(width)))
        {
            throw new ArgumentException("A border width cannot be negative.", nameof(definition));
        }

        if (definition.BorderStyle is { } style && !Enum.IsDefined(style))
        {
            throw new ArgumentException("The border style is not one the library knows.", nameof(definition));
        }
    }

    /// <summary>Checks that the parts of a colour are from 0 to 1.</summary>
    /// <param name="color">The colour, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">A part is out of range.</exception>
    private static void CheckColor(PdfFieldColor? color)
    {
        if (color is { IsTransparent: false } c && (!IsUnit(c.Red) || !IsUnit(c.Green) || !IsUnit(c.Blue)))
        {
            throw new ArgumentException("A colour part is a number from 0 to 1.", nameof(color));
        }
    }

    /// <summary>Determines whether a number is from 0 to 1.</summary>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when it is in range.</returns>
    private static bool IsUnit(float value) => value is >= 0 and <= 1;
}

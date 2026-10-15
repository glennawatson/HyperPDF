// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Writes a planned change into a field and its widgets (ISO 32000-2 12.7.4 and 12.5.6.19). Only the entries of the parts that
/// change are replaced, so entries the library does not know stay as they were, and the appearances are drawn again.
/// </summary>
internal static class FormFieldChangeWriter
{
    /// <summary>The field entries each part owns.</summary>
    private static readonly KeyGroup[] FieldKeys =
    [
        new(FormFieldParts.Tooltip, [KnownName.TU]),
        new(FormFieldParts.Flags, [KnownName.Ff]),
        new(FormFieldParts.Value, [KnownName.V, KnownName.I]),
        new(FormFieldParts.Default, [KnownName.DV]),
        new(FormFieldParts.MaxLength, [KnownName.MaxLen]),
        new(FormFieldParts.Quadding, [KnownName.Q]),
        new(FormFieldParts.Appearance, [KnownName.DA]),
        new(FormFieldParts.Options, [KnownName.Opt, KnownName.I, KnownName.V]),
        new(FormFieldParts.Actions, [KnownName.AA]),
    ];

    /// <summary>The widget characteristics each look part owns.</summary>
    private static readonly KeyGroup[] LookKeys =
    [
        new(FormFieldParts.BorderColor, [KnownName.BC]),
        new(FormFieldParts.Background, [KnownName.BG]),
        new(FormFieldParts.Caption, [KnownName.CA]),
    ];

    /// <summary>Writes a change.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction receiving the changed objects.</param>
    /// <param name="node">The field.</param>
    /// <param name="details">The field as it was.</param>
    /// <param name="change">The planned change.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    internal static void Write(
        PdfDocument document,
        PdfEditTransaction transaction,
        FormFieldNode node,
        PdfFieldDetails details,
        FormFieldChange change,
        CancellationToken cancellationToken)
    {
        var store = document.Objects;
        var field = node.Dictionary.Clone();
        var built = new PdfDictionary(store);
        FormFieldBuilder.WriteField(store, built, change.Spec);
        CopyFieldEntries(store, field, built, change, details.Type);
        using var controls = FormControls.Read(node.Dictionary, node.Id);
        var characteristics = FormFieldBuilder.BuildCharacteristics(store, change.Spec.Definition);
        var border = FormFieldBuilder.BuildBorder(store, change.Spec.Definition);
        var activation = FormFieldActionData.BuildActivation(store, change.Spec.Definition.Actions);
        for (var i = 0; i < controls.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var merged = controls[i].Id == node.Id;
            var widget = merged ? field : controls[i].Dictionary.Clone();
            WriteLook(store, widget, change, new(characteristics, border, activation));
            var drawing = new WidgetDrawing(details.Type, change.Flags, field, Math.Max(0, details.Widgets[i].Location.PageIndex));
            DrawWidget(document, transaction, drawing, widget, FormFieldValues.GetOnState(change.Spec, i), cancellationToken);
            if (!merged)
            {
                transaction.Replace(controls[i].Id, PdfValue.FromDictionary(widget));
            }
        }

        transaction.Replace(node.Id, PdfValue.FromDictionary(field));
    }

    /// <summary>Moves or resizes a widget and draws it again.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction receiving the changed objects.</param>
    /// <param name="node">The field.</param>
    /// <param name="details">The field as it is.</param>
    /// <param name="position">The widget's position in the field.</param>
    /// <param name="bounds">The new rectangle.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    internal static void WriteBounds(
        PdfDocument document,
        PdfEditTransaction transaction,
        FormFieldNode node,
        PdfFieldDetails details,
        int position,
        PdfRectangle bounds,
        CancellationToken cancellationToken)
    {
        var store = document.Objects;
        using var controls = FormControls.Read(node.Dictionary, node.Id);
        var control = controls[position];
        var widget = control.Dictionary.Clone();
        widget.Set(KnownName.Rect, PdfValue.FromArray(bounds.ToArray(store)));
        var field = control.Id == node.Id ? widget : node.Dictionary;
        var drawing = new WidgetDrawing(details.Type, details.Flags, field, Math.Max(0, details.Widgets[position].Location.PageIndex));
        var isButton = details.Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton;
        var name = isButton ? FieldAttributes.GetOnState(control.Dictionary, store.Names) : null;
        var on = new FormButtonOnState(name, isButton && control.IsChecked(store.Names));
        DrawWidget(document, transaction, drawing, widget, on, cancellationToken);
        transaction.Replace(control.Id, PdfValue.FromDictionary(widget));
    }

    /// <summary>Replaces the entries of the changed parts in the field.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="field">The field copy being changed.</param>
    /// <param name="built">A dictionary holding the entries the field has after the change.</param>
    /// <param name="change">The planned change.</param>
    /// <param name="type">The kind of field.</param>
    private static void CopyFieldEntries(PdfObjectStore store, PdfDictionary field, PdfDictionary built, FormFieldChange change, PdfFieldType type)
    {
        foreach (var group in FieldKeys)
        {
            if ((change.Parts & group.Part) != 0)
            {
                CopyKeys(field, built, group.Keys);
            }
        }

        if ((change.Parts & FormFieldParts.Value) == 0)
        {
            return;
        }

        if (type == PdfFieldType.Text)
        {
            CopyKeys(field, built, [KnownName.DS]);
            CopyName(store, field, built, "RV"u8);
        }
        else if (type is PdfFieldType.CheckBox or PdfFieldType.RadioButton)
        {
            CopyKeys(field, built, [KnownName.Opt]);
            KeepDefaultState(field, store, change.Spec);
        }
    }

    /// <summary>Copies entries from the built dictionary, or removes them when the built dictionary has none.</summary>
    /// <param name="field">The field copy being changed.</param>
    /// <param name="built">The entries after the change.</param>
    /// <param name="keys">The keys.</param>
    private static void CopyKeys(PdfDictionary field, PdfDictionary built, KnownName[] keys)
    {
        foreach (var key in keys)
        {
            _ = field.Remove(key);
            if (built.ContainsKey(key))
            {
                field.Set(key, built.GetRaw(key));
            }
        }
    }

    /// <summary>Copies one entry that has no known name.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="field">The field copy being changed.</param>
    /// <param name="built">The entries after the change.</param>
    /// <param name="spelling">The key's spelling.</param>
    private static void CopyName(PdfObjectStore store, PdfDictionary field, PdfDictionary built, ReadOnlySpan<byte> spelling)
    {
        var key = store.Names.Intern(spelling);
        _ = field.Remove(key);
        if (built.ContainsKey(key))
        {
            field.Set(key, built.GetRaw(key));
        }
    }

    /// <summary>Drops a button's default state when the planned states no longer have it.</summary>
    /// <param name="field">The field copy being changed.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="spec">The field after the change.</param>
    private static void KeepDefaultState(PdfDictionary field, PdfObjectStore store, FormFieldSpec spec)
    {
        if (spec.Buttons is not { } buttons || !field.ContainsKey(KnownName.DV))
        {
            return;
        }

        var name = store.Names.GetString(field.GetName(KnownName.DV));
        if (!string.Equals(name, FormValues.OffState, StringComparison.Ordinal) && Array.IndexOf(buttons.States, name) < 0)
        {
            _ = field.Remove(KnownName.DV);
        }
    }

    /// <summary>Writes the look entries of a widget.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="widget">The widget copy being changed.</param>
    /// <param name="change">The planned change.</param>
    /// <param name="look">The look entries after the change.</param>
    private static void WriteLook(PdfObjectStore store, PdfDictionary widget, FormFieldChange change, WidgetLook look)
    {
        var parts = change.Parts;
        if ((parts & (FormFieldParts.BorderColor | FormFieldParts.Background | FormFieldParts.Caption)) != 0)
        {
            var current = widget.GetDictionary(KnownName.MK)?.Clone() ?? new PdfDictionary(store);
            foreach (var group in LookKeys)
            {
                if ((parts & group.Part) != 0)
                {
                    CopyKeys(current, look.Characteristics, group.Keys);
                }
            }

            widget.Set(KnownName.MK, PdfValue.FromDictionary(current));
        }

        if ((parts & FormFieldParts.Border) != 0 && look.Border is not null)
        {
            widget.Set(KnownName.BS, PdfValue.FromDictionary(look.Border));
        }

        if ((parts & FormFieldParts.Actions) != 0 && change.Spec.Definition.Type == PdfFieldType.PushButton)
        {
            SetOrRemove(widget, KnownName.A, look.Activation);
        }
    }

    /// <summary>Sets a dictionary entry, or removes it when there is nothing to set.</summary>
    /// <param name="owner">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The value; <see langword="null"/> removes the entry.</param>
    private static void SetOrRemove(PdfDictionary owner, KnownName key, PdfDictionary? value)
    {
        if (value is null)
        {
            _ = owner.Remove(key);
            return;
        }

        owner.Set(key, PdfValue.FromDictionary(value));
    }

    /// <summary>Draws the appearance of one widget.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction receiving the streams.</param>
    /// <param name="drawing">The field and the change being drawn.</param>
    /// <param name="widget">The widget copy being changed.</param>
    /// <param name="on">The widget's on-state and whether it is on; the name is <see langword="null"/> for other fields.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    private static void DrawWidget(
        PdfDocument document,
        PdfEditTransaction transaction,
        WidgetDrawing drawing,
        PdfDictionary widget,
        FormButtonOnState on,
        CancellationToken cancellationToken)
    {
        var request = new FormAppearanceRequest(drawing.Type, drawing.Flags, drawing.Field, on.Name, cancellationToken);
        FormFieldAppearances.Attach(document, transaction, request, widget, drawing.PageIndex, on.IsOn ? on.Name : null);
    }

    /// <summary>The entries a part owns.</summary>
    /// <param name="Part">The part.</param>
    /// <param name="Keys">The keys.</param>
    [DebuggerDisplay("KeyGroup: {Part}")]
    private readonly record struct KeyGroup(FormFieldParts Part, KnownName[] Keys);

    /// <summary>The look entries a change writes into widgets.</summary>
    /// <param name="Characteristics">The characteristics after the change.</param>
    /// <param name="Border">The border style after the change; <see langword="null"/> for none.</param>
    /// <param name="Activation">The activation action after the change; <see langword="null"/> for none.</param>
    [DebuggerDisplay("WidgetLook")]
    private readonly record struct WidgetLook(PdfDictionary Characteristics, PdfDictionary? Border, PdfDictionary? Activation);

    /// <summary>The field and the change a widget is drawn for.</summary>
    /// <param name="Type">The kind of field.</param>
    /// <param name="Flags">The field flags after the change.</param>
    /// <param name="Field">The field copy that holds the new value.</param>
    /// <param name="PageIndex">The page the widget sits on, for its resources.</param>
    [DebuggerDisplay("WidgetDrawing: {Type} page {PageIndex}")]
    private readonly record struct WidgetDrawing(PdfFieldType Type, PdfFieldFlags Flags, PdfDictionary Field, int PageIndex);
}

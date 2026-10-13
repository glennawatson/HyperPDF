// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Builds the dictionaries of new fields and widgets (ISO 32000-2 12.7.3, Table 226, and 12.5.6.19, Table 191).</summary>
internal static class FormFieldBuilder
{
    /// <summary>The annotation flag that makes a widget print (ISO 32000-2 Table 167).</summary>
    private const int PrintFlag = 4;

    /// <summary>The default border width.</summary>
    private const float DefaultBorderWidth = 1;

    /// <summary>The grey of a push button's face.</summary>
    private const float ButtonFace = 0.75F;

    /// <summary>The grey of a signature field's border.</summary>
    private const float SignatureBorder = 0.5F;

    /// <summary>Writes the entries that belong to the field: its type, name, flags, value, options and actions.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The dictionary receiving the entries: the field, or the field merged with its only widget.</param>
    /// <param name="spec">The planned field.</param>
    internal static void WriteField(PdfObjectStore store, PdfDictionary target, FormFieldSpec spec)
    {
        var definition = spec.Definition;
        target.Set(KnownName.FT, PdfValue.FromName(FormFieldFlags.GetFieldType(definition.Type)));
        target.Set(KnownName.T, Text(store, spec.Partial));
        if (spec.Flags != PdfFieldFlags.None)
        {
            target.Set(KnownName.Ff, PdfValue.FromInteger((long)(uint)spec.Flags));
        }

        target.Set(KnownName.TU, Text(store, definition.Tooltip ?? definition.Name));
        target.Set(KnownName.DA, PdfValue.FromString(Encoding.ASCII.GetBytes(spec.DefaultAppearance)));
        if (definition.Quadding != 0)
        {
            target.Set(KnownName.Q, PdfValue.FromInteger(definition.Quadding));
        }

        if (definition.MaxLength > 0)
        {
            target.Set(KnownName.MaxLen, PdfValue.FromInteger(definition.MaxLength));
        }

        FormFieldValues.Write(store, target, spec);
        FormFieldActionData.WriteField(store, target, definition);
        WriteLock(store, target, definition);
    }

    /// <summary>Writes a widget's own entries: its rectangle, page, flags, characteristics, border and action.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The dictionary receiving the entries.</param>
    /// <param name="spec">The planned field.</param>
    /// <param name="placement">Where the widget goes.</param>
    /// <param name="pageId">The widget's page; not valid for a page written directly in the tree.</param>
    /// <param name="parent">The field the widget is a kid of; not valid when the widget is the field.</param>
    internal static void WriteWidget(PdfObjectStore store, PdfDictionary target, FormFieldSpec spec, PdfWidgetPlacement placement, PdfObjectId pageId, PdfObjectId parent)
    {
        target.Set(KnownName.Type, PdfValue.FromName(KnownName.Annot));
        target.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Widget));
        target.Set(KnownName.Rect, PdfValue.FromArray(placement.Bounds.ToArray(store)));
        if (pageId.IsValid)
        {
            target.Set(KnownName.P, PdfValue.FromReference(pageId));
        }

        target.Set(KnownName.F, PdfValue.FromInteger(PrintFlag));
        if (parent.IsValid)
        {
            target.Set(KnownName.Parent, PdfValue.FromReference(parent));
        }

        target.Set(KnownName.MK, PdfValue.FromDictionary(BuildCharacteristics(store, spec.Definition)));
        if (BuildBorder(store, spec.Definition) is { } border)
        {
            target.Set(KnownName.BS, PdfValue.FromDictionary(border));
        }

        FormFieldActionData.WriteWidget(store, target, spec.Definition);
    }

    /// <summary>Builds the appearance characteristics (<c>/MK</c>) of a widget.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="definition">The definition.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary BuildCharacteristics(PdfObjectStore store, PdfFieldDefinition definition)
    {
        var characteristics = new PdfDictionary(store);
        SetColor(store, characteristics, KnownName.BC, definition.BorderColor ?? DefaultBorder(definition.Type));
        var background = definition.BackgroundColor ?? DefaultBackground(definition.Type);
        if (background is { } color)
        {
            SetColor(store, characteristics, KnownName.BG, color);
        }

        if (definition.Type == PdfFieldType.PushButton && definition.Caption is { Length: > 0 } caption)
        {
            characteristics.Set(KnownName.CA, Text(store, caption));
        }
        else if (definition.Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton)
        {
            var mark = definition.Caption is { Length: > 0 } style ? style : DefaultMark(definition.Type);
            characteristics.Set(KnownName.CA, Text(store, mark));
        }

        return characteristics;
    }

    /// <summary>Writes a colour entry of <c>/MK</c>: three numbers, or an empty array for no colour.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="characteristics">The dictionary receiving the colour.</param>
    /// <param name="key">The entry's key.</param>
    /// <param name="color">The colour.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetColor(PdfObjectStore store, PdfDictionary characteristics, PdfName key, PdfFieldColor color) =>
        characteristics.Set(key, PdfValue.FromArray(color.IsTransparent ? new PdfArray(store) : PdfArray.FromNumbers(store, [color.Red, color.Green, color.Blue])));

    /// <summary>Builds the border style dictionary (<c>/BS</c>), when the definition asks for a width or style.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="definition">The definition.</param>
    /// <returns>The dictionary; <see langword="null"/> for the default border.</returns>
    internal static PdfDictionary? BuildBorder(PdfObjectStore store, PdfFieldDefinition definition)
    {
        if (definition.BorderWidth is null && definition.BorderStyle is null)
        {
            return null;
        }

        var border = new PdfDictionary(store);
        border.Set(KnownName.Type, PdfValue.FromName(KnownName.Border));
        border.Set(KnownName.W, PdfValue.FromReal(definition.BorderWidth ?? DefaultBorderWidth));
        border.Set(KnownName.S, PdfValue.FromName(GetStyleName(definition.BorderStyle ?? PdfFieldBorderStyle.Solid)));
        return border;
    }

    /// <summary>Gets the name a border style is written as.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The name.</returns>
    internal static KnownName GetStyleName(PdfFieldBorderStyle style) => style switch
    {
        PdfFieldBorderStyle.Dashed => KnownName.D,
        PdfFieldBorderStyle.Beveled => KnownName.B,
        PdfFieldBorderStyle.Inset => KnownName.I,
        PdfFieldBorderStyle.Underline => KnownName.U,
        _ => KnownName.S,
    };

    /// <summary>Makes a text string value, encoded for the document's version.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="text">The text.</param>
    /// <returns>The string value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfValue Text(PdfObjectStore store, string text) => PdfValue.FromString(PdfText.Encode(text, store.Version));

    /// <summary>Writes the lock dictionary of a signature field (<c>/Lock</c>, ISO 32000-2 Table 235).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="target">The dictionary receiving it.</param>
    /// <param name="definition">The definition.</param>
    private static void WriteLock(PdfObjectStore store, PdfDictionary target, PdfFieldDefinition definition)
    {
        if (definition.LockedFields is not { Length: > 0 } names)
        {
            return;
        }

        var fields = new PdfArray(store, names.Length);
        foreach (var name in names)
        {
            fields.Add(Text(store, name));
        }

        var fieldLock = new PdfDictionary(store);
        fieldLock.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("SigFieldLock")));
        fieldLock.Set(store.Names.Intern("Action"), PdfValue.FromName(store.Names.Intern("Include")));
        fieldLock.Set(KnownName.Fields, PdfValue.FromArray(fields));
        target.Set(KnownName.Lock, PdfValue.FromDictionary(fieldLock));
    }

    /// <summary>Gets the border colour a kind of field has when the definition gives none.</summary>
    /// <param name="type">The kind of field.</param>
    /// <returns>The colour.</returns>
    private static PdfFieldColor DefaultBorder(PdfFieldType type) =>
        type == PdfFieldType.Signature ? PdfFieldColor.FromRgb(SignatureBorder, SignatureBorder, SignatureBorder) : PdfFieldColor.Black;

    /// <summary>Gets the background colour a kind of field has when the definition gives none.</summary>
    /// <param name="type">The kind of field.</param>
    /// <returns>The colour; <see langword="null"/> for none.</returns>
    private static PdfFieldColor? DefaultBackground(PdfFieldType type) => type switch
    {
        PdfFieldType.PushButton => PdfFieldColor.FromRgb(ButtonFace, ButtonFace, ButtonFace),
        PdfFieldType.CheckBox or PdfFieldType.RadioButton => PdfFieldColor.White,
        _ => null,
    };

    /// <summary>Gets the mark a kind of button draws when the definition names none: a check, or a circle for a radio button.</summary>
    /// <param name="type">The kind of field.</param>
    /// <returns>The mark's style character.</returns>
    private static string DefaultMark(PdfFieldType type) => type == PdfFieldType.RadioButton ? "l" : "4";
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Works out a field's type and reads its value.</summary>
internal static class FormValues
{
    /// <summary>The text an unchecked button group has as its value.</summary>
    internal const string OffState = "Off";

    /// <summary>The state name used for a button whose appearance names none.</summary>
    internal const string DefaultOnState = "Yes";

    /// <summary>Works out the type of a field from its <c>/FT</c> and flags.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="names">The document's names.</param>
    /// <param name="flags">The field's flags.</param>
    /// <returns>The type; unknown when the field has no recognised <c>/FT</c>.</returns>
    internal static PdfFieldType GetType(PdfDictionary field, PdfNameTable names, PdfFieldFlags flags)
    {
        var value = FieldAttributes.Find(field, KnownName.FT);
        var type = value.Kind switch
        {
            PdfKind.Name => names.GetSpelling(value.AsName()),
            PdfKind.String => value.AsStringBytes(),
            _ => default,
        };
        return Classify(type, flags);
    }

    /// <summary>Reads a field's value as text.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="type">The field's type.</param>
    /// <returns>The value: a string, or a stream or array's first entry; the default value stands in for a missing one except for text fields.</returns>
    internal static string ReadText(PdfDictionary field, PdfFieldType type)
    {
        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.IsNull && type != PdfFieldType.Text)
        {
            value = FieldAttributes.Find(field, KnownName.DV);
        }

        if (value.AsArray() is { } array)
        {
            return array.Count > 0 ? FieldAttributes.ReadText(array.Get(0)) : string.Empty;
        }

        return FieldAttributes.ReadText(value);
    }

    /// <summary>Classifies an ASCII field type without allocating a string.</summary>
    /// <param name="type">The field type spelling.</param>
    /// <param name="flags">The field flags.</param>
    /// <returns>The field type.</returns>
    private static PdfFieldType Classify(ReadOnlySpan<byte> type, PdfFieldFlags flags)
    {
        if (type.SequenceEqual("Btn"u8))
        {
            return GetButtonType(flags);
        }

        if (type.SequenceEqual("Tx"u8))
        {
            return PdfFieldType.Text;
        }

        if (type.SequenceEqual("Ch"u8))
        {
            return (flags & PdfFieldFlags.Combo) != 0 ? PdfFieldType.ComboBox : PdfFieldType.ListBox;
        }

        return type.SequenceEqual("Sig"u8) ? PdfFieldType.Signature : PdfFieldType.Unknown;
    }

    /// <summary>Maps a button's flags to its type.</summary>
    /// <param name="flags">The field flags.</param>
    /// <returns>The type.</returns>
    private static PdfFieldType GetButtonType(PdfFieldFlags flags)
    {
        if ((flags & PdfFieldFlags.Radio) != 0)
        {
            return PdfFieldType.RadioButton;
        }

        return (flags & PdfFieldFlags.Pushbutton) != 0 ? PdfFieldType.PushButton : PdfFieldType.CheckBox;
    }
}

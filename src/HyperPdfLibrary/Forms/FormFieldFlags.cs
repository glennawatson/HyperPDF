// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Works out which field flags (ISO 32000-2 Table 227 to Table 230) belong to which kind of field.</summary>
internal static class FormFieldFlags
{
    /// <summary>The flags every kind of field takes.</summary>
    internal const PdfFieldFlags Common = PdfFieldFlags.ReadOnly | PdfFieldFlags.Required | PdfFieldFlags.NoExport;

    /// <summary>The flags that decide which kind of button or choice a field is; they cannot change after the field exists.</summary>
    internal const PdfFieldFlags TypeBits = PdfFieldFlags.Radio | PdfFieldFlags.Pushbutton | PdfFieldFlags.Combo;

    /// <summary>The flags only a text field takes.</summary>
    private const PdfFieldFlags TextOnly =
        PdfFieldFlags.Multiline | PdfFieldFlags.Password | PdfFieldFlags.FileSelect | PdfFieldFlags.DoNotSpellCheck
        | PdfFieldFlags.DoNotScroll | PdfFieldFlags.Comb | PdfFieldFlags.RichTextOrRadiosInUnison;

    /// <summary>The flags a radio group takes besides the common ones.</summary>
    private const PdfFieldFlags RadioOnly = PdfFieldFlags.NoToggleToOff | PdfFieldFlags.RichTextOrRadiosInUnison;

    /// <summary>The flags a combo box takes besides the common ones.</summary>
    private const PdfFieldFlags ComboOnly = PdfFieldFlags.Edit | PdfFieldFlags.Sort | PdfFieldFlags.DoNotSpellCheck | PdfFieldFlags.CommitOnSelectionChange;

    /// <summary>The flags a list box takes besides the common ones.</summary>
    private const PdfFieldFlags ListOnly = PdfFieldFlags.Sort | PdfFieldFlags.MultiSelect | PdfFieldFlags.CommitOnSelectionChange;

    /// <summary>Gets the flags a kind of field may carry that the caller chooses.</summary>
    /// <param name="type">The kind of field.</param>
    /// <returns>The allowed flags.</returns>
    internal static PdfFieldFlags GetAllowed(PdfFieldType type) => Common | type switch
    {
        PdfFieldType.Text => TextOnly,
        PdfFieldType.RadioButton => RadioOnly,
        PdfFieldType.ComboBox => ComboOnly,
        PdfFieldType.ListBox => ListOnly,
        _ => PdfFieldFlags.None,
    };

    /// <summary>Gets the flags that make a field the given kind.</summary>
    /// <param name="type">The kind of field.</param>
    /// <returns>The type flags.</returns>
    internal static PdfFieldFlags GetTypeFlags(PdfFieldType type) => type switch
    {
        PdfFieldType.RadioButton => PdfFieldFlags.Radio,
        PdfFieldType.PushButton => PdfFieldFlags.Pushbutton,
        PdfFieldType.ComboBox => PdfFieldFlags.Combo,
        _ => PdfFieldFlags.None,
    };

    /// <summary>Gets the <c>/FT</c> name of a kind of field.</summary>
    /// <param name="type">The kind of field.</param>
    /// <returns>The name.</returns>
    internal static KnownName GetFieldType(PdfFieldType type) => type switch
    {
        PdfFieldType.Text => KnownName.Tx,
        PdfFieldType.ComboBox or PdfFieldType.ListBox => KnownName.Ch,
        PdfFieldType.Signature => KnownName.Sig,
        _ => KnownName.Btn,
    };
}

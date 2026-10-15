// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A widget annotation together with the field it belongs to.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Index">The widget's index in the page's <c>/Annots</c> array.</param>
/// <param name="Widget">The widget dictionary.</param>
/// <param name="WidgetId">The widget's object id; not valid for a widget written directly in the array.</param>
/// <param name="Field">The field dictionary: the widget's parent when the widget has no partial name, otherwise the widget.</param>
/// <param name="FieldId">The field's object id.</param>
/// <param name="Type">The field type.</param>
/// <param name="Flags">The field flags.</param>
[DebuggerDisplay("ResolvedWidget: {Type} on page {PageIndex}")]
internal readonly record struct ResolvedWidget(
    int PageIndex,
    int Index,
    PdfDictionary Widget,
    PdfObjectId WidgetId,
    PdfDictionary Field,
    PdfObjectId FieldId,
    PdfFieldType Type,
    PdfFieldFlags Flags)
{
    /// <summary>Gets a value indicating whether the field and widget can both be edited.</summary>
    internal bool IsEditable => FieldId.IsValid && WidgetId.IsValid;

    /// <summary>Gets a value indicating whether the field is a check box or radio button.</summary>
    internal bool IsButton => Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton;

    /// <summary>Gets a value indicating whether the field is a combo box or list box.</summary>
    internal bool IsChoice => Type is PdfFieldType.ComboBox or PdfFieldType.ListBox;

    /// <summary>Gets a value indicating whether the field cannot be changed.</summary>
    internal bool IsReadOnly => (Flags & PdfFieldFlags.ReadOnly) != 0;

    /// <summary>Gets the field's full name; empty for a widget that belongs to no named field. The name is built each time it is read.</summary>
    internal string Name => FieldAttributes.GetFullName(Widget);
}

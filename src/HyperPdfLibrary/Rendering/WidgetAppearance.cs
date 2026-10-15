// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Draws a text, choice or push button widget from its value when the form sets <c>/NeedAppearances</c> (ISO 32000-2
/// 12.7.3.1). Check boxes, radio buttons, signatures and widgets of forms that do not ask are left alone.
/// </summary>
internal static class WidgetAppearance
{
    /// <summary>Generates the appearance of a widget.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The appearance, or <see langword="null"/> when the form does not ask or the field is not drawn from its value.</returns>
    internal static GeneratedAppearance? Create(in AnnotationContext context)
    {
        var form = context.Store.Catalog.GetDictionary(KnownName.AcroForm);
        if (form is null || !form.GetBoolean(KnownName.NeedAppearances))
        {
            return null;
        }

        var widget = context.Annotation;
        var field = !widget.ContainsKey(KnownName.T) && widget.GetDictionary(KnownName.Parent) is { } parent ? parent : widget;
        var source = FieldAttributes.Find(field, KnownName.FT).IsNull && widget.ContainsKey(KnownName.FT) ? widget : field;
        var flags = FieldAttributes.GetFlags(source);
        var type = FormValues.GetType(source, context.Store.Names, flags);
        return IsDrawnFromValue(type) ? Draw(context, form, field, type, flags) : null;
    }

    /// <summary>Determines whether a field type is drawn when the form asks readers to draw appearances.</summary>
    /// <param name="type">The field type.</param>
    /// <returns><see langword="true"/> for text, choice and push button fields.</returns>
    private static bool IsDrawnFromValue(PdfFieldType type) => type is PdfFieldType.Text or PdfFieldType.ComboBox or PdfFieldType.ListBox or PdfFieldType.PushButton;

    /// <summary>Draws a widget of a type that is drawn from its value.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="form">The AcroForm dictionary.</param>
    /// <param name="field">The field dictionary.</param>
    /// <param name="type">The field type.</param>
    /// <param name="flags">The field flags.</param>
    /// <returns>The appearance.</returns>
    private static GeneratedAppearance Draw(in AnnotationContext context, PdfDictionary form, PdfDictionary field, PdfFieldType type, PdfFieldFlags flags)
    {
        var widget = context.Annotation;
        var appearance = new AppearanceContext(context.Store, widget, field, type, flags, form, context.Page.Resources) { Fonts = context.Cache.Fonts };
        return new(CreateForm(appearance), null, AppearanceRects.Read(widget));
    }

    /// <summary>Draws a text, combo box, list box or push button widget from its value.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    private static PdfStream CreateForm(in AppearanceContext context)
    {
        if (context.Type == PdfFieldType.PushButton)
        {
            return FormPushButtonAppearance.Create(context);
        }

        if (context.Type == PdfFieldType.ListBox)
        {
            return FormTextAppearance.CreateList(context);
        }

        if (context.Type == PdfFieldType.Text)
        {
            return FormTextAppearance.CreateText(context, FieldAttributes.Find(context.Field, KnownName.V));
        }

        var text = FormValues.ReadText(context.Field, context.Type);
        if (context.Type == PdfFieldType.ComboBox)
        {
            var option = FormChoices.FindValue(context.Field, text);
            text = option >= 0 ? FormChoices.GetLabel(context.Field, option) : text;
        }

        return FormTextAppearance.CreateText(context, text);
    }
}

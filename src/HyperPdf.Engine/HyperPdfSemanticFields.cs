// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Reading;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>Describes the form fields of a page for assistive technology: what each is, its value and its state.</summary>
internal static class HyperPdfSemanticFields
{
    /// <summary>The most parents followed when looking for a field's tooltip, so a damaged cycle ends.</summary>
    private const int MaxDepth = 32;

    /// <summary>Adds the form fields of a page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="items">Receives the items.</param>
    internal static void Add(HyperPdfDocument self, int pageIndex, List<PageSemanticItem> items)
    {
        List<PdfFormWidget> widgets = [];
        PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(self.Document), pageIndex, widgets);
        if (widgets.Count == 0)
        {
            return;
        }

        var page = PdfDocumentPages.GetPage(self.Document, pageIndex);
        foreach (var widget in widgets)
        {
            var annotation = PdfPageAnnotations.Get(self.Document.Objects, page, widget.Index);
            items.Add(Describe(self.Document, widget, annotation, LinkTargets.ToPageRect(page.ToPhysicalRectangle(widget.Bounds))));
        }
    }

    /// <summary>Gets the words for a kind of field.</summary>
    /// <param name="type">The field type.</param>
    /// <returns>The role, such as "check box".</returns>
    internal static string RoleOf(PdfFieldType type) => type switch
    {
        PdfFieldType.PushButton => "button",
        PdfFieldType.CheckBox => "check box",
        PdfFieldType.RadioButton => "radio button",
        PdfFieldType.ComboBox => "combo box",
        PdfFieldType.ListBox => "list box",
        PdfFieldType.Text => "text field",
        PdfFieldType.Signature => "signature field",
        _ => "form field",
    };

    /// <summary>Puts a field's state into words, such as "checked, required, read only".</summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The state, or <see langword="null"/> when it has nothing to say.</returns>
    internal static string? StateOf(PdfFormWidget widget)
    {
        var state = new StringBuilder();
        if (widget.Type is PdfFieldType.CheckBox or PdfFieldType.RadioButton)
        {
            _ = state.Append(widget.IsChecked ? "checked" : "not checked");
        }

        Append(state, widget.IsRequired, "required");
        Append(state, widget.IsReadOnly, "read only");
        Append(state, widget.Type == PdfFieldType.Text && widget.IsMultiline, "several lines");
        return state.Length == 0 ? null : state.ToString();
    }

    /// <summary>Describes one widget.</summary>
    /// <param name="document">The document.</param>
    /// <param name="widget">The widget.</param>
    /// <param name="annotation">The widget's dictionary, or <see langword="null"/>.</param>
    /// <param name="bounds">The widget's box on the page.</param>
    /// <returns>The item.</returns>
    private static PageSemanticItem Describe(PdfDocument document, PdfFormWidget widget, PdfDictionary? annotation, HyperPdf.Core.Geometry.PageRect bounds)
    {
        var tooltip = annotation is null ? null : Tooltip(annotation);
        var rich = annotation is null || widget.Type != PdfFieldType.Text ? null : PdfFormRichValues.Describe(annotation, CancellationToken.None);
        return new(PageSemanticKind.FormField, tooltip ?? NameOf(widget), bounds)
        {
            Role = RoleOf(widget.Type),
            Description = tooltip is not null && widget.Name.Length > 0 ? $"Field {widget.Name}" : null,
            Value = ValueOf(widget, rich),
            State = StateOf(widget),
            RichText = rich is null ? null : DescribeStyles(rich),
            IsHidden = annotation is not null && HyperPdfPageSemantics.IsHidden(document, annotation),
        };
    }

    /// <summary>Gets what to call a field that has no tooltip: its name, else its role.</summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The name.</returns>
    private static string NameOf(PdfFormWidget widget) => widget.Name.Length > 0 ? widget.Name : RoleOf(widget.Type);

    /// <summary>Gets a field's value in words, never a password's.</summary>
    /// <param name="widget">The widget.</param>
    /// <param name="rich">The field's rich text, or <see langword="null"/>.</param>
    /// <returns>The value, or <see langword="null"/> when there is none or it must not be spoken.</returns>
    private static string? ValueOf(PdfFormWidget widget, PdfFormRichTextSummary? rich)
    {
        if ((widget.Flags & PdfFieldFlags.Password) != 0)
        {
            return null;
        }

        var text = widget.Value.Length > 0 ? widget.Value : rich?.PlainText;
        return text is { Length: > 0 } ? text : null;
    }

    /// <summary>Names the styles a rich text value uses.</summary>
    /// <param name="rich">The summary.</param>
    /// <returns>The styles in words, such as "bold, italic", or "formatted" when it only has paragraphs.</returns>
    private static string DescribeStyles(PdfFormRichTextSummary rich)
    {
        var styles = new StringBuilder();
        Append(styles, rich.Bold, "bold");
        Append(styles, rich.Italic, "italic");
        Append(styles, rich.Underline, "underlined");
        Append(styles, rich.StrikeOut, "struck out");
        return styles.Length == 0 ? "formatted text" : styles.ToString();
    }

    /// <summary>Adds a word to a comma separated list when a condition holds.</summary>
    /// <param name="builder">The list.</param>
    /// <param name="condition">Whether to add it.</param>
    /// <param name="word">The word.</param>
    private static void Append(StringBuilder builder, bool condition, string word)
    {
        if (!condition)
        {
            return;
        }

        _ = builder.Append(builder.Length == 0 ? string.Empty : ", ").Append(word);
    }

    /// <summary>Finds a field's tooltip (<c>/TU</c>), on the widget or the nearest parent that has one.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <returns>The tooltip, or <see langword="null"/>.</returns>
    private static string? Tooltip(PdfDictionary widget)
    {
        var current = widget;
        for (var depth = 0; current is not null && depth < MaxDepth; depth++)
        {
            if (current.GetText(KnownName.TU) is { Length: > 0 } tooltip)
            {
                return tooltip;
            }

            current = current.GetDictionary(KnownName.Parent);
        }

        return null;
    }
}

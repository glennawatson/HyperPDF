// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
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

    /// <summary>The flag for a required field.</summary>
    private const int RequiredBit = 1;

    /// <summary>The flag for a read-only field.</summary>
    private const int ReadOnlyBit = 2;

    /// <summary>The flag for a text field of several lines.</summary>
    private const int MultilineBit = 4;

    /// <summary>The combinations of the three flags.</summary>
    private const int StateFlagCombinations = 8;

    /// <summary>The check state of a field that cannot be ticked.</summary>
    private const int NoBox = 0;

    /// <summary>The check state of a ticked box.</summary>
    private const int CheckedBox = 1;

    /// <summary>The check state of a box that is not ticked.</summary>
    private const int NotCheckedBox = 2;

    /// <summary>The flag for bold text.</summary>
    private const int BoldBit = 1;

    /// <summary>The flag for italic text.</summary>
    private const int ItalicBit = 2;

    /// <summary>The flag for underlined text.</summary>
    private const int UnderlineBit = 4;

    /// <summary>The flag for struck-out text.</summary>
    private const int StrikeBit = 8;

    /// <summary>The words for each state of a field, made once: the check state times eight plus the flags.</summary>
    private static readonly string?[] States = MakeStates();

    /// <summary>The words for each combination of text styles, made once.</summary>
    private static readonly string[] Styles = MakeStyles();

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
        var box = BoxState(widget);
        var flags = (widget.IsRequired ? RequiredBit : 0) | (widget.IsReadOnly ? ReadOnlyBit : 0) | (widget.Type == PdfFieldType.Text && widget.IsMultiline ? MultilineBit : 0);
        return States[(box * StateFlagCombinations) + flags];
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string DescribeStyles(PdfFormRichTextSummary rich) =>
        Styles[(rich.Bold ? BoldBit : 0) | (rich.Italic ? ItalicBit : 0) | (rich.Underline ? UnderlineBit : 0) | (rich.StrikeOut ? StrikeBit : 0)];

    /// <summary>Gets whether a box or radio button is ticked.</summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The check state: not a box, ticked, or not ticked.</returns>
    private static int BoxState(PdfFormWidget widget)
    {
        if (widget.Type is not (PdfFieldType.CheckBox or PdfFieldType.RadioButton))
        {
            return NoBox;
        }

        return widget.IsChecked ? CheckedBox : NotCheckedBox;
    }

    /// <summary>Makes the state words for every combination of a field's state, once.</summary>
    /// <returns>The words by combination: the check state times the flag combinations plus the flags; <see langword="null"/> for none.</returns>
    private static string?[] MakeStates()
    {
        string[] boxes = [string.Empty, "checked", "not checked"];
        string[] flagWords = ["required", "read only", "several lines"];
        var states = new string?[boxes.Length * StateFlagCombinations];
        for (var box = 0; box < boxes.Length; box++)
        {
            for (var flags = 0; flags < StateFlagCombinations; flags++)
            {
                var text = Join(boxes[box], flags, flagWords);
                states[(box * StateFlagCombinations) + flags] = text.Length == 0 ? null : text;
            }
        }

        return states;
    }

    /// <summary>Makes the style words for every combination of styles, once.</summary>
    /// <returns>The words by combination of bold, italic, underline and strike-out.</returns>
    private static string[] MakeStyles()
    {
        string[] words = ["bold", "italic", "underlined", "struck out"];
        var styles = new string[1 << words.Length];
        for (var flags = 0; flags < styles.Length; flags++)
        {
            var text = Join(string.Empty, flags, words);
            styles[flags] = text.Length == 0 ? "formatted text" : text;
        }

        return styles;
    }

    /// <summary>Joins a first word and the words whose bits are set, with commas.</summary>
    /// <param name="first">The first word, or empty for none.</param>
    /// <param name="flags">Which of the words to add, one bit each.</param>
    /// <param name="words">The words.</param>
    /// <returns>The joined words; empty when there are none.</returns>
    private static string Join(string first, int flags, string[] words)
    {
        var list = new List<string>();
        if (first.Length > 0)
        {
            list.Add(first);
        }

        for (var i = 0; i < words.Length; i++)
        {
            if ((flags & (1 << i)) != 0)
            {
                list.Add(words[i]);
            }
        }

        return string.Join(", ", list);
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

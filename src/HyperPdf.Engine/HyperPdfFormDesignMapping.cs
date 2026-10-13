// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine;

/// <summary>Converts between the viewer's form design types and the library's, moving rectangles between page space and user space.</summary>
internal static class HyperPdfFormDesignMapping
{
    /// <summary>Every flag the form design sets.</summary>
    private const FormDesignFlags KnownFlags =
        FormDesignFlags.ReadOnly | FormDesignFlags.Required | FormDesignFlags.NoExport | FormDesignFlags.Multiline | FormDesignFlags.Password
        | FormDesignFlags.NoToggleToOff | FormDesignFlags.Edit | FormDesignFlags.Sort | FormDesignFlags.MultiSelect | FormDesignFlags.DoNotSpellCheck
        | FormDesignFlags.DoNotScroll | FormDesignFlags.Comb | FormDesignFlags.CommitOnSelectionChange;

    /// <summary>Converts a field to add.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="design">The field.</param>
    /// <returns>The library definition.</returns>
    /// <exception cref="ArgumentException">A widget names a page the document does not have.</exception>
    internal static PdfFieldDefinition ToDefinition(PdfDocument document, FormFieldDesign design)
    {
        var widgets = new PdfWidgetPlacement[design.Widgets.Count];
        for (var i = 0; i < widgets.Length; i++)
        {
            var widget = design.Widgets[i];
            var page = GetPage(document, widget.PageIndex);
            widgets[i] = new(widget.PageIndex, HyperPdfAnnotationReading.ToUserRectangle(page, widget.Bounds)) { ExportValue = widget.ExportValue };
        }

        return new((PdfFieldType)design.Kind, design.Name, widgets)
        {
            Tooltip = design.Tooltip,
            Flags = (PdfFieldFlags)(design.Flags & KnownFlags),
            Value = design.Value,
            Values = [.. design.Values],
            DefaultValue = design.DefaultValue,
            IsChecked = design.IsChecked,
            ExportValue = design.ExportValue,
            MaxLength = design.MaxLength,
            Quadding = design.Quadding,
            DefaultAppearance = design.DefaultAppearance,
            FontSize = design.FontSize,
            Options = ToOptions(design.Options),
            Caption = design.Caption,
        };
    }

    /// <summary>Converts changes to a field.</summary>
    /// <param name="changes">The changes.</param>
    /// <returns>The library changes.</returns>
    internal static PdfFieldChanges ToChanges(FormFieldChanges changes) => new()
    {
        Tooltip = changes.Tooltip,
        AddFlags = (PdfFieldFlags)(changes.AddFlags & KnownFlags),
        RemoveFlags = (PdfFieldFlags)(changes.RemoveFlags & KnownFlags),
        Value = changes.Value,
        Values = changes.Values is null ? null : [.. changes.Values],
        IsChecked = changes.IsChecked,
        DefaultValue = changes.DefaultValue,
        MaxLength = changes.MaxLength,
        Quadding = changes.Quadding,
        DefaultAppearance = changes.DefaultAppearance,
        Options = changes.Options is null ? null : ToOptions(changes.Options),
        ExportValues = changes.ExportValues is null ? null : [.. changes.ExportValues],
        Caption = changes.Caption,
    };

    /// <summary>Converts a field read from the document.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="details">The library's description of the field.</param>
    /// <returns>The viewer's description.</returns>
    internal static FormFieldInfo ToInfo(PdfDocument document, PdfFieldDetails details)
    {
        var widgets = new FormWidgetInfo[details.Widgets.Length];
        for (var i = 0; i < widgets.Length; i++)
        {
            var widget = details.Widgets[i];
            var page = GetPage(document, widget.Location.PageIndex);
            widgets[i] = new(widget.Location.PageIndex, widget.Location.Index, HyperPdfAnnotationReading.ToPageRect(page, widget.Bounds), widget.ExportValue, widget.IsChecked);
        }

        var options = new FormChoiceOption[details.Options.Length];
        for (var i = 0; i < options.Length; i++)
        {
            options[i] = new(details.Options[i].Value, details.Options[i].Label);
        }

        return new(
            details.Name,
            (FormFieldKind)details.Type,
            (FormDesignFlags)details.Flags & KnownFlags,
            details.Tooltip,
            details.Value,
            details.Values,
            details.DefaultValue,
            details.MaxLength,
            details.Quadding,
            details.DefaultAppearance,
            options,
            widgets);
    }

    /// <summary>Gets a page of the library document.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentException">The document has no such page.</exception>
    internal static PdfPage GetPage(PdfDocument document, int pageIndex) =>
        (uint)pageIndex < (uint)document.PageCount
            ? PdfDocumentPages.GetPage(document, pageIndex)
            : throw new ArgumentException($"The document has no page {pageIndex + 1}.", nameof(pageIndex));

    /// <summary>Converts choices.</summary>
    /// <param name="options">The viewer's choices.</param>
    /// <returns>The library's choices.</returns>
    private static PdfFieldOption[] ToOptions(IReadOnlyList<FormChoiceOption> options)
    {
        var result = new PdfFieldOption[options.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(options[i].Value, options[i].Label);
        }

        return result;
    }
}

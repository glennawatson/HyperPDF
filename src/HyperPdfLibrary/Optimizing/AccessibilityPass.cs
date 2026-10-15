// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Fills in the catalog entries screen readers and PDF/UA checkers look for, without changing any that are present:
/// /Lang from the options or the XMP dc:language, the title from the XMP dc:title or /Info, /ViewerPreferences
/// /DisplayDocTitle when there is a title, and /MarkInfo /Marked only when the document has a structure tree. Existing
/// strings and XMP packets keep their bytes; a new title written into XMP is edited in place.
/// </summary>
internal static class AccessibilityPass
{
    /// <summary>The local name of the Dublin Core language property.</summary>
    private const string LanguageProperty = "language";

    /// <summary>Fills in the missing entries.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="options">The options, which may give a language.</param>
    /// <param name="report">Receives the changes.</param>
    internal static void Run(PdfDocument document, in PdfOptimizeOptions options, OptimizeReportBuilder report)
    {
        var store = document.Objects;
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (!root.IsReference || StoreReading.Resolve(store, root).AsDictionary() is not { } catalog)
        {
            return;
        }

        var xmp = PdfDocumentMetadata.GetXmp(document);

        // The catalog is copied only when one of the steps changes it.
        PdfDictionary? copy = null;
        FillLanguage(catalog, ref copy, options.Language, xmp, report);
        if (FillTitle(document, xmp, report))
        {
            ShowTitle(catalog, ref copy, store, report);
        }

        MarkTagged(catalog, ref copy, report);
        if (copy is null)
        {
            return;
        }

        StoreEditing.Replace(store, root.AsReference(), PdfValue.FromDictionary(copy));
        PdfDocumentOptimizing.RefreshAfterOptimizerEdit(document);
    }

    /// <summary>Sets /Lang when it is missing.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="copy">The catalog copy to change, made on first use.</param>
    /// <param name="preferred">The language from the options, or <see langword="null"/>.</param>
    /// <param name="xmp">The document's XMP, or <see langword="null"/>.</param>
    /// <param name="report">Receives the change.</param>
    private static void FillLanguage(PdfDictionary catalog, ref PdfDictionary? copy, string? preferred, XmpMetadata? xmp, OptimizeReportBuilder report)
    {
        if (catalog.GetText(KnownName.Lang) is { Length: > 0 })
        {
            return;
        }

        var chosen = preferred;
        if (string.IsNullOrWhiteSpace(chosen) && xmp?.GetValues(XmpMetadata.DublinCoreNamespace, LanguageProperty) is { Length: > 0 } declared)
        {
            chosen = declared[0];
        }

        if (string.IsNullOrWhiteSpace(chosen))
        {
            report.Skip(PdfOptimizeCategory.Accessibility, 0, "The document has no /Lang and none is known; set the language option to add one.");
            return;
        }

        copy ??= catalog.Clone();
        copy.Set(KnownName.Lang, PdfValue.FromString(PdfText.Encode(chosen)));
        report.Noted(PdfOptimizeCategory.Accessibility, 0, $"Set the document language to {chosen}.");
    }

    /// <summary>
    /// Copies the title between /Info and XMP when one of them lacks it. A PDF/A-4 document keeps its title in XMP only,
    /// as its /Info may hold nothing but /ModDate (ISO 19005-4:2020 §6.1.3).
    /// </summary>
    /// <param name="document">The working copy.</param>
    /// <param name="xmp">The document's XMP, or <see langword="null"/>.</param>
    /// <param name="report">Receives the change.</param>
    /// <returns><see langword="true"/> when the document has a title afterwards.</returns>
    private static bool FillTitle(PdfDocument document, XmpMetadata? xmp, OptimizeReportBuilder report)
    {
        var store = document.Objects;
        var infoTitle = StoreReading.Resolve(store, store.Trailer.GetRaw(KnownName.Info)).AsDictionary()?.GetText(KnownName.Title);
        var xmpTitle = xmp?.Title;
        var hasInfo = !string.IsNullOrWhiteSpace(infoTitle);
        var hasXmp = !string.IsNullOrWhiteSpace(xmpTitle);
        if (hasInfo != hasXmp && xmp is not null)
        {
            CopyTitle(document, xmp, hasInfo ? infoTitle! : xmpTitle!, hasInfo, report);
        }

        return hasInfo || hasXmp;
    }

    /// <summary>Copies a title into the place that lacks it.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="xmp">The document's XMP.</param>
    /// <param name="title">The title.</param>
    /// <param name="toXmp">Whether the title goes from /Info to XMP; otherwise from XMP to /Info.</param>
    /// <param name="report">Receives the change.</param>
    private static void CopyTitle(PdfDocument document, XmpMetadata xmp, string title, bool toXmp, OptimizeReportBuilder report)
    {
        if (toXmp)
        {
            WriteXmpTitle(document, title, report);
        }
        else if (xmp.PdfAPart != PdfAClaim.Part4)
        {
            WriteInfoTitle(document.Objects, title, report);
        }
    }

    /// <summary>Writes a title into /Info, creating it when missing.</summary>
    /// <param name="store">The working store.</param>
    /// <param name="title">The title.</param>
    /// <param name="report">Receives the change.</param>
    private static void WriteInfoTitle(PdfObjectStore store, string title, OptimizeReportBuilder report)
    {
        var raw = store.Trailer.GetRaw(KnownName.Info);
        var copy = StoreReading.Resolve(store, raw).AsDictionary()?.Clone() ?? new PdfDictionary(store);
        copy.Set(KnownName.Title, PdfValue.FromString(PdfText.Encode(title)));
        if (raw.IsReference)
        {
            StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromDictionary(copy));
        }
        else
        {
            store.Trailer.Set(KnownName.Info, PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromDictionary(copy))));
        }

        report.Noted(PdfOptimizeCategory.Accessibility, 0, "Copied the XMP title into the document information.");
    }

    /// <summary>Writes a title into the catalog's XMP packet, editing it in place.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="title">The title.</param>
    /// <param name="report">Receives the change.</param>
    private static void WriteXmpTitle(PdfDocument document, string title, OptimizeReportBuilder report)
    {
        var store = document.Objects;
        var raw = document.Catalog.GetRaw(KnownName.Metadata);
        if (!raw.IsReference || StoreReading.Resolve(store, raw).AsStream() is not { } stream)
        {
            return;
        }

        if (XmpEditor.Apply(stream.DecodeToArray(), [new XmpChange(XmpProperty.Title, title)]) is not { } packet)
        {
            return;
        }

        // XMP is stored uncompressed so tools that do not read PDF can find it.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
        report.Noted(PdfOptimizeCategory.Accessibility, 0, "Copied the document information title into the XMP.");
    }

    /// <summary>Sets /ViewerPreferences /DisplayDocTitle when it is not already set.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="catalogCopy">The catalog copy to change, made on first use.</param>
    /// <param name="store">The working store.</param>
    /// <param name="report">Receives the change.</param>
    private static void ShowTitle(PdfDictionary catalog, ref PdfDictionary? catalogCopy, PdfObjectStore store, OptimizeReportBuilder report)
    {
        var raw = catalog.GetRaw(KnownName.ViewerPreferences);
        var preferences = StoreReading.Resolve(store, raw).AsDictionary();
        if (preferences?.GetBoolean(OptimizerNames.DisplayDocTitle, false) == true)
        {
            return;
        }

        var copy = preferences?.Clone() ?? new PdfDictionary(store, 1);
        copy.Set(OptimizerNames.DisplayDocTitle, PdfValue.FromBoolean(true));
        if (raw.IsReference)
        {
            StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromDictionary(copy));
        }
        else
        {
            catalogCopy ??= catalog.Clone();
            catalogCopy.Set(KnownName.ViewerPreferences, PdfValue.FromDictionary(copy));
        }

        report.Noted(PdfOptimizeCategory.Accessibility, 0, "Viewers now show the document title instead of the file name.");
    }

    /// <summary>Sets /MarkInfo /Marked when the document has a structure tree and does not say it is tagged.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="catalogCopy">The catalog copy to change, made on first use.</param>
    /// <param name="report">Receives the change.</param>
    private static void MarkTagged(PdfDictionary catalog, ref PdfDictionary? catalogCopy, OptimizeReportBuilder report)
    {
        if (catalog.GetRaw(KnownName.StructTreeRoot).IsNull)
        {
            return;
        }

        var markInfo = catalog.GetDictionary(KnownName.MarkInfo);
        if (markInfo?.GetBoolean(OptimizerNames.Marked, false) == true)
        {
            return;
        }

        var copy = markInfo?.Clone() ?? new PdfDictionary(catalog.Owner, 1);
        copy.Set(OptimizerNames.Marked, PdfValue.FromBoolean(true));
        catalogCopy ??= catalog.Clone();
        catalogCopy.Set(KnownName.MarkInfo, PdfValue.FromDictionary(copy));
        report.Noted(PdfOptimizeCategory.Accessibility, 0, "Marked the document as tagged, as it has a structure tree.");
    }
}

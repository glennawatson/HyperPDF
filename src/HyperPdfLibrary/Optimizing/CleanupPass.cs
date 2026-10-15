// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The optional cleanup: page thumbnails, private application data, empty annotation arrays and page resources the
/// content never names. Changed dictionaries are copied and put back; the catalog's /Metadata, /StructTreeRoot,
/// /OutputIntents, /AF, /AcroForm and /OCProperties are never touched. A resource dictionary is cleaned only when every
/// stream that can use it was read: inline images, Type 3 fonts or appearance streams that borrow it keep it whole.
/// </summary>
/// <param name="document">The working copy.</param>
/// <param name="scanner">What the content analysis learned.</param>
/// <param name="items">The steps to run.</param>
/// <param name="report">Receives the changes.</param>
[DebuggerDisplay("CleanupPass: {_replaced.Count} resource dictionaries replaced")]
internal sealed class CleanupPass(PdfDocument document, ContentUsageScanner scanner, PdfCleanupItems items, OptimizeReportBuilder report)
{
    /// <summary>The bytes an object's header and trailer take, counted for a dropped object.</summary>
    private const int ObjectOverhead = 20;

    /// <summary>The resource dictionaries already replaced, by object number.</summary>
    private readonly HashSet<int> _replaced = [];

    /// <summary>Gets the resource categories cleaned.</summary>
    private static ReadOnlySpan<int> Categories =>
        [(int)KnownName.XObject,
        (int)KnownName.Font,
        (int)KnownName.ExtGState,
        (int)KnownName.ColorSpace,
        (int)KnownName.Pattern,
        (int)KnownName.Shading,
        (int)KnownName.Properties,
        ];

    /// <summary>Runs the cleanup.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="scanner">What the content analysis learned.</param>
    /// <param name="items">The steps to run.</param>
    /// <param name="report">Receives the changes.</param>
    internal static void Run(PdfDocument document, ContentUsageScanner scanner, PdfCleanupItems items, OptimizeReportBuilder report)
    {
        var pass = new CleanupPass(document, scanner, items, report);
        for (var i = 0; i < document.PageCount; i++)
        {
            pass.CleanPage(PdfDocumentPages.GetPage(document, i));
        }

        if ((items & PdfCleanupItems.PieceInfo) != 0)
        {
            pass.CleanCatalog();
        }

        PdfDocumentOptimizing.RefreshAfterOptimizerEdit(document);
    }

    /// <summary>Gets the stored size of a value's object, for the report.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The raw stream length plus overhead, or the overhead alone.</returns>
    private long SizeOf(PdfValue value) => (StoreReading.Resolve(document.Objects, value).AsStream()?.RawLength ?? 0) + ObjectOverhead;

    /// <summary>Cleans one page.</summary>
    /// <param name="page">The page.</param>
    private void CleanPage(PdfPage page)
    {
        var store = document.Objects;
        if (StoreReading.GetObject(store, page.Id).AsDictionary() is not { } current)
        {
            return;
        }

        // The page dictionary is copied only when a step changes it.
        PdfDictionary? copy = null;
        RemoveEntry(current, ref copy, OptimizerNames.Thumb, PdfCleanupItems.Thumbnails, page, "Removed the page thumbnail.");
        RemoveEntry(current, ref copy, OptimizerNames.PieceInfo, PdfCleanupItems.PieceInfo, page, "Removed private application data from the page.");
        RemoveEmptyAnnotations(current, ref copy, page);
        if ((items & PdfCleanupItems.UnusedResources) != 0)
        {
            CleanResources(current, ref copy, page);
        }

        if (copy is not null)
        {
            StoreEditing.Replace(store, page.Id, PdfValue.FromDictionary(copy));
        }
    }

    /// <summary>Removes one entry when its step is on.</summary>
    /// <param name="current">The page dictionary.</param>
    /// <param name="copy">The copy to change, made on first use.</param>
    /// <param name="key">The entry.</param>
    /// <param name="step">The step that removes it.</param>
    /// <param name="source">The page.</param>
    /// <param name="description">What was done.</param>
    private void RemoveEntry(PdfDictionary current, ref PdfDictionary? copy, PdfName key, PdfCleanupItems step, PdfPage source, string description)
    {
        if ((items & step) == 0 || !current.ContainsKey(key))
        {
            return;
        }

        var size = SizeOf(current.GetRaw(key));
        copy ??= current.Clone();
        _ = copy.Remove(key);
        report.Changed(PdfOptimizeCategory.Cleanup, source.Id.Number, description, size, 0);
    }

    /// <summary>Removes an empty /Annots array.</summary>
    /// <param name="current">The page dictionary.</param>
    /// <param name="copy">The copy to change, made on first use.</param>
    /// <param name="source">The page.</param>
    private void RemoveEmptyAnnotations(PdfDictionary current, ref PdfDictionary? copy, PdfPage source)
    {
        if ((items & PdfCleanupItems.EmptyAnnotations) == 0 || current.GetArray(KnownName.Annots) is not { Count: 0 })
        {
            return;
        }

        copy ??= current.Clone();
        _ = copy.Remove(KnownName.Annots);
        report.Changed(PdfOptimizeCategory.Cleanup, source.Id.Number, "Removed an empty annotation array.", ObjectOverhead, 0);
    }

    /// <summary>Removes resource entries the page's content never names.</summary>
    /// <param name="current">The page dictionary.</param>
    /// <param name="copy">The copy to change, made on first use.</param>
    /// <param name="source">The page.</param>
    private void CleanResources(PdfDictionary current, ref PdfDictionary? copy, PdfPage source)
    {
        var raw = current.GetRaw(KnownName.Resources);
        if (raw.IsNull || source.Resources is not { } resources)
        {
            // Inherited resources may serve other pages; they stay whole.
            return;
        }

        if (scanner.UnsafeResources.Contains(resources) || (raw.IsReference && _replaced.Contains(raw.AsReference().Number)))
        {
            return;
        }

        var used = scanner.UsedNames.GetValueOrDefault(resources);
        if (Clean(resources, used, source.Id.Number) is not { } cleaned)
        {
            return;
        }

        if (raw.IsReference)
        {
            _ = _replaced.Add(raw.AsReference().Number);
            StoreEditing.Replace(document.Objects, raw.AsReference(), PdfValue.FromDictionary(cleaned));
            return;
        }

        copy ??= current.Clone();
        copy.Set(KnownName.Resources, PdfValue.FromDictionary(cleaned));
    }

    /// <summary>Copies a resource dictionary without the names its content does not use.</summary>
    /// <param name="resources">The resource dictionary.</param>
    /// <param name="used">The names used, or <see langword="null"/> when none are.</param>
    /// <param name="pageNumber">The page's object number, for the report.</param>
    /// <returns>The copy, or <see langword="null"/> when nothing was unused.</returns>
    private PdfDictionary? Clean(PdfDictionary resources, HashSet<ResourceUse>? used, int pageNumber)
    {
        PdfDictionary? copy = null;
        foreach (var category in Categories)
        {
            if (resources.GetDictionary((KnownName)category) is not { } entries)
            {
                continue;
            }

            var dropped = CountDropped((KnownName)category, entries, used);
            if (dropped == 0)
            {
                continue;
            }

            var kept = new PdfDictionary(document.Objects, entries.Count - dropped);
            for (var i = 0; i < entries.Count; i++)
            {
                var name = entries.GetKeyAt(i);
                if (IsKept((KnownName)category, name, used))
                {
                    kept.Set(name, entries.GetValueAt(i));
                }
            }

            copy ??= resources.Clone();
            copy.Set((KnownName)category, PdfValue.FromDictionary(kept));
            var description = string.Create(CultureInfo.InvariantCulture, $"Removed {dropped} unused /{document.Objects.Names.GetString((KnownName)category)} resources.");
            report.Changed(PdfOptimizeCategory.Cleanup, pageNumber, description, 0, 0);
        }

        return copy;
    }

    /// <summary>Counts the entries of a resource category that do not stay.</summary>
    /// <param name="category">The category.</param>
    /// <param name="entries">The category's dictionary.</param>
    /// <param name="used">The names used, or <see langword="null"/>.</param>
    /// <returns>The number of unused entries.</returns>
    private int CountDropped(KnownName category, PdfDictionary entries, HashSet<ResourceUse>? used)
    {
        var dropped = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            dropped += IsKept(category, entries.GetKeyAt(i), used) ? 0 : 1;
        }

        return dropped;
    }

    /// <summary>Determines whether a resource entry stays: it is used, or it is a default colour space used implicitly.</summary>
    /// <param name="category">The category.</param>
    /// <param name="name">The entry's name.</param>
    /// <param name="used">The names used, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it stays.</returns>
    private bool IsKept(
        KnownName category,
        PdfName name,
        HashSet<ResourceUse>? used) =>
        (category == KnownName.ColorSpace
        && document.Objects.Names.GetSpelling(name).StartsWith("Default"u8))
        || used?.Contains(new(
        category,
        name)) == true;

    /// <summary>Removes the catalog's private application data.</summary>
    private void CleanCatalog()
    {
        var store = document.Objects;
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (!root.IsReference || StoreReading.Resolve(store, root).AsDictionary() is not { } catalog || !catalog.ContainsKey(OptimizerNames.PieceInfo))
        {
            return;
        }

        var copy = catalog.Clone();
        var size = SizeOf(copy.GetRaw(OptimizerNames.PieceInfo));
        _ = copy.Remove(OptimizerNames.PieceInfo);
        StoreEditing.Replace(store, root.AsReference(), PdfValue.FromDictionary(copy));
        report.Changed(PdfOptimizeCategory.Cleanup, root.AsReference().Number, "Removed private application data from the catalog.", size, 0);
    }
}

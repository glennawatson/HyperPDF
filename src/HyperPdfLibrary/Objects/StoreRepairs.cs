// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <summary>Repairs damaged cross-reference tables and document catalogs.</summary>
public static class StoreRepairs
{
    /// <summary>Gets a value indicating whether reading has so far repaired damage in the file. Faults inside streams and fonts show only once they are read.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>True when the store records repairs.</returns>
    public static bool HasRepairs(PdfObjectStore self) => self.WasRepaired || self.Context?.HasRepairs == true;

    /// <summary>Gets the distinct repairs and limits reported so far, in the order they were first met.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>A copy of the reports.</returns>
    public static PdfDiagnostic[] GetDiagnostics(PdfObjectStore self) => self.Context?.Snapshot() ?? [];

    /// <summary>Gets the distinct repairs reported so far, without the limit reports.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>A copy of the repair reports.</returns>
    public static PdfDiagnostic[] GetRepairs(PdfObjectStore self)
    {
        var all = StoreRepairs.GetDiagnostics(self);
        var count = 0;
        foreach (var report in all)
        {
            count += report.Code.IsRepair ? 1 : 0;
        }

        if (count == all.Length)
        {
            return all;
        }

        var repairs = new PdfDiagnostic[count];
        var next = 0;
        foreach (var report in all)
        {
            if (!report.Code.IsRepair)
            {
                continue;
            }

            repairs[next] = report;
            next++;
        }

        return repairs;
    }

    /// <summary>Rebuilds the cross-reference table by scanning the file, keeping the trailer when the scan finds none.</summary>
    /// <param name = "self">The owned object-store state.</param>
    internal static void Repair(PdfObjectStore self)
    {
        self.RepairTriedState = true;
        var rebuilt = XrefRepair.Rebuild(self.Source, self);
        rebuilt.Trailer ??= self.XrefState.Trailer;
        self.XrefState = rebuilt;
        self.ObjectStreamsIfCreated?.Clear();

        // Objects already parsed stay cached; only the ones that were missing are looked up in the new table.
        var grown = new object?[Math.Max(rebuilt.Size, self.CacheState.Length)];
        self.CacheState.CopyTo(grown, 0);
        Volatile.Write(ref self.CacheState, grown);
    }

    /// <summary>
    /// Indexes object streams again after the file key is known, for a table rebuilt by scanning, because its object
    /// streams could not be read while they were still encrypted.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    internal static void IndexCompressedAfterAuthentication(PdfObjectStore self)
    {
        if (!self.XrefState.Repaired)
        {
            return;
        }

        XrefRepair.IndexCompressed(self.XrefState, self);
        self.NextNumberState = Math.Max(self.NextNumberState, self.XrefState.Size);
        if (self.CacheState.Length < self.XrefState.Size)
        {
            Array.Resize(ref self.CacheState, self.XrefState.Size);
        }
    }

    /// <summary>Gets what the cross-reference table says about an object.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The entry type, free for numbers the table does not hold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static XrefEntryType GetEntryType(PdfObjectStore self, int number) => self.XrefState.GetType(number);

    /// <summary>Gets the file offset of an object, or the number of its object stream.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "number">The object number.</param>
    /// <returns>The location the table gives, or -1 for numbers it does not hold.</returns>
    internal static long GetEntryLocation(PdfObjectStore self, int number) => (uint)number < (uint)self.XrefState.Size ? self.XrefState.GetLocation(number) : -1;

    /// <summary>Gets the catalog the trailer's /Root names, or finds one when /Root names no dictionary (a stream's dictionary is not a catalog).</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The catalog, or <see langword="null"/> when the file has none.</returns>
    internal static PdfDictionary? FindCatalog(PdfObjectStore self)
    {
        var root = self.Trailer.Get(KnownName.Root);
        return root.Kind == PdfKind.Dictionary ? root.AsDictionary() : StoreRepairs.RebuildCatalog(self) ?? root.AsDictionary();
    }

    /// <summary>
    /// Finds the catalog when the trailer's /Root does not lead to one: the newest <c>/Type /Catalog</c> object, else a new
    /// catalog over the root of the page tree.
    /// </summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <returns>The catalog, or <see langword="null"/> when the file has neither.</returns>
    internal static PdfDictionary? RebuildCatalog(PdfObjectStore self)
    {
        if (self.Context is { Recovery: false })
        {
            return null;
        }

        PdfObjectId pagesRoot = default;
        for (var number = self.Size - 1; number > 0; number--)
        {
            PdfOpenContext.ThrowIfCancelled(self.Context);
            var id = new PdfObjectId(number, 0);
            var value = StoreReading.GetObject(self, id);
            if (value.Kind != PdfKind.Dictionary || value.AsDictionary() is not { } candidate)
            {
                continue;
            }

            if (candidate.IsName(KnownName.Type, KnownName.Catalog))
            {
                return StoreRepairs.UseCatalog(self, id, candidate);
            }

            if (!pagesRoot.IsValid && candidate.IsName(KnownName.Type, KnownName.Pages) && !candidate.ContainsKey(KnownName.Parent))
            {
                pagesRoot = id;
            }
        }

        return pagesRoot.IsValid ? StoreRepairs.UseCatalog(self, StoreRepairs.MakeCatalog(self, pagesRoot)) : null;
    }

    /// <summary>Makes a catalog over a page tree root.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "pagesRoot">The page tree root.</param>
    /// <returns>The new catalog's id.</returns>
    internal static PdfObjectId MakeCatalog(PdfObjectStore self, PdfObjectId pagesRoot)
    {
        var catalog = new PdfDictionary(self);
        catalog.Add(KnownName.Type, PdfValue.FromName(KnownName.Catalog));
        catalog.Add(KnownName.Pages, PdfValue.FromReference(pagesRoot));
        return StoreEditing.Add(self, PdfValue.FromDictionary(catalog));
    }

    /// <summary>Points the trailer at a catalog made by <see cref = "StoreRepairs.MakeCatalog"/>.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "id">The new catalog's id.</param>
    /// <returns>The catalog.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary UseCatalog(PdfObjectStore self, PdfObjectId id) => StoreRepairs.UseCatalog(self, id, StoreReading.GetObject(self, id).AsDictionary()!);

    /// <summary>Sets /Root in the trailer to a catalog and reports the repair.</summary>
    /// <param name = "self">The owned object-store state.</param>
    /// <param name = "id">The id of the catalog the trailer should name.</param>
    /// <param name = "catalog">The catalog object itself.</param>
    /// <returns>The same catalog object.</returns>
    internal static PdfDictionary UseCatalog(PdfObjectStore self, PdfObjectId id, PdfDictionary catalog)
    {
        self.Trailer.Set(KnownName.Root, PdfValue.FromReference(id));
        PdfOpenContext.Report(self.Context, PdfDiagnosticCode.CatalogRebuilt, "The catalog was missing and was found by scanning.", id.Number, -1);
        return catalog;
    }
}

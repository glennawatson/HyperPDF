// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Removes the catalog entries ISO 32000-2 deprecates, or refuses when removing one would change the pages: /NeedsRendering
/// (7.7.2), the XFA and /NeedAppearances entries of the form (12.7.3, Annex K), alternate presentations (7.7.4, 13.5),
/// the Web Capture information (14.10), the prepress viewer preferences (12.2), the tag suspects flag (14.7.1) and the
/// deprecated counts of the legal attestations (12.8.7).
/// </summary>
internal static class Pdf20ConversionCatalog
{
    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(Pdf20ConversionSession session)
    {
        var store = session.Store;
        var names = session.Names;
        var catalog = store.Catalog.Clone();
        var changed = FixNeedsRendering(session, catalog);
        changed |= RemoveUsageRights(session, catalog);
        changed |= Pdf20ConversionForms.Apply(session, catalog);
        changed |= RemoveFromChild(session, catalog, KnownName.Names, "7.7.4", "Removed the slide show (alternate presentations), which PDF 2.0 deprecates.", names.AlternatePresentations);
        changed |= RemoveAll(session, catalog, "14.10", "Removed the Web Capture information, which PDF 2.0 deprecates.", names.SpiderInfo);
        changed |= RemoveFromChild(
            session,
            catalog,
            KnownName.ViewerPreferences,
            "12.2",
            "Removed the preferred display and print areas from the viewer preferences, which PDF 2.0 deprecates.",
            names.ViewArea,
            names.ViewClip,
            names.PrintArea,
            names.PrintClip);
        changed |= RemoveFromChild(session, catalog, KnownName.MarkInfo, "14.7.1", "Removed the tag suspects flag, which PDF 2.0 deprecates.", names.Suspects);
        changed |= RemoveFromChild(
            session,
            catalog,
            KnownName.Legal,
            "12.8.7",
            "Removed the counts of movie actions, sound actions and external OPI dictionaries from the legal attestations, which PDF 2.0 deprecates.",
            names.MovieActions,
            names.SoundActions,
            names.ExternalOpiDictionaries);
        if (changed)
        {
            PdfAConversionRewriter.ReplaceCatalog(store, catalog);
        }
    }

    /// <summary>
    /// Determines whether the permissions dictionary holds only usage rights signatures (/UR and /UR3), which PDF 2.0 deprecates
    /// (ISO 32000-2 12.8.2.3), and no certification signature.
    /// </summary>
    /// <param name="permissions">The catalog's /Perms dictionary.</param>
    /// <returns><see langword="true"/> when every entry is a usage rights signature.</returns>
    internal static bool HoldsOnlyUsageRights(PdfDictionary permissions)
    {
        if (permissions.Owner is not { } store)
        {
            return false;
        }

        for (var i = 0; i < permissions.Count; i++)
        {
            if (store.Names.GetString(permissions.GetKeyAt(i)) is not ("UR" or "UR3"))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Removes the usage rights signature that Reader extensions add, which any change would invalidate.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    private static bool RemoveUsageRights(Pdf20ConversionSession session, PdfDictionary catalog)
    {
        if (catalog.GetDictionary(KnownName.Perms) is not { } permissions || !HoldsOnlyUsageRights(permissions))
        {
            return false;
        }

        _ = catalog.Remove(KnownName.Perms);
        session.Changed("12.8.2.3", "Removed the usage rights signature that Reader extensions add, which PDF 2.0 deprecates. Any change to the file would have invalidated it.");
        return true;
    }

    /// <summary>Refuses a file whose pages are drawn from XFA, and removes a /NeedsRendering that is false.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    private static bool FixNeedsRendering(Pdf20ConversionSession session, PdfDictionary catalog)
    {
        if (!catalog.ContainsKey(KnownName.NeedsRendering))
        {
            return false;
        }

        if (catalog.GetBoolean(KnownName.NeedsRendering))
        {
            session.Refuse("7.7.2", "The pages of this document are drawn from its XFA form when it is opened, and PDF 2.0 deprecates XFA, so the copy would show no pages.");
            return false;
        }

        _ = catalog.Remove(KnownName.NeedsRendering);
        session.Changed("7.7.2", "Removed the request to draw the pages from XFA, which PDF 2.0 deprecates. It was off, so the pages are unchanged.");
        return true;
    }

    /// <summary>Removes entries from a dictionary the catalog holds, replacing it with a copy.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <param name="child">The catalog key of the dictionary.</param>
    /// <param name="clause">The clause behind the change.</param>
    /// <param name="description">The change, in plain words.</param>
    /// <param name="keys">The keys to remove from it.</param>
    /// <returns><see langword="true"/> when an entry was there.</returns>
    private static bool RemoveFromChild(Pdf20ConversionSession session, PdfDictionary catalog, PdfName child, string clause, string description, params ReadOnlySpan<PdfName> keys)
    {
        if (catalog.GetDictionary(child) is not { } original)
        {
            return false;
        }

        var copy = original.Clone();
        if (!RemoveAll(session, copy, clause, description, keys))
        {
            return false;
        }

        catalog.Set(child, PdfValue.FromDictionary(copy));
        return true;
    }

    /// <summary>Removes keys from a dictionary and records the change once when any was there.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary copy.</param>
    /// <param name="clause">The clause behind the change.</param>
    /// <param name="description">The change, in plain words.</param>
    /// <param name="keys">The keys.</param>
    /// <returns><see langword="true"/> when an entry was there.</returns>
    private static bool RemoveAll(Pdf20ConversionSession session, PdfDictionary dictionary, string clause, string description, params ReadOnlySpan<PdfName> keys)
    {
        var removed = false;
        foreach (var key in keys)
        {
            removed |= dictionary.Remove(key);
        }

        if (removed)
        {
            session.Changed(clause, description);
        }

        return removed;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes the catalog meet PDF/A: removes /Requirements, /NeedsRendering, /Perms entries other than signatures,
/// document JavaScript and alternate presentations, checks the interactive form, and asks for a structure tree at
/// level A (ISO 19005-1 §6.6, §6.8, §6.9; ISO 19005-2 and -3 §6.1.12, §6.4, §6.7; ISO 19005-4:2020 §6.1.13, §6.4, §6.9).
/// </summary>
internal static class PdfAConversionCatalog
{
    /// <summary>Applies the fixes.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var store = session.Store;
        var names = store.Names;
        var catalog = store.Catalog.Clone();
        var changed = Remove(session, catalog, names.Intern("Requirements"u8), "Removed the viewer requirements list.", PdfARule.Requirements);
        changed |= Remove(session, catalog, KnownName.NeedsRendering, "Removed the request to render the file from XFA.", PdfARule.NeedsRendering);
        changed |= Remove(session, catalog, KnownName.Perms, "Removed the usage rights entry, which PDF/A limits to signatures.", PdfARule.Permissions, PdfARule.PermissionsPdfA4);
        changed |= FixNames(session, catalog);
        changed |= PdfAConversionForms.Apply(session, catalog);
        changed |= PdfAConversionLayers.Apply(session, catalog);
        changed |= MarkStructure(session, catalog);
        if (changed)
        {
            PdfAConversionRewriter.ReplaceCatalog(store, catalog);
        }
    }

    /// <summary>Removes a catalog entry and records the change.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <param name="key">The key.</param>
    /// <param name="description">The change, in plain words.</param>
    /// <param name="rules">The rules it answers.</param>
    /// <returns><see langword="true"/> when the entry was there.</returns>
    private static bool Remove(PdfAConversionSession session, PdfDictionary catalog, PdfName key, string description, params ReadOnlySpan<PdfARule> rules)
    {
        if (!catalog.Remove(key))
        {
            return false;
        }

        session.Changed(description, rules);
        return true;
    }

    /// <summary>Removes document JavaScript (parts 1 to 3) and alternate presentations from the name dictionary.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    private static bool FixNames(PdfAConversionSession session, PdfDictionary catalog)
    {
        if (catalog.GetDictionary(KnownName.Names) is not { } original)
        {
            return false;
        }

        var names = original.Clone();
        var changed = session.Part < PdfAClaim.Part4
            && Remove(session, names, KnownName.JavaScript, "Removed the document's JavaScript.", PdfARule.ActionTypePdfA2, PdfARule.ActionTypePdfA1);
        changed |= Remove(session, names, session.Store.Names.Intern("AlternatePresentations"u8), "Removed the slide show presentation.", PdfARule.AlternatePresentations);
        if (changed)
        {
            catalog.Set(KnownName.Names, PdfValue.FromDictionary(names));
        }

        return changed;
    }

    /// <summary>
    /// For level A, checks the document has a structure tree and marks it as tagged (ISO 19005-1 §6.8.2.2; ISO 19005-2
    /// and -3 §6.7.2.2). Tags are never invented: an untagged document cannot reach level A.
    /// </summary>
    /// <param name="session">The conversion.</param>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns><see langword="true"/> when the catalog changed.</returns>
    private static bool MarkStructure(PdfAConversionSession session, PdfDictionary catalog)
    {
        if (PdfALevels.Conformance(session.Level) != "A")
        {
            return false;
        }

        if (catalog.GetDictionary(KnownName.StructTreeRoot) is null)
        {
            session.Refuse("Level A needs a tagged document, and this document has no tags. Choose level B or U instead.", PdfARule.StructureTreeRoot);
            return false;
        }

        var marked = session.Store.Names.Intern("Marked"u8);
        if (catalog.GetDictionary(KnownName.MarkInfo) is { } info && info.Get(marked).AsBoolean())
        {
            return false;
        }

        var markInfo = catalog.GetDictionary(KnownName.MarkInfo)?.Clone() ?? new PdfDictionary(session.Store, 1);
        markInfo.Set(marked, PdfValue.FromBoolean(true));
        catalog.Set(KnownName.MarkInfo, PdfValue.FromDictionary(markInfo));
        session.Changed("Marked the document as tagged, as it has a structure tree.", PdfARule.MarkInfoMarked);
        return true;
    }
}

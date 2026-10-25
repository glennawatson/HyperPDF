// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Composition;

/// <summary>Writes what the catalog needs once every page is written: the named destinations that links jump to and the outline.</summary>
internal static class CatalogWriter
{
    /// <summary>The entries of the catalog's names dictionary.</summary>
    private const int NamesEntries = 1;

    /// <summary>Writes the named destinations and the outline.</summary>
    /// <param name="state">The document being written, with every page's destinations.</param>
    /// <param name="env">The document and its outline entries.</param>
    /// <param name="plans">The pages, for their sizes.</param>
    internal static void Write(RenderState state, LayoutEnv env, List<PagePlan> plans)
    {
        var builder = state.Creator.Builder;
        var destinations = new Dictionary<string, DestinationPoint>(state.Destinations, StringComparer.Ordinal);
        AddPageDestinations(state, plans, destinations, env);
        foreach (var name in state.LinkedBookmarks)
        {
            if (!destinations.ContainsKey(name))
            {
                env.Warnings.Add($"A link goes to the bookmark '{name}', which does not exist.");
            }
        }

        var catalog = builder.CatalogEntries;
        if (destinations.Count > 0)
        {
            var names = new PdfDictionary(builder.Owner, NamesEntries);
            names.Set(KnownName.Dests, PdfValue.FromReference(NameTreeWriter.Write(builder, destinations, state.PageIds)));
            catalog.Set(KnownName.Names, PdfValue.FromDictionary(names));
        }

        var outline = OutlineWriter.Write(builder, env.Outline, destinations, state.PageIds);
        if (!outline.IsValid)
        {
            return;
        }

        catalog.Set(KnownName.Outlines, PdfValue.FromReference(outline));
        catalog.Set(builder.Names.Intern("PageMode"), PdfValue.FromName(builder.Names.Intern("UseOutlines")));
    }

    /// <summary>Adds a destination for each page that a link goes to by number.</summary>
    /// <param name="state">The document being written.</param>
    /// <param name="plans">The pages.</param>
    /// <param name="destinations">The destinations, which get the pages added.</param>
    /// <param name="env">The document, which collects notes.</param>
    private static void AddPageDestinations(RenderState state, List<PagePlan> plans, Dictionary<string, DestinationPoint> destinations, LayoutEnv env)
    {
        foreach (var page in state.LinkedPages)
        {
            if (page > plans.Count)
            {
                env.Warnings.Add($"A link goes to page {page}, but the document has only {plans.Count} pages.");
                continue;
            }

            destinations[LinkAnnotations.GetPageName(page)] = new(page, 0F, plans[page - 1].Section.Setup.PageHeight);
        }
    }
}

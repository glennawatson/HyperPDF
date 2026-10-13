// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Keeps the separation dictionaries (<c>/SeparationInfo</c>, ISO 32000-2 §14.11.4) of copied or remaining pages valid.
/// Each page that stays linked gets a new dictionary whose <c>/Pages</c> array lists exactly the linked pages of its
/// printed page in the output, so every member's array is identical and names only output pages. A repeated copy, a page
/// without a colourant and a damaged dictionary lose the entry. A colour space whose colourant does not match is left
/// out. What could not be kept whole is recorded in a <see cref="PdfPageEditReport"/>.
/// </summary>
internal static class PdfCarrySeparations
{
    /// <summary>Plans the separation dictionaries of pages being copied.</summary>
    /// <param name="source">The document copied from.</param>
    /// <param name="pages">The source pages in output order.</param>
    /// <param name="report">Receives what could not be kept, or <see langword="null"/>.</param>
    /// <returns>The plan, or <see langword="null"/> when no copied page has a separation dictionary.</returns>
    internal static SeparationCopyPlan? Plan(PdfDocument source, ReadOnlySpan<PdfPage> pages, PdfPageEditReport? report)
    {
        var key = SeparationReader.InfoKey(source.Objects.Names);
        if (!PdfCarrySeparations.AnyHasKey(pages, key))
        {
            return null;
        }

        var info = PdfDocumentSeparations.GetSeparations(source);
        var groups = PdfCarrySeparations.GroupsByPage(info);
        var first = PdfCarrySeparations.FirstCopies(pages, groups);
        var plan = new SeparationCopyPlan(new int[]?[pages.Length], new PdfSeparationPage?[pages.Length], new PdfDictionary?[pages.Length], SeparationReader.ColorantKey(source.Objects.Names));
        var carried = new Dictionary<int, int[]>();
        var unlinked = new Dictionary<int, int>();
        var damaged = 0;
        for (var i = 0; i < pages.Length; i++)
        {
            var index = pages[i].Index;
            if (!groups.TryGetValue(index, out var group))
            {
                damaged += pages[i].Dictionary.ContainsKey(key) ? 1 : 0;
                continue;
            }

            var linked = first.GetValueOrDefault(index, -1) == i;
            CollectionsMarshal.GetValueRefOrAddDefault(unlinked, group.Index, out _) += linked ? 0 : 1;
            if (!linked)
            {
                continue;
            }

            var separation = group.Separations[PdfDocumentSeparations.IndexOf(group, index)];
            PdfCarrySeparations.Link(plan, i, separation, PdfCarrySeparations.CarriedOnce(carried, group, first), pages[i].Dictionary.GetDictionary(key));
        }

        PdfCarrySeparations.ReportCopy(report, info, carried, unlinked, damaged);
        return plan;
    }

    /// <summary>Builds a copied page's separation dictionary.</summary>
    /// <param name="plan">The plan, or <see langword="null"/>.</param>
    /// <param name="position">The page's output position.</param>
    /// <param name="sink">The page copier.</param>
    /// <param name="ids">The output page objects, by position.</param>
    /// <returns>The dictionary, or null when the page gets none.</returns>
    internal static PdfValue Build(SeparationCopyPlan? plan, int position, IPdfCarrySink sink, ReadOnlySpan<PdfObjectId> ids)
    {
        if (plan?.Links[position] is not { } links || plan.Sources[position] is not { } source || plan.Pages[position] is not { } page)
        {
            return default;
        }

        var target = sink.TargetStore;
        var members = new PdfArray(target, links.Length);
        foreach (var link in links)
        {
            members.Add(PdfValue.FromReference(ids[link]));
        }

        var dictionary = new PdfDictionary(target);
        dictionary.Set(KnownName.Pages, PdfValue.FromArray(members));
        dictionary.Set(SeparationReader.ColorantKey(sink.TargetNames), sink.Import(source.Get(plan.ColorantKey)));
        if (PdfCarrySeparations.KeepsSpace(page))
        {
            dictionary.Set(KnownName.ColorSpace, sink.Import(source.GetRaw(KnownName.ColorSpace)));
        }

        return PdfValue.FromDictionary(dictionary);
    }

    /// <summary>
    /// Relinks the separation pages that remain after a deletion: each printed page that lost a separation keeps the
    /// remaining ones, linked to each other.
    /// </summary>
    /// <param name="document">The document, before its page tree is written again.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="removed">The deleted page indexes.</param>
    /// <param name="report">Receives the printed pages that lost a separation, or <see langword="null"/>.</param>
    internal static void RelinkAfterDelete(PdfDocument document, PdfEditTransaction transaction, HashSet<int> removed, PdfPageEditReport? report)
    {
        var info = PdfDocumentSeparations.GetSeparations(document);
        if (info.Groups.Length == 0)
        {
            return;
        }

        var key = SeparationReader.InfoKey(document.Objects.Names);
        var colorantKey = SeparationReader.ColorantKey(document.Objects.Names);
        foreach (var group in info.Groups)
        {
            var kept = PdfCarrySeparations.Remaining(document, group, removed);

            // A printed page deleted whole, or not touched, needs nothing.
            if (kept.Count == 0 || kept.Count == group.Separations.Length)
            {
                continue;
            }

            var unlinked = PdfCarrySeparations.Relink(transaction, kept, key, colorantKey);
            report?.Add(new(group.Index, group.Separations.Length, kept.Count - unlinked, unlinked));
        }
    }

    /// <summary>Determines whether a page's colour space can be kept: it is valid and names the page's colourant.</summary>
    /// <param name="page">The separation page.</param>
    /// <returns><see langword="true"/> when it is written.</returns>
    private static bool KeepsSpace(PdfSeparationPage page) =>
        page.SpaceKind != PdfSeparationSpaceKind.None && Array.IndexOf(page.SpaceColorants, page.Colorant) >= 0;

    /// <summary>Determines whether any page has a separation dictionary.</summary>
    /// <param name="pages">The pages.</param>
    /// <param name="key">The <c>/SeparationInfo</c> name.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool AnyHasKey(ReadOnlySpan<PdfPage> pages, PdfName key)
    {
        foreach (var page in pages)
        {
            if (page.Dictionary.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Indexes the groups by the pages in them.</summary>
    /// <param name="info">The groups.</param>
    /// <returns>Each separation page's group.</returns>
    private static Dictionary<int, PdfSeparationGroup> GroupsByPage(PdfSeparationInfo info)
    {
        var groups = new Dictionary<int, PdfSeparationGroup>();
        foreach (var group in info.Groups)
        {
            foreach (var separation in group.Separations)
            {
                groups[separation.PageIndex] = group;
            }
        }

        return groups;
    }

    /// <summary>Finds the first output position of each separation page that can stay linked.</summary>
    /// <param name="pages">The source pages in output order.</param>
    /// <param name="groups">Each separation page's group.</param>
    /// <returns>The output position by source page index.</returns>
    private static Dictionary<int, int> FirstCopies(ReadOnlySpan<PdfPage> pages, Dictionary<int, PdfSeparationGroup> groups)
    {
        var first = new Dictionary<int, int>();
        for (var i = 0; i < pages.Length; i++)
        {
            var index = pages[i].Index;
            if (groups.TryGetValue(index, out var group) && group.Separations[PdfDocumentSeparations.IndexOf(group, index)].Colorant.Length > 0)
            {
                _ = first.TryAdd(index, i);
            }
        }

        return first;
    }

    /// <summary>Gets the output positions of a group's linked pages, in the group's order.</summary>
    /// <param name="group">The group.</param>
    /// <param name="first">The output position by source page index.</param>
    /// <returns>The positions.</returns>
    private static int[] Carried(PdfSeparationGroup group, Dictionary<int, int> first)
    {
        var links = new List<int>(group.Separations.Length);
        foreach (var separation in group.Separations)
        {
            if (first.TryGetValue(separation.PageIndex, out var position))
            {
                links.Add(position);
            }
        }

        return [.. links];
    }

    /// <summary>Gets the linked output positions of a group, working them out the first time.</summary>
    /// <param name="carried">The positions worked out so far, by group.</param>
    /// <param name="group">The group.</param>
    /// <param name="first">The output position by source page index.</param>
    /// <returns>The positions.</returns>
    private static int[] CarriedOnce(Dictionary<int, int[]> carried, PdfSeparationGroup group, Dictionary<int, int> first)
    {
        ref var links = ref CollectionsMarshal.GetValueRefOrAddDefault(carried, group.Index, out _);
        links ??= PdfCarrySeparations.Carried(group, first);
        return links;
    }

    /// <summary>Plans a linked copied page.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="position">The page's output position.</param>
    /// <param name="page">The page's separation details.</param>
    /// <param name="links">The output positions of the group's linked pages.</param>
    /// <param name="source">The page's source separation dictionary.</param>
    private static void Link(SeparationCopyPlan plan, int position, PdfSeparationPage page, int[] links, PdfDictionary? source)
    {
        plan.Links[position] = links;
        plan.Pages[position] = page;
        plan.Sources[position] = source;
    }

    /// <summary>Records the groups of a copy that were not carried whole.</summary>
    /// <param name="report">The report, or <see langword="null"/>.</param>
    /// <param name="info">The source groups.</param>
    /// <param name="carried">The linked output positions by group.</param>
    /// <param name="unlinked">The unlinked counts of every group touched.</param>
    /// <param name="damaged">The copied pages whose separation dictionary is in no group.</param>
    private static void ReportCopy(PdfPageEditReport? report, PdfSeparationInfo info, Dictionary<int, int[]> carried, Dictionary<int, int> unlinked, int damaged)
    {
        if (report is null)
        {
            return;
        }

        foreach (var group in info.Groups)
        {
            if (!unlinked.TryGetValue(group.Index, out var count))
            {
                continue;
            }

            var kept = carried.TryGetValue(group.Index, out var links) ? links.Length : 0;
            if (kept < group.Separations.Length || count > 0)
            {
                report.Add(new(group.Index, group.Separations.Length, kept, count));
            }
        }

        if (damaged > 0)
        {
            report.Add(new(-1, 0, 0, damaged));
        }
    }

    /// <summary>Gets the pages of a group that a deletion leaves.</summary>
    /// <param name="document">The document.</param>
    /// <param name="group">The group.</param>
    /// <param name="removed">The deleted page indexes.</param>
    /// <returns>The remaining pages with their separation details.</returns>
    private static List<RemainingSeparation> Remaining(PdfDocument document, PdfSeparationGroup group, HashSet<int> removed)
    {
        var kept = new List<RemainingSeparation>(group.Separations.Length);
        foreach (var separation in group.Separations)
        {
            if (!removed.Contains(separation.PageIndex))
            {
                kept.Add(new(PdfDocumentPages.GetPage(document, separation.PageIndex), separation));
            }
        }

        return kept;
    }

    /// <summary>Gives the remaining pages of a printed page dictionaries that list only each other.</summary>
    /// <param name="transaction">The edit.</param>
    /// <param name="kept">The remaining pages.</param>
    /// <param name="key">The <c>/SeparationInfo</c> name.</param>
    /// <param name="colorantKey">The <c>/DeviceColorant</c> name.</param>
    /// <returns>The pages whose dictionary was removed because they have no colourant.</returns>
    private static int Relink(PdfEditTransaction transaction, List<RemainingSeparation> kept, PdfName key, PdfName colorantKey)
    {
        var store = kept.Count > 0 ? kept[0].Page.Dictionary.Owner : null;
        var members = new PdfArray(store, kept.Count);
        foreach (var remaining in kept)
        {
            if (remaining.Separation.Colorant.Length > 0)
            {
                members.Add(PdfValue.FromReference(remaining.Page.Id));
            }
        }

        var unlinked = 0;
        foreach (var remaining in kept)
        {
            var page = remaining.Page;
            var copy = transaction.CloneDictionary(page.Id) ?? page.Dictionary.Clone();
            if (remaining.Separation.Colorant.Length == 0 || copy.GetDictionary(key) is not { } info)
            {
                _ = copy.Remove(key);
                unlinked++;
            }
            else
            {
                copy.Set(key, PdfValue.FromDictionary(PdfCarrySeparations.Rewrite(info, members, colorantKey, remaining.Separation)));
            }

            transaction.Replace(page.Id, PdfValue.FromDictionary(copy));
        }

        return unlinked;
    }

    /// <summary>Makes a remaining page's new separation dictionary in the same document.</summary>
    /// <param name="info">The page's current dictionary.</param>
    /// <param name="members">The remaining linked pages.</param>
    /// <param name="colorantKey">The <c>/DeviceColorant</c> name.</param>
    /// <param name="separation">The page's separation details.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Rewrite(PdfDictionary info, PdfArray members, PdfName colorantKey, PdfSeparationPage separation)
    {
        var dictionary = new PdfDictionary(info.Owner);
        dictionary.Set(KnownName.Pages, PdfValue.FromArray(members.Clone()));
        dictionary.Set(colorantKey, info.GetRaw(colorantKey));
        if (PdfCarrySeparations.KeepsSpace(separation))
        {
            dictionary.Set(KnownName.ColorSpace, info.GetRaw(KnownName.ColorSpace));
        }

        return dictionary;
    }
}

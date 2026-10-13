// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Decides whether a copied page keeps its trap network (ISO 32000-2 §14.11.6.2). A copy renumbers every object the
/// network's <c>/Version</c> names along with the page, so a network that matches its page in the source still matches
/// the copy. It is carried, fixed when its flags, position or appearance state are wrong. A network that no longer
/// matches the source page, that cannot be checked, or that names objects a copy leaves out, such as pages, is left out
/// of the copy and reported.
/// </summary>
internal static class PdfTrapNetworkCopy
{
    /// <summary>Checks the trap network of a page being copied.</summary>
    /// <param name="sink">The copier, which can copy a fixed network in place of the source's.</param>
    /// <param name="page">The source page.</param>
    /// <param name="source">The source page's <c>/Annots</c> array.</param>
    /// <param name="prepared">The values the copy writes as its <c>/Annots</c>, in the source's order.</param>
    /// <param name="report">Receives what was done, or <see langword="null"/>.</param>
    /// <returns>The values to copy: <paramref name="prepared"/> itself when the network is carried unchanged.</returns>
    internal static PdfArray Prepare(IPdfCarrySink sink, PdfPage page, PdfArray source, PdfArray prepared, PdfPageEditReport? report)
    {
        var store = sink.Source;
        var count = PdfTrapNetworkReader.Find(store, source, out var index);
        if (count == 0 || PdfTrapNetworkReader.Find(store, prepared, out var position) == 0)
        {
            return prepared;
        }

        var network = new PdfTrapNetworkPage(store, page.Index, page.Id, page.Dictionary, source, index, count);
        var check = PdfTrapNetworkValidator.Check(network, new(store));
        var problems = check.Problems | Lost(store, network.Network) | Dropped(store, source, prepared);
        if (problems == PdfTrapNetworkProblems.None)
        {
            return prepared;
        }

        if ((problems & PdfTrapNetworkCheck.RemovalProblems) != 0)
        {
            report?.AddTrapNetwork(new(page.Index, PdfTrapNetworkAction.LeftOut, problems));
            return PdfTrapNetworkReader.Without(store, prepared);
        }

        report?.AddTrapNetwork(new(page.Index, PdfTrapNetworkAction.Repaired, problems));
        return Repair(sink, prepared, position, problems);
    }

    /// <summary>Copies a fixed network in place of the source's, last in the copied array.</summary>
    /// <param name="sink">The copier.</param>
    /// <param name="prepared">The values the copy writes as its <c>/Annots</c>.</param>
    /// <param name="position">The network's index in <paramref name="prepared"/>.</param>
    /// <param name="problems">What is wrong with the network.</param>
    /// <returns>The values to copy.</returns>
    private static PdfArray Repair(IPdfCarrySink sink, PdfArray prepared, int position, PdfTrapNetworkProblems problems)
    {
        var raw = prepared.GetRaw(position);
        var entry = raw;
        if (PdfTrapNetworkFix.Repaired(sink.Source, PdfTrapNetworkReader.Get(sink.Source, prepared, position)!, problems) is { } repaired)
        {
            if (raw.IsReference)
            {
                // The copy stays an indirect object; the copier writes the fixed dictionary under its new number.
                sink.Override(raw.AsReference().Number, repaired, false);
            }
            else
            {
                entry = PdfValue.FromDictionary(repaired);
            }
        }

        return (problems & PdfTrapNetworkProblems.NotLast) == 0 && raw.IsReference ? prepared : PdfTrapNetworkReader.MovedLast(prepared, position, entry);
    }

    /// <summary>Determines whether the network's <c>/Version</c> names a page or page tree node, which a copy leaves out.</summary>
    /// <param name="store">The source objects.</param>
    /// <param name="network">The network annotation.</param>
    /// <returns><see cref="PdfTrapNetworkProblems.ReferencesLost"/> when it does.</returns>
    private static PdfTrapNetworkProblems Lost(PdfObjectStore store, PdfDictionary network)
    {
        if (network.Get(KnownName.Version).AsArray() is not { } version)
        {
            return PdfTrapNetworkProblems.None;
        }

        foreach (var item in version.Items)
        {
            if (item.IsReference && StoreReading.Resolve(store, item).AsDictionary() is { } target
                && (target.IsName(KnownName.Type, KnownName.Page) || target.IsName(KnownName.Type, KnownName.Pages)))
            {
                return PdfTrapNetworkProblems.ReferencesLost;
            }
        }

        return PdfTrapNetworkProblems.None;
    }

    /// <summary>Determines whether the copy leaves out some of the page's other annotations, which changes what the traps were made for.</summary>
    /// <param name="store">The source objects.</param>
    /// <param name="source">The source page's <c>/Annots</c> array.</param>
    /// <param name="prepared">The values the copy writes.</param>
    /// <returns><see cref="PdfTrapNetworkProblems.AnnotationsChanged"/> when it does.</returns>
    private static PdfTrapNetworkProblems Dropped(PdfObjectStore store, PdfArray source, PdfArray prepared) =>
        Others(store, source) == Others(store, prepared) ? PdfTrapNetworkProblems.None : PdfTrapNetworkProblems.AnnotationsChanged;

    /// <summary>Counts the annotations of an array other than trap networks.</summary>
    /// <param name="store">The objects the entries resolve in.</param>
    /// <param name="annotations">The array.</param>
    /// <returns>The count.</returns>
    private static int Others(PdfObjectStore store, PdfArray annotations)
    {
        var count = 0;
        for (var i = 0; i < annotations.Count; i++)
        {
            if (PdfTrapNetworkReader.Get(store, annotations, i) is { } annotation && !PdfTrapNetworkReader.IsNetwork(annotation))
            {
                count++;
            }
        }

        return count;
    }
}

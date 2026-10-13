// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Removes a page's trap networks, or fixes a network that still matches its page: only the Print and ReadOnly flags,
/// last in <c>/Annots</c>, and an <c>/AS</c> that names its appearance. Every change goes through a transaction, so it can
/// be undone.
/// </summary>
internal static class PdfTrapNetworkFix
{
    /// <summary>Gets the appearance state given to a network's only appearance when it had no <c>/AS</c>.</summary>
    private static ReadOnlySpan<byte> StateName => "Traps"u8;

    /// <summary>Removes or fixes a page's network.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="page">The page.</param>
    /// <param name="check">What is wrong with the network.</param>
    /// <returns>What was done.</returns>
    internal static PdfTrapNetworkAction Apply(PdfEditTransaction transaction, PdfTrapNetworkPage page, in PdfTrapNetworkCheck check)
    {
        if (check.MustRemove)
        {
            return Publish(transaction, page, PdfTrapNetworkReader.Without(page.Store, page.Annotations)) ? PdfTrapNetworkAction.Removed : PdfTrapNetworkAction.Unchanged;
        }

        var raw = page.Annotations.GetRaw(page.NetworkIndex);
        var entry = raw;
        if (Repaired(page.Store, page.Network, check.Problems) is { } repaired)
        {
            if (raw.IsReference)
            {
                transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(repaired));
            }
            else
            {
                entry = PdfValue.FromDictionary(repaired);
            }
        }

        if ((check.Problems & PdfTrapNetworkProblems.NotLast) == 0 && raw.IsReference)
        {
            return PdfTrapNetworkAction.Repaired;
        }

        return Publish(transaction, page, PdfTrapNetworkReader.MovedLast(page.Annotations, page.NetworkIndex, entry)) ? PdfTrapNetworkAction.Repaired : PdfTrapNetworkAction.Unchanged;
    }

    /// <summary>Makes a fixed copy of a network that has the wrong flags or no appearance state.</summary>
    /// <param name="store">The objects the network belongs to; the new state name is added to their names.</param>
    /// <param name="network">The network annotation.</param>
    /// <param name="problems">What is wrong with it.</param>
    /// <returns>The copy, or <see langword="null"/> when only its position needs fixing.</returns>
    internal static PdfDictionary? Repaired(PdfObjectStore store, PdfDictionary network, PdfTrapNetworkProblems problems)
    {
        if ((problems & (PdfTrapNetworkProblems.WrongFlags | PdfTrapNetworkProblems.MissingAppearanceState)) == 0)
        {
            return null;
        }

        var copy = network.Clone();
        if ((problems & PdfTrapNetworkProblems.WrongFlags) != 0)
        {
            copy.Set(KnownName.F, PdfValue.FromInteger(PdfTrapNetworkValidator.RequiredFlags));
        }

        if ((problems & PdfTrapNetworkProblems.MissingAppearanceState) != 0 && network.GetDictionary(KnownName.AP) is { } appearances)
        {
            // A single appearance moves into a subdictionary so /AS has an appearance to name (ISO 32000-2 §12.5.5).
            var name = store.Names.Intern(StateName);
            var states = new PdfDictionary(store);
            states.Set(name, appearances.GetRaw(KnownName.N));
            var dictionary = appearances.Clone();
            dictionary.Set(KnownName.N, PdfValue.FromDictionary(states));
            copy.Set(KnownName.AP, PdfValue.FromDictionary(dictionary));
            copy.Set(KnownName.AS, PdfValue.FromName(name));
        }

        return copy;
    }

    /// <summary>Puts a new annotation array on the page: into its own object when it has one, otherwise into a copy of the page.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotations">The new array.</param>
    /// <returns><see langword="true"/> when put in place; <see langword="false"/> for a page that is not an object of its own.</returns>
    private static bool Publish(PdfEditTransaction transaction, PdfTrapNetworkPage page, PdfArray annotations)
    {
        var raw = page.Page.GetRaw(KnownName.Annots);
        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromArray(annotations));
            return true;
        }

        if (!page.Id.IsValid)
        {
            return false;
        }

        var copy = transaction.CloneDictionary(page.Id) ?? page.Page.Clone();
        if (annotations.Count == 0)
        {
            _ = copy.Remove(KnownName.Annots);
        }
        else
        {
            copy.Set(KnownName.Annots, PdfValue.FromArray(annotations));
        }

        transaction.Replace(page.Id, PdfValue.FromDictionary(copy));
        return true;
    }
}

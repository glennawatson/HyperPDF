// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks halftone transfer functions, which PDF/A allows only where ISO 32000 needs them: none on a halftone of its own or
/// on a process colourant's halftone inside a type 5 halftone, and one on every spot colourant's halftone there
/// (ISO 19005-2 §6.2.5; ISO 19005-4:2020 §6.2.5). A wrong transfer function cannot be fixed without changing the output,
/// so the conversion stops.
/// </summary>
internal static class PdfAConversionHalftones
{
    /// <summary>The halftone type that holds one halftone per colourant.</summary>
    private const int PerColourant = 5;

    /// <summary>The keys of a type 5 halftone that are not colourants.</summary>
    private static readonly string[] OwnKeys = ["Type", "HalftoneType", "HalftoneName", "TransferFunction"];

    /// <summary>The process colourants, whose halftones take no transfer function.</summary>
    private static readonly string[] ProcessColourants = ["Cyan", "Magenta", "Yellow", "Black"];

    /// <summary>Refuses the conversion when a halftone's transfer function breaks the rule.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Check(PdfAConversionSession session)
    {
        var store = session.Store;
        var halftones = new List<PdfDictionary>();
        var graph = PdfObjectGraph.Collect(store, false, session.CancellationToken);
        for (var number = 1; number <= graph.Count; number++)
        {
            Collect(store, graph.GetValue(number), halftones, 0);
        }

        var nested = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var wrong = 0;
        foreach (var halftone in halftones)
        {
            if (halftone.GetInt32(store.Names.Intern("HalftoneType"u8)) == PerColourant)
            {
                wrong += CheckColourants(store, halftone, nested);
            }
        }

        var transfer = store.Names.Intern("TransferFunction"u8);
        foreach (var halftone in halftones)
        {
            wrong += !nested.Contains(halftone) && halftone.ContainsKey(transfer) ? 1 : 0;
        }

        if (wrong > 0)
        {
            session.Refuse("The document's halftone settings have transfer functions PDF/A does not allow.", PdfARule.HalftoneTransfer);
        }
    }

    /// <summary>Collects the halftone dictionaries in a value, including nested direct ones.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="value">The value.</param>
    /// <param name="halftones">The halftones found.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void Collect(PdfObjectStore store, PdfValue value, List<PdfDictionary> halftones, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            if (dictionary.ContainsKey(store.Names.Intern("HalftoneType"u8)))
            {
                halftones.Add(dictionary);
            }

            for (var i = 0; i < dictionary.Count; i++)
            {
                Collect(store, dictionary.GetValueAt(i), halftones, depth + 1);
            }
        }
        else if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                Collect(store, array.GetRaw(i), halftones, depth + 1);
            }
        }
    }

    /// <summary>Checks the colourant halftones of a type 5 halftone.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="halftone">The type 5 halftone.</param>
    /// <param name="nested">Receives the colourant halftones.</param>
    /// <returns>The number that break the rule.</returns>
    private static int CheckColourants(PdfObjectStore store, PdfDictionary halftone, HashSet<PdfDictionary> nested)
    {
        var transfer = store.Names.Intern("TransferFunction"u8);
        var wrong = 0;
        for (var i = 0; i < halftone.Count; i++)
        {
            var colourant = store.Names.GetString(halftone.GetKeyAt(i));
            if (OwnKeys.Contains(colourant) || StoreReading.Resolve(store, halftone.GetValueAt(i)).AsDictionary() is not { } entry)
            {
                continue;
            }

            _ = nested.Add(entry);
            var needs = colourant != "Default" && !ProcessColourants.Contains(colourant);
            var has = entry.ContainsKey(transfer);
            wrong += colourant == "Default" || needs == has ? 0 : 1;
        }

        return wrong;
    }
}

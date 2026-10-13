// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Finds the deprecated features whose removal would lose content, so the copy is refused: movie, sound and trap network
/// annotations (ISO 32000-2 12.5.6.16, 12.5.6.17, 12.5.6.21), sound and movie actions (12.6.4.9, 12.6.4.10), OPI
/// dictionaries (8.9.5.1, 8.10.2, 14.11.7) and preseparated pages (14.11.4). It also spells the print field check state in
/// its current form (14.8.5.6), which is a move and loses nothing.
/// </summary>
internal static class Pdf20ConversionMediaFixes
{
    /// <summary>Fixes or counts one dictionary.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    internal static void Fix(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        CountAnnotation(tally, dictionary);
        CountAction(tally, dictionary);
        tally.OpiDictionaries += dictionary.ContainsKey(KnownName.OPI) ? 1 : 0;
        tally.Separations += dictionary.ContainsKey(tally.Names.SeparationInfo) && dictionary.IsName(KnownName.Type, KnownName.Page) ? 1 : 0;
        if (dictionary.GetName(KnownName.O) == tally.Names.PrintField)
        {
            FixPrintField(tally, dictionary, ref copy);
        }
    }

    /// <summary>Counts a movie, sound or trap network annotation.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    private static void CountAnnotation(Pdf20ConversionTally tally, PdfDictionary dictionary)
    {
        if (!dictionary.ContainsKey(KnownName.Rect))
        {
            return;
        }

        var subtype = dictionary.Get(KnownName.Subtype).AsName().ToKnownName();
        tally.MovieAnnotations += subtype == KnownName.Movie ? 1 : 0;
        tally.SoundAnnotations += subtype == KnownName.Sound ? 1 : 0;
        tally.TrapNetworks += subtype == KnownName.TrapNet ? 1 : 0;
    }

    /// <summary>Counts a sound or movie action.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    private static void CountAction(Pdf20ConversionTally tally, PdfDictionary dictionary)
    {
        var action = dictionary.Get(KnownName.S).AsName().ToKnownName();
        tally.MediaActions += action is KnownName.Sound or KnownName.Movie ? 1 : 0;
    }

    /// <summary>Spells a print field's check state as ISO 32000-2 14.8.5.6 now does: On, Off or Neutral.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The attribute object.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    private static void FixPrintField(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        var value = dictionary.GetName(tally.Names.Checked);
        if (value.IsNone || dictionary.Owner is not { } store)
        {
            return;
        }

        var spelling = store.Names.GetString(value);
        var current = spelling switch
        {
            "on" => "On",
            "off" => "Off",
            "neutral" => "Neutral",
            _ => null,
        };
        if (current is null)
        {
            return;
        }

        Pdf20ConversionEdit.Set(dictionary, ref copy, tally.Names.Checked, PdfValue.FromName(store.Names.Intern(current)));
        tally.PrintFields++;
    }
}

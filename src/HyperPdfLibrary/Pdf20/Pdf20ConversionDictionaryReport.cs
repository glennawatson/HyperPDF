// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Pdf20;

/// <summary>Words the counts of the dictionary pass for the user: what changed, and what stops the copy.</summary>
internal static class Pdf20ConversionDictionaryReport
{
    /// <summary>Gets the changes, in the order they are listed. A <c>{n}</c> in the text stands for the count.</summary>
    private static Pdf20TallyRule[] Changes { get; } =
    [
        new(static t => t.ResourceNames, "9.6.2.1, 8.9.5.1, 8.10.2", "Removed the names of {n} fonts, images and forms, which PDF 2.0 deprecates."),
        new(static t => t.GlyphSets, "9.8.1, 9.8.3.1", "Removed {n} lists of the glyphs a font holds, which PDF 2.0 deprecates."),
        new(static t => t.ProcSets, "14.2", "Removed {n} procedure set lists, which PDF 2.0 deprecates."),
        new(static t => t.TransferFunctions, "8.4.5", "Removed {n} transfer functions that changed no colour, which PDF 2.0 deprecates."),
        new(static t => t.WebCaptureIds, "14.10", "Removed {n} Web Capture identifiers, which PDF 2.0 deprecates."),
        new(static t => t.BlendModes, "11.3.5", "Replaced {n} lists of blend modes, or the Compatible blend mode, with the one mode readers used."),
        new(
            static t => t.FileNames,
            "7.11.3",
            "Removed the DOS, Mac OS and Unix file names from {n} file specifications, which PDF 2.0 deprecates. The main file name is kept."),
        new(
            static t => t.LaunchParameters,
            "12.6.4.6",
            "Removed the Windows, Mac OS and Unix launch parameters from {n} launch actions, which PDF 2.0 deprecates. The action keeps the file it opens."),
        new(static t => t.PrintFields, "14.8.5.6", "Wrote the check state of {n} print fields in its current form."),
        new(
            static t => t.LegacyCollections,
            "9.7.5.2",
            "Kept {n} font descriptions that use the Adobe-Korea1 or Adobe-Japan2 character collections, which PDF 2.0 deprecates, "
            + "because their glyph numbers cannot be moved to a newer collection safely."),
    ];

    /// <summary>Gets the refusals: features whose removal would change what the document shows, plays or prints.</summary>
    private static Pdf20TallyRule[] Refusals { get; } =
    [
        new(
            static t => t.OpiDictionaries,
            "14.11.7",
            "The document has images or forms that point to OPI (open prepress interface) files, which PDF 2.0 deprecates. Removing them would change how the file prints."),
        new(
            static t => t.Separations,
            "14.11.4",
            "The document is a preseparated file, where each page holds one ink. PDF 2.0 deprecates this, and the copy could not keep which page holds which ink."),
        new(
            static t => t.TrapNetworks,
            "12.5.6.21",
            "The document has trap network annotations, which PDF 2.0 deprecates. Removing them would change how the file prints."),
        new(
            static t => t.ColourTransfers,
            "8.4.5",
            "The document sets transfer functions that change colours. PDF 2.0 deprecates them, and the copy would print differently."),
        new(
            static t => t.LaunchWithoutFile,
            "12.6.4.6",
            "A launch action names its file only in platform launch parameters, which PDF 2.0 deprecates, so the copy would launch nothing."),
        new(
            static t => t.MovieAnnotations,
            "12.5.6.17",
            "The document has movie annotations. PDF 2.0 deprecates them, and HyperPDF cannot turn a movie into a form PDF 2.0 keeps, so the copy would lose the movie."),
        new(
            static t => t.SoundAnnotations,
            "12.5.6.16",
            "The document has sound annotations. PDF 2.0 deprecates them, and HyperPDF cannot turn a sound into a form PDF 2.0 keeps, so the copy would lose the sound."),
        new(
            static t => t.MediaActions,
            "12.6.4.9, 12.6.4.10",
            "The document has actions that play sounds or movies. PDF 2.0 deprecates them, so the copy would lose what they do."),
    ];

    /// <summary>Records the changes and the refusals.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="tally">The counts.</param>
    internal static void Report(Pdf20ConversionSession session, Pdf20ConversionTally tally)
    {
        foreach (var rule in Changes)
        {
            if (rule.Count(tally) is var count and > 0)
            {
                session.Changed(rule.Clause, rule.Text.Replace("{n}", count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
            }
        }

        foreach (var rule in Refusals)
        {
            if (rule.Count(tally) > 0)
            {
                session.Refuse(rule.Clause, rule.Text);
            }
        }
    }
}

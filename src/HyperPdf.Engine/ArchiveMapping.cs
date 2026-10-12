// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdfLibrary.Conformance;

namespace HyperPdf.Engine;

/// <summary>Maps PDF/A levels, conversion results and claim checks between the app and the library.</summary>
internal static class ArchiveMapping
{
    /// <summary>Gets the library level for an app level.</summary>
    /// <param name="level">The app level.</param>
    /// <returns>The library level.</returns>
    internal static PdfALevel ToLibrary(ArchiveLevel level) => level switch
    {
        ArchiveLevel.PdfA1A => PdfALevel.PdfA1A,
        ArchiveLevel.PdfA2B => PdfALevel.PdfA2B,
        ArchiveLevel.PdfA2U => PdfALevel.PdfA2U,
        ArchiveLevel.PdfA2A => PdfALevel.PdfA2A,
        ArchiveLevel.PdfA3B => PdfALevel.PdfA3B,
        ArchiveLevel.PdfA3U => PdfALevel.PdfA3U,
        ArchiveLevel.PdfA3A => PdfALevel.PdfA3A,
        ArchiveLevel.PdfA4 => PdfALevel.PdfA4,
        ArchiveLevel.PdfA4E => PdfALevel.PdfA4E,
        ArchiveLevel.PdfA4F => PdfALevel.PdfA4F,
        _ => PdfALevel.PdfA1B,
    };

    /// <summary>Gets the app level for a library level.</summary>
    /// <param name="level">The library level.</param>
    /// <returns>The app level.</returns>
    internal static ArchiveLevel FromLibrary(PdfALevel level) => level switch
    {
        PdfALevel.PdfA1A => ArchiveLevel.PdfA1A,
        PdfALevel.PdfA2B => ArchiveLevel.PdfA2B,
        PdfALevel.PdfA2U => ArchiveLevel.PdfA2U,
        PdfALevel.PdfA2A => ArchiveLevel.PdfA2A,
        PdfALevel.PdfA3B => ArchiveLevel.PdfA3B,
        PdfALevel.PdfA3U => ArchiveLevel.PdfA3U,
        PdfALevel.PdfA3A => ArchiveLevel.PdfA3A,
        PdfALevel.PdfA4 => ArchiveLevel.PdfA4,
        PdfALevel.PdfA4E => ArchiveLevel.PdfA4E,
        PdfALevel.PdfA4F => ArchiveLevel.PdfA4F,
        _ => ArchiveLevel.PdfA1B,
    };

    /// <summary>Converts a conversion result.</summary>
    /// <param name="result">The library result.</param>
    /// <returns>The app result.</returns>
    internal static ArchiveCopyResult ToResult(PdfAConversionResult result) =>
        new(FromLibrary(result.Level), ToNotes(result.Changes), ToNotes(result.Problems));

    /// <summary>Gets the removal a save made, when it removed the document's PDF/A claim.</summary>
    /// <param name="check">The writer's check of the claim, or <see langword="null"/>.</param>
    /// <returns>The removal, or <see langword="null"/> when the claim was kept or there was none.</returns>
    internal static ArchiveClaimRemoval? ToRemoval(PdfAClaimCheck? check)
    {
        if (check is not { Removed: true })
        {
            return null;
        }

        var broken = new string[check.Broken.Length];
        for (var i = 0; i < broken.Length; i++)
        {
            broken[i] = check.Broken[i].Description;
        }

        return new(FromLibrary(check.Level), broken) { Reasons = Descriptions(check.Problems) };
    }

    /// <summary>Gets the fixes a save made, when it repaired a file that broke its PDF/A level.</summary>
    /// <param name="check">The writer's check of the claim, or <see langword="null"/>.</param>
    /// <returns>The repair, or <see langword="null"/> when nothing was repaired.</returns>
    internal static ArchiveClaimRepair? ToRepair(PdfAClaimCheck? check) =>
        check is { Repaired: true } ? new(FromLibrary(check.Level), Descriptions(check.Repairs)) : null;

    /// <summary>Gets the plain-words descriptions of conversion notes.</summary>
    /// <param name="notes">The notes.</param>
    /// <returns>The descriptions.</returns>
    private static string[] Descriptions(PdfAConversionNote[] notes)
    {
        var descriptions = new string[notes.Length];
        for (var i = 0; i < notes.Length; i++)
        {
            descriptions[i] = notes[i].Description;
        }

        return descriptions;
    }

    /// <summary>Converts conversion notes.</summary>
    /// <param name="notes">The library notes.</param>
    /// <returns>The app notes.</returns>
    private static ArchiveNote[] ToNotes(PdfAConversionNote[] notes)
    {
        var converted = new ArchiveNote[notes.Length];
        for (var i = 0; i < notes.Length; i++)
        {
            converted[i] = new(notes[i].Description, notes[i].Clause);
        }

        return converted;
    }
}

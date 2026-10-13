// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Flattening;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Turns the app's flatten choices into the library's, and the library's report into notes in plain words.</summary>
internal static class HyperPdfFlattenCopyMapping
{
    /// <summary>The ways annotations of one reason are counted: left on the page, and removed.</summary>
    private const int GroupsPerReason = 2;

    /// <summary>The clause of ISO 32000-2 on appearance streams, behind most of the notes.</summary>
    private const string AppearanceClause = "12.5.5";

    /// <summary>The clause of ISO 32000-2 on annotation flags.</summary>
    private const string FlagsClause = "12.5.3";

    /// <summary>The clause of ISO 32000-2 on digital signatures.</summary>
    private const string SignatureClause = "12.8";

    /// <summary>The clause of ISO 32000-2 on the page's rotation.</summary>
    private const string RotationClause = "8.3.4";

    /// <summary>Converts the app's options to the library's.</summary>
    /// <param name="options">The app's options.</param>
    /// <returns>The library's options.</returns>
    internal static PdfFlattenOptions ToLibrary(FlattenCopyOptions options) => new()
    {
        Mode = options.Look == FlattenCopyLook.Print ? PdfFlattenMode.Print : PdfFlattenMode.Display,
        Targets = options.Scope switch
        {
            FlattenCopyScope.FormFields => PdfFlattenTargets.WidgetsOnly,
            FlattenCopyScope.CommentsAndMarkup => PdfFlattenTargets.MarkupOnly,
            FlattenCopyScope.Everything => PdfFlattenTargets.All,
            _ => PdfFlattenTargets.WithoutLinks,
        },
        BakeRotation = options.TurnPagesUpright,
    };

    /// <summary>Makes the result of a document that cannot be flattened because it is signed.</summary>
    /// <returns>The refusal.</returns>
    internal static FlattenCopyResult Signed() => new(
        0,
        0,
        0,
        false,
        [],
        [new("The document is signed. Flattening changes the signed pages and would make the signature invalid, so no copy was made.", SignatureClause)]);

    /// <summary>Makes the result of a finished flatten.</summary>
    /// <param name="report">The library's report.</param>
    /// <returns>The result.</returns>
    internal static FlattenCopyResult ToResult(PdfFlattenReport report)
    {
        var notes = new List<ArchiveNote>();
        AddSkipNotes(report, notes);
        if (report.XfaRemoved)
        {
            notes.Add(new("The older XFA form data was removed, because it no longer matches the page.", "12.7.8"));
        }

        if (report.RotationsBaked > 0)
        {
            notes.Add(new("Links and bookmarks that jump to a spot on a turned page may land in a different place.", RotationClause));
        }

        return new(report.PagesChanged, report.WidgetsDrawn, report.AnnotationsDrawn, report.FormRemoved, notes, []);
    }

    /// <summary>Adds one note for each reason annotations were not drawn and whether they were removed anyway.</summary>
    /// <param name="report">The library's report.</param>
    /// <param name="notes">Receives the notes.</param>
    private static void AddSkipNotes(PdfFlattenReport report, List<ArchiveNote> notes)
    {
        // One counter for each reason, left alone and then removed.
        var counts = new int[(Enum.GetValues<PdfFlattenSkipReason>().Length) * GroupsPerReason];
        foreach (var skip in report.Skipped)
        {
            counts[((int)skip.Reason * GroupsPerReason) + (skip.Removed ? 1 : 0)]++;
        }

        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] > 0)
            {
                notes.Add(Describe((PdfFlattenSkipReason)(i / GroupsPerReason), i % GroupsPerReason != 0, counts[i]));
            }
        }
    }

    /// <summary>Describes a number of annotations that were not drawn for the same reason.</summary>
    /// <param name="reason">The reason.</param>
    /// <param name="removed">Whether they were removed anyway.</param>
    /// <param name="count">How many there were.</param>
    /// <returns>The note.</returns>
    private static ArchiveNote Describe(PdfFlattenSkipReason reason, bool removed, int count)
    {
        var items = string.Create(CultureInfo.InvariantCulture, $"{count} {(removed ? "form field" : "annotation")}{(count == 1 ? string.Empty : "s")}");
        var be = count == 1 ? "was" : "were";
        var outcome = removed ? "so they were taken out without being drawn" : "so they were left as they were";
        return reason switch
        {
            PdfFlattenSkipReason.Hidden => new($"{items} {be} marked hidden, {outcome}.", FlagsClause),
            PdfFlattenSkipReason.NotShown => new($"{items} {be} marked not to show on screen, {outcome}.", FlagsClause),
            PdfFlattenSkipReason.NotPrinted => new($"{items} {be} not marked to print, {outcome}.", FlagsClause),
            PdfFlattenSkipReason.NoAppearance => new($"{items} had nothing to draw, {outcome}.", AppearanceClause),
            PdfFlattenSkipReason.UnusableAppearance => new($"{items} had a look that could not be placed on the page, {outcome}.", AppearanceClause),
            PdfFlattenSkipReason.LayerNotAllowed => new($"{items} {be} in a layer, which a PDF/A-1 file cannot have, {outcome}.", "8.11"),
            PdfFlattenSkipReason.ProductionMark => new($"{items} {be} for printing production (a printer's mark or trap network), {outcome}.", "14.11"),
            PdfFlattenSkipReason.RedactionNotApplied => new($"{items} {be} marking text to redact, {outcome}. Use Apply Redactions to remove that content.", "12.5.6.23"),
            _ => new($"{items} needed a font that a PDF/A file must embed, {outcome}.", AppearanceClause),
        };
    }
}

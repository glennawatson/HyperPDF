// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Output;

/// <summary>Describes what a save changed in the file, in plain words: its PDF/A claim and its trap networks.</summary>
internal static class SaveText
{
    /// <summary>Gets the note shown after a save, or <see langword="null"/> when the save changed nothing the reader needs to know.</summary>
    /// <param name="outcome">The save's result.</param>
    /// <returns>The note: the PDF/A line first, then the trap network line.</returns>
    internal static string? Notice(in DocumentSaveOutcome outcome)
    {
        var note = ClaimNotice(outcome);
        if (Pdf20Notice(outcome) is { } pdf20)
        {
            note = note is null ? pdf20 : Join(note, pdf20);
        }

        if (outcome.TrapNetworkWarnings.Count > 0)
        {
            note = Join(note, TrapNetworkNotice(outcome.TrapNetworkWarnings));
        }

        if (outcome.RemovedRequirements.Count > 0)
        {
            var removed = new string[outcome.RemovedRequirements.Count];
            for (var index = 0; index < removed.Length; index++)
            {
                removed[index] = outcome.RemovedRequirements[index].Text;
            }

            return Join(note, string.Join(' ', removed));
        }

        return outcome.RequirementWarnings.Count > 0 ? Join(note, RequirementNotices.SaveNotice(outcome.RequirementWarnings)) : note;
    }

    /// <summary>Gets the warning about removed trap networks: the first page's, and how many more pages had the same.</summary>
    /// <param name="warnings">The warnings, one for each page.</param>
    /// <returns>The warning.</returns>
    internal static string TrapNetworkNotice(IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        var more = warnings.Count - 1;
        return more switch
        {
            0 => warnings[0],
            1 => UiText.Format("{0} The same applies to one other page.", warnings[0]),
            _ => UiText.Format("{0} The same applies to {1} other pages.", warnings[0], more),
        };
    }

    /// <summary>Joins two notes, one to a line.</summary>
    /// <param name="first">The first note, or <see langword="null"/>.</param>
    /// <param name="second">The second note.</param>
    /// <returns>The notes.</returns>
    private static string Join(string? first, string second) => first is null ? second : string.Join('\n', first, second);

    /// <summary>Gets the note about writing PDF 2.0: that the file was written as PDF 2.0, or why it was left in its own version.</summary>
    /// <param name="outcome">The save's result.</param>
    /// <returns>The note, or <see langword="null"/> when PDF 2.0 was not tried.</returns>
    private static string? Pdf20Notice(in DocumentSaveOutcome outcome)
    {
        if (outcome.Pdf20Written)
        {
            return UiText.Get("Saved as a PDF 2.0 file.");
        }

        return outcome.Pdf20Refusal is { } reason
            ? UiText.Format("Saved in the file's own version, not PDF 2.0: {0} Use Save PDF 2.0 copy to choose what happens.", reason)
            : null;
    }

    /// <summary>Gets the note about the file's PDF/A claim, or <see langword="null"/> when the save kept it as it was.</summary>
    /// <param name="outcome">The save's result.</param>
    /// <returns>The note.</returns>
    private static string? ClaimNotice(in DocumentSaveOutcome outcome)
    {
        if (outcome.ClaimRemoved is { } removal)
        {
            return ArchiveText.ClaimRemovedNotice(removal);
        }

        return outcome.ClaimRepaired is { } repair ? ArchiveText.ClaimRepairedNotice(repair) : null;
    }
}

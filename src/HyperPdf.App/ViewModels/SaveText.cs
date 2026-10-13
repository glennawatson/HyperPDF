// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;

namespace HyperPdf.App.ViewModels;

/// <summary>Describes what a save changed in the file, in plain words: its PDF/A claim and its trap networks.</summary>
internal static class SaveText
{
    /// <summary>Gets the note shown after a save, or <see langword="null"/> when the save changed nothing the reader needs to know.</summary>
    /// <param name="outcome">The save's result.</param>
    /// <returns>The note: the PDF/A line first, then the trap network line.</returns>
    internal static string? Notice(in DocumentSaveOutcome outcome)
    {
        var note = ClaimNotice(outcome);
        if (outcome.TrapNetworkWarnings.Count > 0)
        {
            note = Join(note, TrapNetworkNotice(outcome.TrapNetworkWarnings));
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
            1 => $"{warnings[0]} The same applies to one other page.",
            _ => string.Create(CultureInfo.InvariantCulture, $"{warnings[0]} The same applies to {more} other pages."),
        };
    }

    /// <summary>Joins two notes, one to a line.</summary>
    /// <param name="first">The first note, or <see langword="null"/>.</param>
    /// <param name="second">The second note.</param>
    /// <returns>The notes.</returns>
    private static string Join(string? first, string second) => first is null ? second : string.Join('\n', first, second);

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

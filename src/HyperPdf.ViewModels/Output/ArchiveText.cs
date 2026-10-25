// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;

namespace HyperPdf.ViewModels.Output;

/// <summary>Describes PDF/A levels, PDF/A copies and removed or repaired PDF/A claims in plain words.</summary>
internal static class ArchiveText
{
    /// <summary>The lines before the list of changes: where the copy was saved, and the list's heading.</summary>
    private const int HeadingLines = 2;

    /// <summary>The most fixes a repaired-save note names, so it stays short.</summary>
    private const int ShownRepairs = 3;

    /// <summary>Gets a level's name, such as PDF/A-2b.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The name.</returns>
    internal static string Label(ArchiveLevel level) => level switch
    {
        ArchiveLevel.PdfA1A => "PDF/A-1a",
        ArchiveLevel.PdfA2B => "PDF/A-2b",
        ArchiveLevel.PdfA2U => "PDF/A-2u",
        ArchiveLevel.PdfA2A => "PDF/A-2a",
        ArchiveLevel.PdfA3B => "PDF/A-3b",
        ArchiveLevel.PdfA3U => "PDF/A-3u",
        ArchiveLevel.PdfA3A => "PDF/A-3a",
        ArchiveLevel.PdfA4 => "PDF/A-4",
        ArchiveLevel.PdfA4E => "PDF/A-4e",
        ArchiveLevel.PdfA4F => "PDF/A-4f",
        _ => "PDF/A-1b",
    };

    /// <summary>Gets the warning shown when a save removed a file's PDF/A claim.</summary>
    /// <param name="removal">The removed claim.</param>
    /// <returns>The warning.</returns>
    internal static string ClaimRemovedNotice(ArchiveClaimRemoval removal)
    {
        ArgumentNullException.ThrowIfNull(removal);
        var notice = UiText.Format("This file no longer met {0}, so HyperPDF removed its PDF/A claim.", Label(removal.Level));
        return removal.Reasons.Count > 0
            ? UiText.Format(
            "{0} It could not be fixed: {1} Use Save PDF/A Copy to make a PDF/A version.",
            notice,
            removal.Reasons[0])
            : UiText.Format("{0} Use Save PDF/A Copy to make a PDF/A version.", notice);
    }

    /// <summary>Gets the note shown when a save fixed a file so it still meets its PDF/A level.</summary>
    /// <param name="repair">The fixes.</param>
    /// <returns>The note, listing at most a few fixes.</returns>
    internal static string ClaimRepairedNotice(ArchiveClaimRepair repair)
    {
        ArgumentNullException.ThrowIfNull(repair);
        var notice = UiText.Format("HyperPDF fixed the saved file so it still meets {0}.", Label(repair.Level));
        var shown = Math.Min(repair.Repairs.Count, ShownRepairs);
        if (shown == 0)
        {
            return notice;
        }

        var named = new string[shown];
        for (var i = 0; i < shown; i++)
        {
            named[i] = repair.Repairs[i];
        }

        var fixes = string.Join(' ', named);
        var more = repair.Repairs.Count - shown;
        return more > 0
            ? UiText.Format(
            "{0} {1} And {2} more fixes.",
            notice,
            fixes,
            more)
            : $"{notice} {fixes}";
    }

    /// <summary>Describes a finished PDF/A copy, one point per line.</summary>
    /// <param name="result">The result.</param>
    /// <param name="path">Where the copy was saved.</param>
    /// <returns>The lines.</returns>
    internal static string[] Lines(ArchiveCopyResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        var label = Label(result.Level);
        if (!result.Converted)
        {
            var problems = new List<string>(result.Problems.Count + 1) { UiText.Format("The copy could not be made as {0}, so no file was saved. Here is why:", label) };
            foreach (var problem in result.Problems)
            {
                problems.Add($"• {problem.Description}");
            }

            return [.. problems];
        }

        var lines = new List<string>(result.Changes.Count + HeadingLines) { UiText.Format("Saved as {0}. The copy meets {1}. Your open file was not changed.", Path.GetFileName(path), label) };
        if (result.Changes.Count == 0)
        {
            lines.Add(UiText.Get("Nothing needed to change."));
            return [.. lines];
        }

        lines.Add(UiText.Get("To meet the standard, the copy:"));
        foreach (var change in result.Changes)
        {
            lines.Add($"• {change.Description}");
        }

        return [.. lines];
    }
}

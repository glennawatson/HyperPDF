// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;

namespace HyperPdf.App.ViewModels;

/// <summary>Describes PDF/A levels, PDF/A copies and removed PDF/A claims in plain words.</summary>
internal static class ArchiveText
{
    /// <summary>The lines before the list of changes: where the copy was saved, and the list's heading.</summary>
    private const int HeadingLines = 2;

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
        return $"This file no longer met {Label(removal.Level)}, so HyperPDF removed its PDF/A claim. Use Save PDF/A Copy to make a PDF/A version.";
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
            var problems = new List<string>(result.Problems.Count + 1) { $"The copy could not be made as {label}, so no file was saved. Here is why:" };
            foreach (var problem in result.Problems)
            {
                problems.Add($"• {problem.Description}");
            }

            return [.. problems];
        }

        var lines = new List<string>(result.Changes.Count + HeadingLines) { $"Saved as {Path.GetFileName(path)}. The copy meets {label}. Your open file was not changed." };
        if (result.Changes.Count == 0)
        {
            lines.Add("Nothing needed to change.");
            return [.. lines];
        }

        lines.Add("To meet the standard, the copy:");
        foreach (var change in result.Changes)
        {
            lines.Add($"• {change.Description}");
        }

        return [.. lines];
    }
}

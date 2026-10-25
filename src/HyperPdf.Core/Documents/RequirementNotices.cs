// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Documents;

/// <summary>Puts a document's unmet requirements into the calm notice shown when it opens.</summary>
public static class RequirementNotices
{
    /// <summary>
    /// Gets what the viewer does with a document whose unmet penalties reach 100 (ISO 32000-2 §12.11.6 says processing shall
    /// not continue). The pages stay readable as a static copy; forms, buttons and actions are off.
    /// </summary>
    public static string StaticCopy => UiText.Get("HyperPDF shows the pages as a static copy and keeps forms, buttons and actions off.");

    /// <summary>Gets what the viewer does when the unmet requirements are optional.</summary>
    public static string UsesFallback => UiText.Get("Everything else works as usual.");

    /// <summary>Gets the notice for a report.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The notice, or <see langword="null"/> when every requirement is met.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    public static string? Describe(RequirementReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return report.Level switch
        {
            RequirementLevel.Critical => $"{report.LocalisedText?.Summary ?? UiText.Get(report.Summary)} {StaticCopy}",
            RequirementLevel.Fallback => $"{report.LocalisedText?.Summary ?? UiText.Get(report.Summary)} {UsesFallback}",
            _ => null,
        };
    }

    /// <summary>Gets the details of a report, one line for each unmet requirement.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The lines, or <see langword="null"/> when there are none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="report"/> is <see langword="null"/>.</exception>
    public static string? DescribeDetails(RequirementReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var details = report.LocalisedText?.Details ?? report.Details;
        if (details.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var line in details)
        {
            _ = builder.AppendLine(UiText.Get(line));
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Gets the note shown after a save that removed requirements the file no longer needs.</summary>
    /// <param name="warnings">The warnings, one for each requirement removed.</param>
    /// <returns>The note.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="warnings"/> is <see langword="null"/>.</exception>
    public static string SaveNotice(IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);
        return string.Join(' ', warnings);
    }
}

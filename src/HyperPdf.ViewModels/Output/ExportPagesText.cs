// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdf.ViewModels.Output;

/// <summary>Words the export window uses to say what a run wrote.</summary>
internal static class ExportPagesText
{
    /// <summary>Describes a finished run in plain words.</summary>
    /// <param name="summary">What the run wrote.</param>
    /// <returns>The lines, one per fact.</returns>
    internal static string Describe(ExportPagesSummary summary)
    {
        var lines = new List<string>
        {
            summary.Files == 1
                ? $"Saved {summary.Path}."
                : string.Create(CultureInfo.CurrentCulture, $"Saved {summary.Files} files, numbered by page, next to {summary.Path}."),
        };

        if (summary.Skipped > 0)
        {
            lines.Add(string.Create(CultureInfo.CurrentCulture, $"{summary.Skipped} thing(s) this format cannot hold were left out."));
        }

        if (summary.Approximated > 0)
        {
            lines.Add(string.Create(CultureInfo.CurrentCulture, $"{summary.Approximated} thing(s) were drawn a little differently from the page."));
        }

        return OptimizeReportText.Join([.. lines]);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>Words the export window uses to say what a run wrote.</summary>
internal static class ExportPagesText
{
    /// <summary>Describes a finished run in plain words.</summary>
    /// <param name="summary">What the run wrote.</param>
    /// <returns>The lines, one per fact.</returns>
    internal static string Describe(ExportPagesSummary summary)
    {
        var lines = new List<string> { summary.Files == 1 ? UiText.Format("Saved {0}.", summary.Path) : UiText.Format("Saved {0} files, numbered by page, next to {1}.", summary.Files, summary.Path) };

        if (summary.Skipped > 0)
        {
            lines.Add(UiText.Format("{0} thing(s) this format cannot hold were left out.", summary.Skipped));
        }

        if (summary.Approximated > 0)
        {
            lines.Add(UiText.Format("{0} thing(s) were drawn a little differently from the page.", summary.Approximated));
        }

        return OptimizeReportText.Join([.. lines]);
    }
}

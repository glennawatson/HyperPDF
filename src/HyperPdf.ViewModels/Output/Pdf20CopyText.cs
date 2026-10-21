// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;

namespace HyperPdf.ViewModels.Output;

/// <summary>Words a PDF 2.0 copy's result in plain language, one point per line.</summary>
internal static class Pdf20CopyText
{
    /// <summary>The lines before the list of changes: where the copy was saved, and the list's heading.</summary>
    private const int HeadingLines = 2;

    /// <summary>Describes a finished PDF 2.0 copy, one point per line.</summary>
    /// <param name="result">The result.</param>
    /// <param name="path">Where the copy was saved.</param>
    /// <returns>The lines.</returns>
    internal static string[] Lines(Pdf20CopyResult result, string path)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Converted)
        {
            var problems = new List<string>(result.Problems.Count + 1) { "The copy could not be made as a PDF 2.0 file, so no file was saved. Here is why:" };
            foreach (var problem in result.Problems)
            {
                problems.Add($"• {problem.Description}");
            }

            return [.. problems];
        }

        var lines = new List<string>(result.Changes.Count + HeadingLines) { $"Saved as {Path.GetFileName(path)}. The copy is a PDF 2.0 file. Your open file was not changed." };
        if (result.Changes.Count == 0)
        {
            lines.Add("Nothing needed to change.");
            return [.. lines];
        }

        lines.Add("To make a PDF 2.0 file, the copy:");
        foreach (var change in result.Changes)
        {
            lines.Add($"• {change.Description}");
        }

        return [.. lines];
    }
}

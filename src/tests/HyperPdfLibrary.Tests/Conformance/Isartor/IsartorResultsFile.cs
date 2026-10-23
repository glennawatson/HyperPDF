// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance.Isartor;

/// <summary>Classifies Isartor test files by name and writes the per-file results list.</summary>
internal static class IsartorResultsFile
{
    /// <summary>The name of the written results file.</summary>
    internal const string FileName = "isartor-results.tsv";

    /// <summary>The start of every test file name.</summary>
    private const string NamePrefix = "isartor-";

    /// <summary>The start of the test number that ends the clause in a file name.</summary>
    private const string TestMarker = "-t";

    /// <summary>Reads the clause a test file was built to break from its name, such as <c>6.2.3.3</c> from <c>isartor-6-2-3-3-t02-fail-b.pdf</c>.</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The clause, or an empty string when the name does not follow the suite's pattern.</returns>
    internal static string ClauseOf(string fileName)
    {
        if (!fileName.StartsWith(NamePrefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var end = fileName.IndexOf(TestMarker, NamePrefix.Length, StringComparison.Ordinal);
        return end < 0 ? string.Empty : fileName[NamePrefix.Length..end].Replace('-', '.');
    }

    /// <summary>Classifies a validation report against the clause the file was built to break.</summary>
    /// <param name="report">The report.</param>
    /// <param name="clause">The expected clause.</param>
    /// <returns>The outcome.</returns>
    internal static IsartorOutcome Classify(PdfAValidationReport report, string clause)
    {
        if (report.Failed.Length == 0)
        {
            return IsartorOutcome.Missed;
        }

        return Array.Exists(report.Failed, failed => InClause(failed.Clause, clause))
            ? IsartorOutcome.DetectedInClause
            : IsartorOutcome.DetectedElsewhere;
    }

    /// <summary>Writes the results as a tab-separated list with a summary at the top.</summary>
    /// <param name="path">The file to write.</param>
    /// <param name="results">The results, one per test file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    internal static async Task WriteAsync(string path, List<IsartorResult> results, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        _ = text.AppendLine("# HyperPDF PDF/A-1b validator results on the Isartor test suite. Every file is built to break one requirement.");
        _ = text.AppendLine($"# files: {results.Count}");
        foreach (var outcome in Enum.GetValues<IsartorOutcome>())
        {
            _ = text.AppendLine($"# {outcome}: {results.Count(result => result.Outcome == outcome)}");
        }

        _ = text.AppendLine("# path\texpected clause\toutcome\tfailed rules (clause:test) or reason not opened");
        foreach (var result in results)
        {
            _ = text.AppendLine($"{result.Path}\t{result.Clause}\t{result.Outcome}\t{string.Join(' ', result.FailedRules)}");
        }

        await File.WriteAllTextAsync(path, text.ToString(), cancellationToken);
    }

    /// <summary>Gets whether a failed rule's clause is the expected clause or nested within it.</summary>
    /// <param name="failed">The failed rule's clause.</param>
    /// <param name="expected">The expected clause.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool InClause(string failed, string expected) =>
        expected.Length > 0
        && (string.Equals(failed, expected, StringComparison.Ordinal)
            || failed.StartsWith($"{expected}.", StringComparison.Ordinal)
            || expected.StartsWith($"{failed}.", StringComparison.Ordinal));
}

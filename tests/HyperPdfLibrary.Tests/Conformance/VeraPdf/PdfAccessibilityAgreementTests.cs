// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Checks the accessibility report against veraPDF on veraPDF's PDF/UA test files (CC BY 4.0), fetched at the commit
/// <c>verapdf.json</c> pins by <c>scripts/FetchVeraPdfTestFiles.cs</c>. For every mapping in <see cref="PdfUaRuleMapping.All"/>
/// and every file, the report must find one of the mapping's codes exactly when veraPDF's recorded result fails one of its
/// rules. The report covers only those rules; the others, and everything a person must judge, such as colour contrast or
/// the quality of a description, stay outside it. Without the test files the test skips, unless
/// <see cref="VeraPdfValidator.RequireVariable"/> is set, as it is in CI.
/// </summary>
public sealed class PdfAccessibilityAgreementTests
{
    /// <summary>Divides a disagreement's mapping label from its detail.</summary>
    private const string Separator = " | ";

    /// <summary>The most disagreements listed for each mapping in a failure message.</summary>
    private const int ListedPerMapping = 3;

    /// <summary>The least number of files that must be compared, so a missing folder cannot pass.</summary>
    private const int MinimumFiles = 100;

    /// <summary>Every mapped rule gives veraPDF's pass or fail on every PDF/UA test file.</summary>
    /// <param name="cancellationToken">Cancels the reports.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportAgreesWithVeraPdf(CancellationToken cancellationToken)
    {
        var testFiles = RequireTestFiles();
        var disagreements = new List<string>();
        var files = 0;
        var comparisons = 0;
        foreach (var expected in VeraPdfUaRecordedResults.Read())
        {
            if (!expected.Completed)
            {
                continue;
            }

            var file = Path.Combine(testFiles, expected.Path.Replace('/', Path.DirectorySeparatorChar));
            using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
            var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
            comparisons += Compare(expected, report, disagreements);
            files++;
        }

        await Assert.That(files).IsGreaterThan(MinimumFiles);
        await Assert.That(comparisons).IsGreaterThan(files);
        await Assert.That(disagreements).IsEmpty().Because($"{disagreements.Count} disagreements with veraPDF:{Environment.NewLine}{Summarize(disagreements)}");
    }

    /// <summary>Compares one report with veraPDF's result for the same file.</summary>
    /// <param name="expected">veraPDF's result.</param>
    /// <param name="report">HyperPdf's report.</param>
    /// <param name="disagreements">Receives each mapping that differs.</param>
    /// <returns>The number of mappings compared.</returns>
    internal static int Compare(VeraPdfUaRecordedResult expected, PdfAccessibilityReport report, List<string> disagreements)
    {
        var compared = 0;
        foreach (var mapping in PdfUaRuleMapping.All)
        {
            if (mapping.Part != expected.Part)
            {
                continue;
            }

            compared++;
            var ours = Array.Exists(mapping.Codes, code => report.GetCount(code) > 0);
            var theirs = Array.Exists(mapping.Rules, expected.Failed.Contains);
            if (mapping.Difference is null ? ours != theirs : theirs && !ours)
            {
                disagreements.Add($"{mapping.Label}{Separator}report {(ours ? "finds" : "does not find")} it but veraPDF {(theirs ? "fails" : "passes")}: {expected.Path}");
            }
        }

        return compared;
    }

    /// <summary>Gets the checkout of veraPDF's test files with the PDF/UA folders, or skips the test when there is none.</summary>
    /// <returns>The checkout folder.</returns>
    /// <exception cref="DirectoryNotFoundException">The test files are required but missing.</exception>
    internal static string RequireTestFiles()
    {
        var folder = Environment.GetEnvironmentVariable(PdfAValidatorAgreementTests.TestFilesVariable);
        if (folder is { Length: > 0 } && Directory.Exists(Path.Combine(folder, "PDF_UA-1")) && Directory.Exists(Path.Combine(folder, "PDF_UA-2")))
        {
            return folder;
        }

        const string message =
            $"No veraPDF PDF/UA test files. Run 'dotnet run --file scripts/FetchVeraPdfTestFiles.cs -- <dir>' and set {PdfAValidatorAgreementTests.TestFilesVariable} to that directory.";
        if (Environment.GetEnvironmentVariable(VeraPdfValidator.RequireVariable) == "1")
        {
            throw new DirectoryNotFoundException(message);
        }

        Skip.Test(message);
        return string.Empty;
    }

    /// <summary>Lists the first few disagreements of each mapping, so one broken mapping cannot hide the others.</summary>
    /// <param name="disagreements">The disagreements, each starting with its mapping's label and the separator.</param>
    /// <returns>The summary.</returns>
    private static string Summarize(List<string> disagreements)
    {
        var lines = new List<string>();
        foreach (var group in disagreements.GroupBy(static line => line[..line.IndexOf(Separator, StringComparison.Ordinal)], StringComparer.Ordinal))
        {
            lines.Add($"{group.Key}: {group.Count()}");
            lines.AddRange(group.Take(ListedPerMapping).Select(static line => $"    {line[(line.IndexOf(Separator, StringComparison.Ordinal) + Separator.Length)..]}"));
        }

        return string.Join(Environment.NewLine, lines);
    }
}

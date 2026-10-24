// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Checks HyperPdf's PDF/A validator against veraPDF on veraPDF's PDF/A test files (CC BY 4.0), fetched at the commit
/// <c>src/verapdf.json</c> pins by <c>src/tools/FetchVeraPdfTestFiles.cs</c>. For every file and every rule the validator checks,
/// it must fail the rule exactly when veraPDF's recorded result does. Without the test files the test skips, unless
/// <see cref="VeraPdfValidator.RequireVariable"/> is set, as it is in CI.
/// </summary>
public sealed class PdfAValidatorAgreementTests
{
    /// <summary>The variable naming the checkout of veraPDF's PDF/A test files.</summary>
    internal const string TestFilesVariable = "HYPERPDF_VERAPDF_TEST_FILES";

    /// <summary>The most disagreements listed in a failure message.</summary>
    private const int ListedDisagreements = 50;

    /// <summary>Every rule the validator checks gives veraPDF's pass or fail on every test file.</summary>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidatorAgreesWithVeraPdf(CancellationToken cancellationToken)
    {
        var testFiles = RequireTestFiles();
        var disagreements = new List<string>();
        var compared = 0;
        foreach (var expected in VeraPdfRecordedResults.Read())
        {
            if (!expected.Completed)
            {
                continue;
            }

            var file = Path.Combine(testFiles, expected.Path.Replace('/', Path.DirectorySeparatorChar));
            using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
            var report = await PdfAValidator.ValidateAsync(document, expected.Level, cancellationToken);
            compared += Compare(expected, report, disagreements);
        }

        await Assert.That(compared).IsGreaterThan(0);
        await Assert.That(disagreements).IsEmpty()
            .Because($"{disagreements.Count} disagreements with veraPDF:{Environment.NewLine}{string.Join(Environment.NewLine, disagreements.Take(ListedDisagreements))}");
    }

    /// <summary>Compares the checked rules of one report with veraPDF's result.</summary>
    /// <param name="expected">veraPDF's result.</param>
    /// <param name="report">HyperPdf's report.</param>
    /// <param name="disagreements">Receives each rule that differs.</param>
    /// <returns>The number of rules compared.</returns>
    internal static int Compare(VeraPdfRecordedResult expected, PdfAValidationReport report, List<string> disagreements)
    {
        var failed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var result in report.Failed)
        {
            _ = failed.Add($"{result.Clause}:{result.Test}");
        }

        foreach (var result in report.Checked)
        {
            var rule = $"{result.Clause}:{result.Test}";
            var ours = failed.Contains(rule);
            if (ours != expected.Failed.Contains(rule))
            {
                disagreements.Add($"{PdfALevels.Label(expected.Level)} {rule} {(ours ? "fails" : "passes")} but veraPDF {(ours ? "passes" : "fails")}: {expected.Path} [{result.Context}]");
            }
        }

        return report.Checked.Length;
    }

    /// <summary>Gets the checkout of veraPDF's PDF/A test files, or skips the test when there is none.</summary>
    /// <returns>The checkout folder.</returns>
    /// <exception cref="DirectoryNotFoundException">The test files are required but missing.</exception>
    internal static string RequireTestFiles()
    {
        var folder = Environment.GetEnvironmentVariable(TestFilesVariable);
        if (folder is { Length: > 0 } && Directory.Exists(Path.Combine(folder, "PDF_A-1b")))
        {
            return folder;
        }

        const string message = $"No veraPDF PDF/A test files. Run 'dotnet run --file src/tools/FetchVeraPdfTestFiles.cs -- <dir>' and set {TestFilesVariable} to that directory.";
        if (Environment.GetEnvironmentVariable(VeraPdfValidator.RequireVariable) == "1")
        {
            throw new DirectoryNotFoundException(message);
        }

        Skip.Test(message);
        return string.Empty;
    }
}

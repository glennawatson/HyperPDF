// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Checks HyperPdf's PDF/A validator against veraPDF on the veraPDF test corpus (CC BY 4.0), fetched at the commit
/// <c>verapdf.json</c> pins by <c>scripts/FetchVeraPdfCorpus.cs</c>. For every file and every rule the validator checks,
/// it must fail the rule exactly when veraPDF's recorded result does. Without the corpus the test skips, unless
/// <see cref="VeraPdfValidator.RequireVariable"/> is set, as it is in CI.
/// </summary>
public sealed class PdfAValidatorAgreementTests
{
    /// <summary>The variable naming the corpus checkout.</summary>
    internal const string CorpusVariable = "HYPERPDF_VERAPDF_CORPUS";

    /// <summary>The most disagreements listed in a failure message.</summary>
    private const int ListedDisagreements = 50;

    /// <summary>Every rule the validator checks gives veraPDF's pass or fail on every corpus file.</summary>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidatorAgreesWithVeraPdfOnTheCorpus(CancellationToken cancellationToken)
    {
        var corpus = RequireCorpus();
        var disagreements = new List<string>();
        var compared = 0;
        foreach (var expected in VeraPdfCorpusResults.Read())
        {
            if (!expected.Completed)
            {
                continue;
            }

            var file = Path.Combine(corpus, expected.Path.Replace('/', Path.DirectorySeparatorChar));
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
    internal static int Compare(VeraPdfCorpusResult expected, PdfAValidationReport report, List<string> disagreements)
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

    /// <summary>Gets the corpus checkout, or skips the test when there is none.</summary>
    /// <returns>The checkout folder.</returns>
    /// <exception cref="DirectoryNotFoundException">The corpus is required but missing.</exception>
    internal static string RequireCorpus()
    {
        var folder = Environment.GetEnvironmentVariable(CorpusVariable);
        if (folder is { Length: > 0 } && Directory.Exists(Path.Combine(folder, "PDF_A-1b")))
        {
            return folder;
        }

        const string message = $"No veraPDF corpus. Run 'dotnet run --file scripts/FetchVeraPdfCorpus.cs -- <dir>' and set {CorpusVariable} to that directory.";
        if (Environment.GetEnvironmentVariable(VeraPdfValidator.RequireVariable) == "1")
        {
            throw new DirectoryNotFoundException(message);
        }

        Skip.Test(message);
        return string.Empty;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Checks the conformance report's PDF/A rule results against veraPDF's recorded verdicts on the pinned veraPDF corpus.
/// The report checks the level a file claims, so each file whose claim names the level veraPDF validated is compared
/// rule by rule. Without the corpus the test skips, unless <see cref="VeraPdfValidator.RequireVariable"/> is set.
/// </summary>
public sealed class PdfConformanceReportAgreementTests
{
    /// <summary>The most disagreements listed in a failure message.</summary>
    private const int ListedDisagreements = 50;

    /// <summary>Every rule the report checks gives veraPDF's pass or fail on every corpus file that claims the level veraPDF checked.</summary>
    /// <param name="cancellationToken">Cancels the reports.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportAgreesWithVeraPdfOnTheCorpus(CancellationToken cancellationToken)
    {
        var corpus = PdfAValidatorAgreementTests.RequireCorpus();
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
            var report = await PdfDocumentConformance.GetConformanceAsync(document, cancellationToken);
            if (report.PdfAValidation is { } rules && rules.Level == expected.Level)
            {
                compared += PdfAValidatorAgreementTests.Compare(expected, rules, disagreements);
            }
        }

        await Assert.That(compared).IsGreaterThan(0);
        await Assert.That(disagreements).IsEmpty()
            .Because($"{disagreements.Count} disagreements with veraPDF:{Environment.NewLine}{string.Join(Environment.NewLine, disagreements.Take(ListedDisagreements))}");
    }
}

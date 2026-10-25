// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Compares the WCAG 2.2 rules HyperPdf checks with veraPDF's own WCAG profiles on veraPDF's PDF/UA test files (CC BY 4.0): on every file,
/// each rule HyperPdf checks must fail in HyperPdf exactly when it fails in veraPDF. Rules HyperPdf does not check, and rules that need a
/// person, are not compared. The profiles are read from the folder named by <see cref="VeraPdfValidator.ProfilesVariable"/>.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class WcagAgreementTests
{
    /// <summary>The fewest rule comparisons that must be made, so an empty folder cannot pass.</summary>
    private const int MinimumComparisons = 1000;

    /// <summary>Every WCAG rule HyperPdf checks gives veraPDF's pass or fail on veraPDF's PDF/UA test files.</summary>
    /// <param name="profile">The HyperPdf profile.</param>
    /// <param name="profileFile">The name of veraPDF's profile file.</param>
    /// <param name="testFolder">The PDF/UA test file folder to read.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStandardsProfile.Wcag22Machine, "WCAG-2-2-Machine.xml", "PDF_UA-1")]
    [Arguments(PdfStandardsProfile.Wcag22MachinePdf20, "WCAG-2-2-Machine-PDF20.xml", "PDF_UA-2")]
    [Arguments(PdfStandardsProfile.Wcag22Complete, "WCAG-2-2-Complete.xml", "PDF_UA-1")]
    [Arguments(PdfStandardsProfile.Wcag22CompletePdf20, "WCAG-2-2-Complete-PDF20.xml", "PDF_UA-2")]
    public async Task CheckedRulesAgreeWithVeraPdf(PdfStandardsProfile profile, string profileFile, string testFolder, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var profilePath = VeraPdfValidator.RequireProfile(profileFile);
        var folder = Path.Combine(PdfAccessibilityAgreementTests.RequireTestFiles(), testFolder);
        var files = Directory.GetFiles(folder, "*.pdf", SearchOption.AllDirectories);
        var jobs = await VeraPdfValidator.ValidateProfileAsync(home, profilePath, files, cancellationToken);
        var disagreements = new List<string>();
        var compared = 0;
        foreach (var file in files)
        {
            var job = jobs[Path.GetFullPath(file)];
            if (job.ProfileName is null)
            {
                continue;
            }

            using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
            var report = await PdfDocumentStandards.CheckAsync(document, profile, cancellationToken);
            compared += Compare(Path.GetFileName(file), report, job, disagreements);
        }

        await Assert.That(compared).IsGreaterThan(MinimumComparisons);
        await Assert.That(disagreements).IsEmpty().Because($"{disagreements.Count} disagreements: {string.Join(" | ", disagreements)}");
    }

    /// <summary>Compares each rule HyperPdf checks with veraPDF's result for one file.</summary>
    /// <param name="name">The file's name, for messages.</param>
    /// <param name="report">HyperPdf's report.</param>
    /// <param name="job">veraPDF's result.</param>
    /// <param name="disagreements">Receives each rule that differs.</param>
    /// <returns>The number of rules compared.</returns>
    private static int Compare(string name, PdfStandardsRuleReport report, VeraPdfJob job, List<string> disagreements)
    {
        var compared = 0;
        foreach (var result in report.Results)
        {
            if (result.Status is PdfStandardsRuleStatus.NotChecked or PdfStandardsRuleStatus.NeedsHumanReview)
            {
                continue;
            }

            compared++;
            var theirs = job.Failures.Exists(failure => failure.Clause == result.Rule.Clause && failure.TestNumber == result.Rule.TestNumber);
            var ours = result.Status == PdfStandardsRuleStatus.Failed;

            // A link that states a difference may fail when veraPDF passes, but never the other way round.
            var weaker = StandardsRuleIndex.Find(result.Rule)?.Difference is not null;
            if (weaker ? theirs && !ours : theirs != ours)
            {
                disagreements.Add($"{name}: {result.Rule.Reference} HyperPdf {result.Status}, veraPDF {(theirs ? "fails" : "passes")}");
            }
        }

        return compared;
    }
}

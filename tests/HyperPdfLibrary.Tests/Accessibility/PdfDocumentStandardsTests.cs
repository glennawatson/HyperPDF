// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests the rule tables and reports that map veraPDF's accessibility profiles to HyperPdf's checks.</summary>
public sealed class PdfDocumentStandardsTests
{
    /// <summary>The WCAG success criterion for info and relationships.</summary>
    private const string Relationships = "1.3.1";

    /// <summary>The WCAG success criterion for name, role and value.</summary>
    private const string NameRoleValue = "4.1.2";

    /// <summary>Every profile lists all of its rules, each with a clause and a test number.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="count">The number of rules in veraPDF's profile.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStandardsProfile.PdfUa1, 106)]
    [Arguments(PdfStandardsProfile.PdfUa2, 92)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part1, 1282)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part2, 1657)]
    [Arguments(PdfStandardsProfile.Iso32005, 1645)]
    [Arguments(PdfStandardsProfile.WtpdfReuse, 1711)]
    [Arguments(PdfStandardsProfile.WtpdfAccessibility, 1724)]
    [Arguments(PdfStandardsProfile.Wcag22, 56)]
    [Arguments(PdfStandardsProfile.Wcag22Development, 23)]
    [Arguments(PdfStandardsProfile.Wcag22Machine, 106)]
    [Arguments(PdfStandardsProfile.Wcag22MachinePdf20, 96)]
    [Arguments(PdfStandardsProfile.Wcag22Complete, 152)]
    [Arguments(PdfStandardsProfile.Wcag22CompletePdf20, 142)]
    public async Task ProfileListsEveryRule(PdfStandardsProfile profile, int count)
    {
        var rules = PdfDocumentStandards.GetRules(profile);

        await Assert.That(rules.Count).IsEqualTo(count);
        await Assert.That(rules.All(static rule => rule.Clause.Length > 0 && rule.TestNumber.Length > 0 && rule.Description.Length > 0)).IsTrue();
    }

    /// <summary>The report has one result for each rule, and the statuses add up to the profile's size.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="count">The number of rules in veraPDF's profile.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStandardsProfile.PdfUa1, 106)]
    [Arguments(PdfStandardsProfile.PdfUa2, 92)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part1, 1282)]
    [Arguments(PdfStandardsProfile.Iso32005, 1645)]
    [Arguments(PdfStandardsProfile.WtpdfReuse, 1711)]
    [Arguments(PdfStandardsProfile.WtpdfAccessibility, 1724)]
    [Arguments(PdfStandardsProfile.Wcag22Complete, 152)]
    public async Task ReportAccountsForEveryRule(PdfStandardsProfile profile, int count)
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var report = await PdfDocumentStandards.CheckAsync(document, profile, CancellationToken.None);

        var total = Enum.GetValues<PdfStandardsRuleStatus>().Sum(report.Count);
        await Assert.That(report.Results.Count).IsEqualTo(count);
        await Assert.That(total).IsEqualTo(count);
    }

    /// <summary>A rule HyperPdf does not check is never reported as passing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UncheckedRulesAreNotPassed()
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var report = PdfDocumentStandards.Check(document, PdfStandardsProfile.PdfUa1);

        // Clause 7.2 test 41 asks that table columns have the same row count; HyperPdf does not check it.
        var result = report.Results.Single(static r => r.Rule.Reference == "7.2:41");
        await Assert.That(result.Status).IsEqualTo(PdfStandardsRuleStatus.NotChecked);
        await Assert.That(report.Count(PdfStandardsRuleStatus.NotChecked)).IsGreaterThan(0);
    }

    /// <summary>A file with no metadata fails the PDF/UA-1 rules that ask for it, and the other results do not change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingMetadataFailsTheMetadataRules()
    {
        using var bare = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Xmp = null }));
        using var untitled = TaggedTestDocument.Open(AccessibilityPdfs.Build(new() { Xmp = AccessibilityPdfs.Packet(1, false) }));

        await Assert.That(StatusOf(PdfDocumentStandards.Check(bare, PdfStandardsProfile.PdfUa1), "7.1:8")).IsEqualTo(PdfStandardsRuleStatus.Failed);
        await Assert.That(StatusOf(PdfDocumentStandards.Check(untitled, PdfStandardsProfile.PdfUa1), "7.1:9")).IsEqualTo(PdfStandardsRuleStatus.Failed);
    }

    /// <summary>A file with metadata passes the rules that ask for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MetadataPassesTheMetadataRules()
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var report = PdfDocumentStandards.Check(document, PdfStandardsProfile.PdfUa1);

        await Assert.That(StatusOf(report, "7.1:8")).IsEqualTo(PdfStandardsRuleStatus.Passed);
        await Assert.That(StatusOf(report, "7.1:9")).IsEqualTo(PdfStandardsRuleStatus.Passed);
    }

    /// <summary>Rules veraPDF tags for a person to judge are listed as needing review, not as checked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HumanRulesNeedReview()
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var report = PdfDocumentStandards.Check(document, PdfStandardsProfile.Wcag22Complete);

        await Assert.That(report.Count(PdfStandardsRuleStatus.NeedsHumanReview)).IsGreaterThan(0);
        await Assert.That(report.GetResults(PdfStandardsRuleStatus.NeedsHumanReview).All(static r => r.Rule.NeedsHumanReview)).IsTrue();
    }

    /// <summary>The WCAG report groups rules by success criterion in numeric order, keeping each rule's own status.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WcagReportGroupsBySuccessCriterion()
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));

        var groups = PdfDocumentStandards.Check(document, PdfStandardsProfile.Wcag22Complete).GetBySuccessCriterion();

        var criteria = groups.Select(static group => group.Criterion).ToArray();
        using (Assert.Multiple())
        {
            await Assert.That(criteria).Contains(Relationships);
            await Assert.That(criteria).Contains(NameRoleValue);
            await Assert.That(criteria.Distinct().Count()).IsEqualTo(criteria.Length);
            await Assert.That(Array.IndexOf(criteria, Relationships)).IsLessThan(Array.IndexOf(criteria, NameRoleValue));
            await Assert.That(groups.Single(static group => group.Criterion == NameRoleValue).Results.Any(static r => r.Status == PdfStandardsRuleStatus.NeedsHumanReview)).IsTrue();
        }
    }

    /// <summary>The same rule text decides the same way in every profile that repeats it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedRulesAreDecidedAlike()
    {
        using var document = TaggedTestDocument.Open(AccessibilityPdfs.Build(new()));
        var byText = new Dictionary<string, PdfStandardsRuleStatus>(StringComparer.Ordinal);
        var disagreements = new List<string>();

        foreach (var profile in Enum.GetValues<PdfStandardsProfile>())
        {
            foreach (var result in PdfDocumentStandards.Check(document, profile).Results)
            {
                // veraPDF tags the same rule text for review in one profile and not in another, so only checked rules are compared.
                if (result.Status is PdfStandardsRuleStatus.NotChecked or PdfStandardsRuleStatus.NeedsHumanReview)
                {
                    continue;
                }

                if (byText.TryGetValue(result.Rule.Description, out var held) && held != result.Status)
                {
                    disagreements.Add($"{profile} {result.Rule.Specification} {result.Rule.Reference}: {held} and {result.Status}");
                }

                byText[result.Rule.Description] = result.Status;
            }
        }

        await Assert.That(disagreements).IsEmpty().Because(string.Join(" | ", disagreements));
    }

    /// <summary>Gets the status of the rule with a reference.</summary>
    /// <param name="report">The report.</param>
    /// <param name="reference">The clause and test number, such as <c>7.1:8</c>.</param>
    /// <returns>The status.</returns>
    private static PdfStandardsRuleStatus StatusOf(PdfStandardsRuleReport report, string reference) =>
        report.Results.Single(r => r.Rule.Reference == reference).Status;
}

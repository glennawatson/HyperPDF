// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Reports, for each rule of a veraPDF profile, whether HyperPdf checks it and what it finds in a document, by following the profile's plans.</summary>
internal static class StandardsRuleEvaluator
{
    /// <summary>Reports what HyperPdf finds for every rule of a profile.</summary>
    /// <param name="document">The document.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>The report.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static PdfStandardsRuleReport Evaluate(PdfDocument document, PdfStandardsProfile profile, CancellationToken cancellationToken)
    {
        var plans = StandardsRulePlans.For(profile);
        var level = profile == PdfStandardsProfile.WtpdfReuse ? PdfWtpdfProfile.Reuse : PdfWtpdfProfile.Accessibility;
        var evidence = new StandardsEvidence(document, level, cancellationToken);
        var results = new PdfStandardsRuleResult[plans.Length];
        try
        {
            for (var index = 0; index < plans.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[index] = new(plans[index].Rule, StatusOf(plans[index], ref evidence));
            }
        }
        finally
        {
            evidence.Dispose();
        }

        return new(profile, results);
    }

    /// <summary>Decides what HyperPdf can say about one rule.</summary>
    /// <param name="plan">The rule's plan.</param>
    /// <param name="evidence">What HyperPdf finds in the document.</param>
    /// <returns>The status.</returns>
    private static PdfStandardsRuleStatus StatusOf(StandardsRulePlan plan, ref StandardsEvidence evidence) => plan.Kind switch
    {
        StandardsRuleKind.Linked => Outcome(evidence.Fails(plan.Link!)),
        StandardsRuleKind.ReuseDeclaration => Outcome(evidence.LacksDeclaration(false)),
        StandardsRuleKind.AccessibilityDeclaration => Outcome(evidence.LacksDeclaration(true)),
        StandardsRuleKind.Pair => Outcome(evidence.IsPairBroken(plan.PairId, plan.AnnexL)),
        StandardsRuleKind.Human => PdfStandardsRuleStatus.NeedsHumanReview,
        _ => PdfStandardsRuleStatus.NotChecked,
    };

    /// <summary>Gives the status of a rule HyperPdf checks.</summary>
    /// <param name="failed">Whether the document breaks the rule.</param>
    /// <returns>Failed or passed.</returns>
    private static PdfStandardsRuleStatus Outcome(bool failed) => failed ? PdfStandardsRuleStatus.Failed : PdfStandardsRuleStatus.Passed;
}

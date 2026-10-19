// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// What HyperPdf can say about each rule of one veraPDF profile for one document. Every rule of the profile appears once.
/// Rules HyperPdf checks are <see cref="PdfStandardsRuleStatus.Passed"/> or <see cref="PdfStandardsRuleStatus.Failed"/>;
/// rules that need a person and rules without a check are listed apart and are never reported as passing. A report that
/// has no failed rule is therefore not a claim that the document conforms: veraPDF is the reference for that.
/// </summary>
/// <param name="Profile">The profile.</param>
/// <param name="Results">One result for each rule of the profile, in table order.</param>
[DebuggerDisplay("PdfStandardsRuleReport: {Profile}, {Results.Count} rules")]
public sealed record PdfStandardsRuleReport(PdfStandardsProfile Profile, IReadOnlyList<PdfStandardsRuleResult> Results)
{
    /// <summary>Counts the rules with a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The number of rules.</returns>
    public int Count(PdfStandardsRuleStatus status)
    {
        var count = 0;
        for (var index = 0; index < Results.Count; index++)
        {
            if (Results[index].Status == status)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Groups the results by the WCAG 2.2 success criterion they bear on, so a reader who works to WCAG can read the report by criterion. A
    /// rule is listed under each criterion it bears on; a rule that no criterion is known for is left out. Rules that need a person and rules
    /// without a check stay in their groups with their status, so a criterion is never shown as met only because HyperPdf cannot check it.
    /// </summary>
    /// <returns>One entry for each criterion, in numeric order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfStandardsCriterionResult[] GetBySuccessCriterion() => StandardsCriteria.Group(Profile, Results);

    /// <summary>Gets the results with a status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The results, in table order.</returns>
    public PdfStandardsRuleResult[] GetResults(PdfStandardsRuleStatus status)
    {
        var found = new PdfStandardsRuleResult[Count(status)];
        var next = 0;
        for (var index = 0; index < Results.Count; index++)
        {
            if (Results[index].Status != status)
            {
                continue;
            }

            found[next] = Results[index];
            next++;
        }

        return found;
    }
}

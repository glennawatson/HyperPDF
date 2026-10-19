// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Groups the results of a profile by the WCAG 2.2 success criterion they bear on. A rule of veraPDF's WCAG profiles names its criterion
/// as its clause. A PDF/UA rule that a WCAG profile repeats takes the criteria <see cref="PdfAccessibilityWcag"/> gives the report codes that
/// decide it. The criteria of each rule are worked out once, in <see cref="StandardsRulePlans"/>.
/// </summary>
internal static class StandardsCriteria
{
    /// <summary>Groups results by criterion.</summary>
    /// <param name="profile">The profile the results belong to.</param>
    /// <param name="results">The results of the profile, in table order. A result that is not the profile's own rule at its position is left out.</param>
    /// <returns>One entry for each criterion any rule bears on, in numeric order.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static PdfStandardsCriterionResult[] Group(PdfStandardsProfile profile, IReadOnlyList<PdfStandardsRuleResult> results)
    {
        var plans = StandardsRulePlans.For(profile);
        var criteria = StandardsRulePlans.Criteria;
        var counts = ArrayPool<int>.Shared.Rent(criteria.Length);
        var filled = ArrayPool<int>.Shared.Rent(criteria.Length);
        counts.AsSpan(0, criteria.Length).Clear();
        filled.AsSpan(0, criteria.Length).Clear();
        try
        {
            var used = Count(plans, results, counts);
            var grouped = new PdfStandardsCriterionResult[used];
            var next = 0;
            var slots = ArrayPool<int>.Shared.Rent(criteria.Length);
            try
            {
                for (var criterion = 0; criterion < criteria.Length; criterion++)
                {
                    if (counts[criterion] == 0)
                    {
                        continue;
                    }

                    slots[criterion] = next;
                    grouped[next] = new(criteria[criterion], new PdfStandardsRuleResult[counts[criterion]]);
                    next++;
                }

                Fill(plans, results, grouped, slots, filled);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(slots);
            }

            return grouped;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(counts);
            ArrayPool<int>.Shared.Return(filled);
        }
    }

    /// <summary>Counts the results of each criterion.</summary>
    /// <param name="plans">The profile's plans.</param>
    /// <param name="results">The results.</param>
    /// <param name="counts">Receives the count for each criterion.</param>
    /// <returns>The number of criteria with a result.</returns>
    private static int Count(StandardsRulePlan[] plans, IReadOnlyList<PdfStandardsRuleResult> results, int[] counts)
    {
        var used = 0;
        for (var index = 0; index < Math.Min(plans.Length, results.Count); index++)
        {
            if (!ReferenceEquals(plans[index].Rule, results[index].Rule))
            {
                continue;
            }

            foreach (var criterion in plans[index].Criteria)
            {
                counts[criterion]++;
                if (counts[criterion] == 1)
                {
                    used++;
                }
            }
        }

        return used;
    }

    /// <summary>Puts each result under each criterion it bears on.</summary>
    /// <param name="plans">The profile's plans.</param>
    /// <param name="results">The results.</param>
    /// <param name="grouped">The groups, one for each criterion with a result.</param>
    /// <param name="slots">The position in <paramref name="grouped"/> of each criterion that has one.</param>
    /// <param name="filled">How many results each group already holds, by criterion.</param>
    private static void Fill(StandardsRulePlan[] plans, IReadOnlyList<PdfStandardsRuleResult> results, PdfStandardsCriterionResult[] grouped, int[] slots, int[] filled)
    {
        for (var index = 0; index < Math.Min(plans.Length, results.Count); index++)
        {
            if (!ReferenceEquals(plans[index].Rule, results[index].Rule))
            {
                continue;
            }

            foreach (var criterion in plans[index].Criteria)
            {
                grouped[slots[criterion]].Results[filled[criterion]] = results[index];
                filled[criterion]++;
            }
        }
    }
}

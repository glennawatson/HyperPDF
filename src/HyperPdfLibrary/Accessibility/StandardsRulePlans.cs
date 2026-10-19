// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Works out, once, how HyperPdf decides each rule of each profile: which link or declaration or parent-child pair settles it, and which WCAG
/// success criteria it bears on. Reading the rule text and finding its link is the costly part of a report, so a check of a document only
/// has to read the document and follow the plans.
/// </summary>
internal static class StandardsRulePlans
{
    /// <summary>The number of profiles.</summary>
    private const int ProfileCount = 13;

    /// <summary>The specification of the Well-Tagged PDF rules.</summary>
    private const string WtpdfSpecification = "WTPDF_1_0";

    /// <summary>The reuse declaration rule of Well-Tagged PDF 1.0.</summary>
    private const string ReuseDeclaration = "6.1.2:1";

    /// <summary>The accessibility declaration rule of Well-Tagged PDF 1.0.</summary>
    private const string AccessibilityDeclaration = "6.1.3:1";

    /// <summary>The start of the specification names veraPDF gives the Annex L rules of the tagged PDF profiles.</summary>
    private const string AnnexSpecification = "ISO_19005";

    /// <summary>The specification of the rules whose clause is a WCAG success criterion.</summary>
    private const string WcagSpecification = "WCAG_2_2";

    /// <summary>The number of dot-separated numbers in a success criterion.</summary>
    private const int CriterionParts = 3;

    /// <summary>The number of each parent-child pair a profile's rules name; filled once and never changed.</summary>
    private static readonly Dictionary<StandardsPairKey, int> PairIds = [];

    /// <summary>The plans of each profile, indexed by <see cref="PdfStandardsProfile"/>; filled once and never changed.</summary>
    private static readonly StandardsRulePlan[][] ByProfile = new StandardsRulePlan[ProfileCount][];

    /// <summary>Initializes static members of the <see cref="StandardsRulePlans"/> class.</summary>
    static StandardsRulePlans()
    {
        var criteria = CollectCriteria();
        Criteria = criteria;
        for (var profile = 0; profile < ProfileCount; profile++)
        {
            ByProfile[profile] = Plan((PdfStandardsProfile)profile, criteria);
        }
    }

    /// <summary>Gets the WCAG success criteria any rule bears on, in numeric order.</summary>
    internal static string[] Criteria { get; }

    /// <summary>Gets the number of distinct parent-child pairs the profiles' rules name.</summary>
    internal static int PairCount => PairIds.Count;

    /// <summary>Gets the plans of a profile's rules, in table order.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The plans; the array is shared, so callers must not change it.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The profile is not one of the defined profiles.</exception>
    internal static StandardsRulePlan[] For(PdfStandardsProfile profile)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)profile);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((int)profile, ProfileCount);
        return ByProfile[(int)profile];
    }

    /// <summary>Finds the number of a parent-child pair.</summary>
    /// <param name="key">The pair.</param>
    /// <param name="id">Receives the pair's number.</param>
    /// <returns><see langword="true"/> when some rule names the pair.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryGetPairId(in StandardsPairKey key, out int id) => PairIds.TryGetValue(key, out id);

    /// <summary>Reads every rule of every profile to number the parent-child pairs and list the success criteria.</summary>
    /// <returns>The success criteria, in numeric order.</returns>
    private static string[] CollectCriteria()
    {
        var found = new List<string>();
        for (var profile = 0; profile < ProfileCount; profile++)
        {
            foreach (var rule in StandardsRuleCatalog.For((PdfStandardsProfile)profile))
            {
                if (StandardsContainmentRules.TryParse(rule.Description, out var pair) && StandardsContainmentRules.IsChecked(in pair))
                {
                    _ = PairIds.TryAdd(new(pair.Kind, pair.Parent, pair.Child), PairIds.Count);
                }

                foreach (var criterion in CriteriaOf(rule, profile >= (int)PdfStandardsProfile.Wcag22))
                {
                    if (!found.Contains(criterion))
                    {
                        found.Add(criterion);
                    }
                }
            }
        }

        found.Sort(CompareNumbers);
        return [.. found];
    }

    /// <summary>Plans the rules of one profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="criteria">The success criteria, in numeric order.</param>
    /// <returns>The plans.</returns>
    private static StandardsRulePlan[] Plan(PdfStandardsProfile profile, string[] criteria)
    {
        var rules = StandardsRuleCatalog.For(profile);
        var wcag = profile >= PdfStandardsProfile.Wcag22;
        var plans = new StandardsRulePlan[rules.Length];
        for (var index = 0; index < rules.Length; index++)
        {
            plans[index] = PlanRule(rules[index], wcag, criteria);
        }

        return plans;
    }

    /// <summary>Plans one rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="wcag">Whether the rule is read as part of a WCAG profile.</param>
    /// <param name="criteria">The success criteria, in numeric order.</param>
    /// <returns>The plan.</returns>
    private static StandardsRulePlan PlanRule(PdfStandardsRule rule, bool wcag, string[] criteria)
    {
        var indexes = IndexesOf(CriteriaOf(rule, wcag), criteria);
        if (Link(rule, wcag) is { } link)
        {
            return new(rule, StandardsRuleKind.Linked, link, -1, false, indexes);
        }

        if (rule.Specification == WtpdfSpecification && rule.Reference is ReuseDeclaration or AccessibilityDeclaration)
        {
            var kind = rule.Reference == AccessibilityDeclaration ? StandardsRuleKind.AccessibilityDeclaration : StandardsRuleKind.ReuseDeclaration;
            return new(rule, kind, null, -1, false, indexes);
        }

        if (StandardsContainmentRules.TryParse(rule.Description, out var pair) && StandardsContainmentRules.IsChecked(in pair))
        {
            var annexL = rule.Specification.StartsWith(AnnexSpecification, StringComparison.Ordinal);
            return new(rule, StandardsRuleKind.Pair, null, PairIds[new(pair.Kind, pair.Parent, pair.Child)], annexL, indexes);
        }

        return new(rule, rule.NeedsHumanReview ? StandardsRuleKind.Human : StandardsRuleKind.NotChecked, null, -1, false, indexes);
    }

    /// <summary>Finds the link that decides a rule, if one applies where the rule is read.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="wcag">Whether the rule is read as part of a WCAG profile.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    private static StandardsRuleLink? Link(PdfStandardsRule rule, bool wcag) =>
        wcag && StandardsRuleLinks.IsWcagVariant(rule) ? null : StandardsRuleIndex.Find(rule);

    /// <summary>Gets the success criteria a rule bears on.</summary>
    /// <param name="rule">The rule.</param>
    /// <param name="wcag">Whether the rule is read as part of a WCAG profile.</param>
    /// <returns>The criterion numbers; none when no criterion is known for the rule.</returns>
    private static List<string> CriteriaOf(PdfStandardsRule rule, bool wcag)
    {
        List<string> found = [];
        if (rule.Specification == WcagSpecification)
        {
            found.Add(Trim(rule.Clause));
            return found;
        }

        if (Link(rule, wcag) is not { } link)
        {
            return found;
        }

        foreach (var code in link.Codes)
        {
            foreach (var criterion in PdfAccessibilityWcag.GetSuccessCriteria(code))
            {
                if (!found.Contains(criterion))
                {
                    found.Add(criterion);
                }
            }
        }

        return found;
    }

    /// <summary>Finds each criterion's position in the list of all criteria.</summary>
    /// <param name="found">The criteria of one rule.</param>
    /// <param name="criteria">The success criteria, in numeric order.</param>
    /// <returns>The positions.</returns>
    private static int[] IndexesOf(List<string> found, string[] criteria)
    {
        var indexes = new int[found.Count];
        for (var index = 0; index < indexes.Length; index++)
        {
            indexes[index] = Array.IndexOf(criteria, found[index]);
        }

        return indexes;
    }

    /// <summary>Keeps the first three numbers of a clause, so <c>4.1.2.1</c> is the criterion <c>4.1.2</c>.</summary>
    /// <param name="clause">The clause.</param>
    /// <returns>The criterion.</returns>
    private static string Trim(string clause)
    {
        var parts = clause.Split('.');
        return parts.Length <= CriterionParts ? clause : string.Join('.', parts, 0, CriterionParts);
    }

    /// <summary>Orders two criterion numbers as numbers, part by part.</summary>
    /// <param name="x">The first criterion.</param>
    /// <param name="y">The second criterion.</param>
    /// <returns>The order.</returns>
    private static int CompareNumbers(string x, string y)
    {
        var left = x.Split('.');
        var right = y.Split('.');
        for (var index = 0; index < Math.Min(left.Length, right.Length); index++)
        {
            var order = int.Parse(left[index], CultureInfo.InvariantCulture).CompareTo(int.Parse(right[index], CultureInfo.InvariantCulture));
            if (order != 0)
            {
                return order;
            }
        }

        return left.Length.CompareTo(right.Length);
    }
}

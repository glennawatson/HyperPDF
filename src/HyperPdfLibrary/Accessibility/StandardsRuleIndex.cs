// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Finds the link for a profile rule by its text. veraPDF repeats a rule word for word in several profiles (PDF/UA-2, Well-Tagged PDF,
/// ISO/TS 32005 and the WCAG profiles share most of theirs), and the check that decides one decides them all. The clause and test
/// number are not used, because a profile may number a different rule the way PDF/UA numbers this one.
/// </summary>
internal static class StandardsRuleIndex
{
    /// <summary>The links by rule text; filled once and never changed.</summary>
    private static readonly Dictionary<string, StandardsRuleLink> ByDescription = Build();

    /// <summary>Finds the link that decides a rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The link, or <see langword="null"/> when HyperPdf has no check for the rule.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StandardsRuleLink? Find(PdfStandardsRule rule) => ByDescription.GetValueOrDefault(rule.Description);

    /// <summary>Reads the links, looking each rule up in the PDF/UA profiles that name it.</summary>
    /// <returns>The links by rule text.</returns>
    private static Dictionary<string, StandardsRuleLink> Build()
    {
        Dictionary<string, PdfStandardsRule> rules = [with(StringComparer.Ordinal)];
        foreach (var profile in (PdfStandardsProfile[])[PdfStandardsProfile.PdfUa1, PdfStandardsProfile.PdfUa2])
        {
            foreach (var rule in StandardsRuleCatalog.For(profile))
            {
                rules[$"{rule.Specification}|{rule.Reference}"] = rule;
            }
        }

        Dictionary<string, StandardsRuleLink> byDescription = [with(StringComparer.Ordinal)];
        foreach (var link in StandardsRuleLinks.All)
        {
            foreach (var reference in link.Rules)
            {
                if (rules.TryGetValue($"{link.Specification}|{reference}", out var rule))
                {
                    _ = byDescription.TryAdd(rule.Description, link);
                }
            }
        }

        return byDescription;
    }
}

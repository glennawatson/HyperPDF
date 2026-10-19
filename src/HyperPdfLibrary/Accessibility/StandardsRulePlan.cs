// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Everything HyperPdf works out about one rule of a profile before it looks at any document, so a check only has to read the document.</summary>
/// <param name="Rule">The rule.</param>
/// <param name="Kind">How the rule is decided.</param>
/// <param name="Link">The link that decides the rule when <paramref name="Kind"/> is <see cref="StandardsRuleKind.Linked"/>.</param>
/// <param name="PairId">The number of the rule's parent-child pair when <paramref name="Kind"/> is <see cref="StandardsRuleKind.Pair"/>.</param>
/// <param name="AnnexL">Whether the pair rule comes from an Annex L profile, which reads the structure tree through <c>Part</c> and <c>Div</c>.</param>
/// <param name="Criteria">The positions, in <see cref="StandardsRulePlans.Criteria"/>, of the WCAG success criteria the rule bears on.</param>
[DebuggerDisplay("StandardsRulePlan: {Rule.Reference} {Kind}")]
internal sealed record StandardsRulePlan(PdfStandardsRule Rule, StandardsRuleKind Kind, StandardsRuleLink? Link, int PairId, bool AnnexL, int[] Criteria);

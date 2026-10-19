// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>One rule of a veraPDF validation profile.</summary>
/// <param name="Specification">The specification the clause belongs to, as veraPDF names it, such as <c>ISO_14289_1</c>.</param>
/// <param name="Clause">The clause, such as <c>7.1</c>, or <c>Table 5. Annot-Aside</c> for a parent-child pair of ISO/TS 32005.</param>
/// <param name="TestNumber">The rule's test number within the clause.</param>
/// <param name="Description">The rule's text as veraPDF's profile gives it.</param>
/// <param name="NeedsHumanReview">Whether a person has to judge the rule, so a program cannot pass or fail it.</param>
[DebuggerDisplay("PdfStandardsRule: {Specification} {Clause}:{TestNumber}")]
public sealed record PdfStandardsRule(string Specification, string Clause, string TestNumber, string Description, bool NeedsHumanReview)
{
    /// <summary>Gets the clause and test number written the way veraPDF reports a failure, such as <c>7.1:5</c>.</summary>
    public string Reference => field ??= $"{Clause}:{TestNumber}";
}

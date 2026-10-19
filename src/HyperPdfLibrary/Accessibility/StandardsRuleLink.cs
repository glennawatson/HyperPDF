// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Ties veraPDF profile rules to the HyperPdf checks that decide them. The checks fail the document when one of the report's
/// <see cref="Codes"/> is found or one of the Well-Tagged PDF <see cref="WtpdfClauses"/> is reported. The link is exact when it has
/// no <see cref="Difference"/>: the checks fail exactly when one of the rules fails. A link with a difference is weaker: the checks
/// fail whenever a rule fails, and may also fail when veraPDF passes, for the stated reason.
/// </summary>
/// <param name="Specification">The specification the rules belong to, as veraPDF's profiles name it.</param>
/// <param name="Rules">The rules as <c>clause:test</c>; the link holds when any of them fails.</param>
/// <param name="Codes">The accessibility report codes that fail the rules.</param>
/// <param name="WtpdfClauses">The Well-Tagged PDF finding clauses that fail the rules.</param>
/// <param name="Difference">Why the checks may fail when veraPDF passes, or <see langword="null"/> when they must agree exactly.</param>
[DebuggerDisplay("StandardsRuleLink: {Specification} {Rules.Length} rules")]
internal sealed record StandardsRuleLink(string Specification, string[] Rules, PdfAccessibilityCode[] Codes, string[] WtpdfClauses, string? Difference)
{
    /// <summary>Initializes a new instance of the <see cref="StandardsRuleLink"/> record that report codes decide exactly.</summary>
    /// <param name="specification">The specification.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="codes">The report codes.</param>
    internal StandardsRuleLink(string specification, string[] rules, PdfAccessibilityCode[] codes)
        : this(specification, rules, codes, [], null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StandardsRuleLink"/> record that report codes decide, with a stated difference.</summary>
    /// <param name="specification">The specification.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="codes">The report codes.</param>
    /// <param name="difference">Why the checks may fail when veraPDF passes.</param>
    internal StandardsRuleLink(string specification, string[] rules, PdfAccessibilityCode[] codes, string difference)
        : this(specification, rules, codes, [], difference)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="StandardsRuleLink"/> record that Well-Tagged PDF clauses decide exactly.</summary>
    /// <param name="specification">The specification.</param>
    /// <param name="rules">The rules.</param>
    /// <param name="wtpdfClauses">The finding clauses.</param>
    internal StandardsRuleLink(string specification, string[] rules, string[] wtpdfClauses)
        : this(specification, rules, [], wtpdfClauses, null)
    {
    }

    /// <summary>Gets a label for messages.</summary>
    internal string Label => $"{Specification} {string.Join('+', Rules)} = {string.Join('+', Codes)}{string.Join('+', WtpdfClauses)}";
}

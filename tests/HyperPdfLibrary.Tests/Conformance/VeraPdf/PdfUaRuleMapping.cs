// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// A link from <see cref="StandardsRuleLinks"/> to the veraPDF PDF/UA part it belongs to. The report agrees with veraPDF on a link
/// when, on every one of veraPDF's PDF/UA test files, one of the codes or Well-Tagged PDF clauses is found exactly when one of the
/// rules fails. A link with a recorded difference is weaker: the report must find a code whenever a rule fails, and may find one
/// when veraPDF passes, for the stated reason.
/// </summary>
/// <param name="Part">The part of ISO 14289 the rules belong to.</param>
/// <param name="Link">The link.</param>
[DebuggerDisplay("PdfUaRuleMapping: part {Part}, {Link.Rules.Length} rules")]
internal sealed record PdfUaRuleMapping(int Part, StandardsRuleLink Link)
{
    /// <summary>Part 2 of ISO 14289.</summary>
    private const int PartTwo = 2;

    /// <summary>Gets the mappings the report is held to.</summary>
    internal static PdfUaRuleMapping[] All { get; } = Build();

    /// <summary>Gets the report codes of the link.</summary>
    internal PdfAccessibilityCode[] Codes => Link.Codes;

    /// <summary>Gets the Well-Tagged PDF finding clauses of the link.</summary>
    internal string[] WtpdfClauses => Link.WtpdfClauses;

    /// <summary>Gets the veraPDF rules of the link as <c>clause:test</c>.</summary>
    internal string[] Rules => Link.Rules;

    /// <summary>Gets why the report may find a code when veraPDF passes, or <see langword="null"/> when they must agree exactly.</summary>
    internal string? Difference => Link.Difference;

    /// <summary>Gets a label for assertion messages.</summary>
    internal string Label => $"part {Part} {string.Join('+', Codes)}{string.Join('+', WtpdfClauses)} = {string.Join('+', Rules)}";

    /// <summary>Builds the mappings of the PDF/UA parts.</summary>
    /// <returns>The mappings.</returns>
    private static PdfUaRuleMapping[] Build()
    {
        var mappings = new List<PdfUaRuleMapping>();
        foreach (var link in StandardsRuleLinks.All)
        {
            if (link.Specification == StandardsRuleLinks.Ua1)
            {
                mappings.Add(new(1, link));
            }
            else if (link.Specification == StandardsRuleLinks.Ua2)
            {
                mappings.Add(new(PartTwo, link));
            }
        }

        return [.. mappings];
    }
}

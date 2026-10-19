// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Turns findings of the accessibility report into Well-Tagged PDF 1.0 findings: the font rules of clause 8.4.5 and the
/// parent-child rules of ISO/TS 32005 Table 5, which WTPDF requires of every tag (clauses 8.2.2 and 8.2.4). They bind both levels.
/// </summary>
internal static class WtpdfReportScan
{
    /// <summary>The clause that names the containment table.</summary>
    private const string ContainmentClause = "8.2.4 (ISO/TS 32005 Table 5)";

    /// <summary>Adds a finding for each font or containment problem in the report.</summary>
    /// <param name="report">The accessibility report.</param>
    /// <param name="findings">Receives the findings.</param>
    internal static void Check(PdfAccessibilityReport report, List<PdfWtpdfFinding> findings)
    {
        var reported = report.Findings;
        for (var i = 0; i < reported.Count; i++)
        {
            var clause = ClauseOf(reported[i].Code);
            if (clause is not null)
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, clause, reported[i].Message, reported[i].ElementId?.Number ?? 0));
            }
        }
    }

    /// <summary>Gets the clause a finding belongs to.</summary>
    /// <param name="code">The finding's code.</param>
    /// <returns>The clause, or <see langword="null"/> when the code is not one WTPDF restates.</returns>
    private static string? ClauseOf(PdfAccessibilityCode code) => code switch
    {
        PdfAccessibilityCode.FontNotEmbedded or PdfAccessibilityCode.GlyphMissingFromFont => "8.4.5.5",
        PdfAccessibilityCode.FontNoUnicodeMap or PdfAccessibilityCode.FontUnicodeMapInvalid => "8.4.5.8",
        PdfAccessibilityCode.NotdefGlyphShown => "8.4.5.9",
        PdfAccessibilityCode.StructureContainmentViolation or PdfAccessibilityCode.StructureOccurrenceViolation => ContainmentClause,
        _ => null,
    };
}

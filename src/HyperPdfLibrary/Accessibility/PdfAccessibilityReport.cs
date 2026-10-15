// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// A plain reading of a document's accessibility: what it claims about PDF/UA, how it is tagged, and what looks wrong.
/// This is a reading report, not a validator. It reads the same entries a validator does and flags the common faults,
/// but it does not prove or disprove PDF/UA conformance. veraPDF and the PDF Accessibility Checker (PAC) are the
/// references for that. It also cannot judge colour contrast, reading order sense or the quality of a description.
/// </summary>
/// <param name="Claim">What the file says about PDF/UA.</param>
/// <param name="IsMarked">Whether the catalog's <c>/MarkInfo /Marked</c> is true.</param>
/// <param name="HasStructureTree">Whether the catalog has a <c>/StructTreeRoot</c>.</param>
/// <param name="Language">The catalog's <c>/Lang</c>, or <see langword="null"/>.</param>
/// <param name="HasTitle">Whether the XMP metadata has a <c>dc:title</c>.</param>
/// <param name="DisplayDocTitle">Whether the viewer preferences ask for the title to show.</param>
/// <param name="HasSuspects">Whether <c>/MarkInfo /Suspects</c> is true.</param>
/// <param name="Pages">What was counted on each page, in page order.</param>
/// <param name="Findings">The problems found: document entries, then the structure tree, then each page, then the fonts. Each code is capped; see <paramref name="Counts"/> for totals.</param>
/// <param name="Counts">How many times each code was found, in code order; codes never found are left out.</param>
[DebuggerDisplay("PdfAccessibilityReport: claim {Claim.Part}, {Findings.Count} findings")]
public sealed record PdfAccessibilityReport(
    PdfUaClaim Claim,
    bool IsMarked,
    bool HasStructureTree,
    string? Language,
    bool HasTitle,
    bool DisplayDocTitle,
    bool HasSuspects,
    IReadOnlyList<PdfAccessibilityPage> Pages,
    IReadOnlyList<PdfAccessibilityFinding> Findings,
    IReadOnlyList<PdfAccessibilityCount> Counts)
{
    /// <summary>Gets the total number of problems found, counting those left out of <see cref="Findings"/> by the cap.</summary>
    public int TotalCount
    {
        get
        {
            var total = 0;
            foreach (var count in Counts)
            {
                total += count.Count;
            }

            return total;
        }
    }

    /// <summary>Gets how many times a code was found.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The count; zero when the code was not found.</returns>
    public int GetCount(PdfAccessibilityCode code)
    {
        foreach (var count in Counts)
        {
            if (count.Code == code)
            {
                return count.Count;
            }
        }

        return 0;
    }
}

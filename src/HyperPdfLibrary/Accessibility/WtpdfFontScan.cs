// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks the font rules of Well-Tagged PDF 1.0 clause 8.4.5 that repeat the PDF/A-4 font rules (clause 6.2.10), by running the PDF/A font
/// checks and renaming the rules they report. Embedding, Unicode mapping and the <c>.notdef</c> glyph come from the accessibility report
/// instead, so those rules are not repeated here. The checks walk the page content, so the result is kept for the revision of the
/// document's objects it was made at; a check of an unchanged document reuses it.
/// </summary>
internal static class WtpdfFontScan
{
    /// <summary>Reports each font rule of 8.4.5 that a document breaks.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    internal static void Run(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        // Read before the check, so an edit that lands while it runs leaves a result that is already stale.
        var revision = PdfDocumentCaches.Revision(document);
        var result = Volatile.Read(ref document.State.WtpdfFonts);
        if (result is null || result.Revision != revision)
        {
            result = new(revision, Failures(document, cancellationToken));
            Volatile.Write(ref document.State.WtpdfFonts, result);
        }

        foreach (var failure in result.Failures)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, failure.Clause, failure.Description, 0));
        }
    }

    /// <summary>Runs the PDF/A font checks and collects the rules WTPDF repeats that the document breaks.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    /// <returns>The failures.</returns>
    private static WtpdfFontFailure[] Failures(PdfDocument document, CancellationToken cancellationToken)
    {
        var context = new PdfAValidationContext(document, PdfALevel.PdfA4, 0, cancellationToken);
        PdfAFontRules.Check(context);
        var failures = new List<WtpdfFontFailure>();
        foreach (var reference in PdfARuleCatalog.For(PdfALevel.PdfA4))
        {
            if (WtpdfClause(reference.Clause) is { } clause && context.FailureOf(reference.Rule) is not null)
            {
                failures.Add(new(clause, PdfARuleCatalog.Describe(reference.Rule)));
            }
        }

        return [.. failures];
    }

    /// <summary>Gets the WTPDF clause that matches a PDF/A-4 font clause.</summary>
    /// <param name="pdfAClause">The PDF/A-4 clause, such as <c>6.2.10.6</c>.</param>
    /// <returns>The WTPDF clause, or <see langword="null"/> for a rule that is not repeated.</returns>
    private static string? WtpdfClause(string pdfAClause) => pdfAClause switch
    {
        "6.2.10.3.1" => "8.4.5.3.1",
        "6.2.10.3.2" => "8.4.5.3.2",
        "6.2.10.3.3" => "8.4.5.4",
        "6.2.10.5" => "8.4.5.6",
        "6.2.10.6" => "8.4.5.7",
        _ => null,
    };
}

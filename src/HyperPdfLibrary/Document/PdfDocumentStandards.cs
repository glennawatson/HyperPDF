// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Accessibility;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Maps the rules of veraPDF's PDF/UA, tagged PDF, Well-Tagged PDF and WCAG profiles to HyperPdf's checks. For each rule the report
/// says whether HyperPdf checks it, and when it does, whether a document breaks it. A rule HyperPdf does not check, or one that
/// needs a person, is listed apart and is never reported as passing. veraPDF remains the reference for conformance.
/// </summary>
public static class PdfDocumentStandards
{
    /// <summary>Gets every rule of a profile, in the profile's clause order.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The rules, with the clause and test number veraPDF reports for each.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The profile is not a defined profile.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IReadOnlyList<PdfStandardsRule> GetRules(PdfStandardsProfile profile) => StandardsRuleCatalog.For(profile);

    /// <summary>Checks a document against the rules HyperPdf can decide in a profile.</summary>
    /// <param name="document">The document.</param>
    /// <param name="profile">The profile.</param>
    /// <returns>One result for each rule of the profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The profile is not a defined profile.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfStandardsRuleReport Check(PdfDocument document, PdfStandardsProfile profile) => Check(document, profile, CancellationToken.None);

    /// <summary>Checks a document against the rules HyperPdf can decide in a profile.</summary>
    /// <param name="document">The document.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Stops the work between elements and pages.</param>
    /// <returns>One result for each rule of the profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The profile is not a defined profile.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfStandardsRuleReport Check(PdfDocument document, PdfStandardsProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        cancellationToken.ThrowIfCancellationRequested();
        return StandardsRuleEvaluator.Evaluate(document, profile, cancellationToken);
    }

    /// <summary>Checks a document against the rules HyperPdf can decide in a profile, fetching the pages the checks read asynchronously first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Stops the reads and the work between elements and pages.</param>
    /// <returns>One result for each rule of the profile.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The profile is not a defined profile.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfStandardsRuleReport> CheckAsync(PdfDocument document, PdfStandardsProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);

        // The accessibility report reads every page, optional content and embedded file the checks use, and keeps them for the checks below.
        _ = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken).ConfigureAwait(false);
        return StandardsRuleEvaluator.Evaluate(document, profile, cancellationToken);
    }
}

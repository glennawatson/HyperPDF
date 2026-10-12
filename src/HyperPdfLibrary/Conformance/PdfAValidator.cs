// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks a document against a PDF/A level, rule by rule, and reports each rule with its clause and test number as the
/// veraPDF profiles number them. The rules checked give the same pass or fail as veraPDF on the veraPDF test corpus; the
/// report lists the rules that are not checked. This is a practical check, not a certification.
/// </summary>
public static class PdfAValidator
{
    /// <summary>Checks a document against the level it claims.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The report, or <see langword="null"/> when the document claims no PDF/A level.</returns>
    public static PdfAValidationReport? Validate(PdfDocument document) =>
        ClaimedLevel(document) is { } level ? Validate(document, level, CancellationToken.None) : null;

    /// <summary>Checks a document against a PDF/A level.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level.</param>
    /// <returns>The report.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfAValidationReport Validate(PdfDocument document, PdfALevel level) => Validate(document, level, CancellationToken.None);

    /// <summary>Checks a document against a PDF/A level.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Stops the check between objects.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfAValidationReport Validate(PdfDocument document, PdfALevel level, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new PdfAValidationContext(document, level, cancellationToken);
        var areas = PdfARuleAreas.All();
        foreach (var area in areas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            area.Check(context);
        }

        return BuildReport(context, areas);
    }

    /// <summary>Checks a document against a PDF/A level, yielding between rule areas.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level.</param>
    /// <param name="cancellationToken">Stops the check.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfAValidationReport> ValidateAsync(PdfDocument document, PdfALevel level, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var context = new PdfAValidationContext(document, level, cancellationToken);
        var areas = PdfARuleAreas.All();
        foreach (var area in areas)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            area.Check(context);
        }

        return BuildReport(context, areas);
    }

    /// <summary>Gets the PDF/A level a document claims in its XMP <c>pdfaid</c> properties.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The level, or <see langword="null"/> when the document claims none or a level the standards do not define.</returns>
    public static PdfALevel? ClaimedLevel(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfALevels.FromClaim(PdfDocumentConformance.ReadPdfAClaim(PdfDocumentMetadata.GetXmp(document)));
    }

    /// <summary>Turns the recorded failures into the report for the level's rules.</summary>
    /// <param name="context">The finished validation.</param>
    /// <param name="areas">The rule areas that ran.</param>
    /// <returns>The report.</returns>
    private static PdfAValidationReport BuildReport(PdfAValidationContext context, PdfARuleArea[] areas)
    {
        var implemented = new HashSet<PdfARule>();
        foreach (var area in areas)
        {
            implemented.UnionWith(area.Rules);
        }

        var specification = PdfALevels.Specification(context.Level);
        var failed = new List<PdfARuleResult>();
        var results = new List<PdfARuleResult>();
        var notChecked = new List<PdfARuleResult>();
        foreach (var (rule, clause, test) in PdfARuleCatalog.For(context.Level))
        {
            var failure = context.FailureOf(rule);
            var result = new PdfARuleResult(specification, clause, test, PdfARuleCatalog.Describe(rule), failure?.Count ?? 0, failure?.Context);
            if (!implemented.Contains(rule))
            {
                notChecked.Add(result);
                continue;
            }

            results.Add(result);
            if (failure is not null)
            {
                failed.Add(result);
            }
        }

        return new(context.Level, [.. failed], [.. results], [.. notChecked]);
    }
}

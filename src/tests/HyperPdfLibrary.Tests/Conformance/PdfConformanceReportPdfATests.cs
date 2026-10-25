// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>The conformance report's PDF/A part: the claim, the output intents and the rule-by-rule check of the claimed level.</summary>
public sealed class PdfConformanceReportPdfATests
{
    /// <summary>The PDF/A-4 revision year.</summary>
    private const int Part4Revision = 2020;

    /// <summary>The PDF/A part based on PDF 2.0.</summary>
    private const int Part4 = 4;

    /// <summary>The PDF/A part based on PDF 1.7.</summary>
    private const int Part2 = 2;

    /// <summary>
    /// Each part's claim is read with its output intent summary, and the report holds the rule-by-rule check of the claimed
    /// level, the same as the validator gives.
    /// </summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conformance">The claimed conformance letter.</param>
    /// <param name="level">The level that claim names.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B", PdfALevel.PdfA1B)]
    [Arguments(2, "B", PdfALevel.PdfA2B)]
    [Arguments(3, "B", PdfALevel.PdfA3B)]
    [Arguments(Part4, null, PdfALevel.PdfA4)]
    [Arguments(Part4, "E", PdfALevel.PdfA4E)]
    [Arguments(Part4, "F", PdfALevel.PdfA4F)]
    public async Task ClaimedLevelIsCheckedRuleByRule(int part, string? conformance, PdfALevel level)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, part, conformance, part == Part4 ? Part4Revision : null);
        ConformancePdf.Intent(pdf, ConformancePdf.PdfASubtype, ConformancePdf.CmykProfile(), ConformancePdf.CmykComponents);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);

        var report = PdfDocumentConformance.GetConformance(document);

        await Assert.That(report.PdfA!.Part).IsEqualTo(part);
        await Assert.That(report.OutputIntents.Length).IsEqualTo(1);
        await Assert.That(report.OutputIntents[0].ProfileColorSpace).IsEqualTo("CMYK");
        await Assert.That(report.OutputIntents[0].HasProfile).IsTrue();
        await Assert.That(report.PdfAValidation).IsNotNull();
        await Assert.That(report.PdfAValidation!.Level).IsEqualTo(level);
        await Assert.That(report.PdfAValidation.Checked).IsNotEmpty();
        await Assert.That(Ids(report.PdfAValidation.Failed)).IsEquivalentTo(Ids(PdfAValidator.Validate(document, level).Failed));
    }

    /// <summary>Valid PDF/A-1b, 2b and 4 files pass every checked rule of their level.</summary>
    /// <param name="part">The part.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(Part4)]
    public async Task ValidFixturePassesItsLevel(int part)
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(new(part, part == Part4 ? null : "B")), null);

        var report = PdfDocumentConformance.GetConformance(document);

        await Assert.That(report.PdfAValidation!.Passed).IsTrue();
        await Assert.That(report.HasConflicts).IsFalse();
    }

    /// <summary>A file without a PDF/A claim has no rule check; its observations are still listed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileHasNoRuleCheck()
    {
        var pdf = ConformancePdf.Page();
        pdf.Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>";
        pdf.Content = "BT /F1 12 Tf (a) Tj ET";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);

        var report = PdfDocumentConformance.GetConformance(document);

        await Assert.That(report.PdfA).IsNull();
        await Assert.That(report.PdfAValidation).IsNull();
        await Assert.That(report.TryGet(PdfConformanceFinding.NonEmbeddedFont, out _)).IsTrue();
    }

    /// <summary>The asynchronous report gives the same claim, observations and rule results as the synchronous one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsynchronousReportMatches()
    {
        using var document = PdfDocumentReader.Open(Restricted(Part2, "B").ToBytes(), null);

        var sync = PdfDocumentConformance.GetConformance(document);
        var async = await PdfDocumentConformance.GetConformanceAsync(document, CancellationToken.None);

        await Assert.That(async.PdfA).IsEqualTo(sync.PdfA);
        await Assert.That(async.Observations).IsEquivalentTo(sync.Observations);
        await Assert.That(Ids(async.PdfAValidation!.Failed)).IsEquivalentTo(Ids(sync.PdfAValidation!.Failed));
    }

    /// <summary>A cancelled report stops with the token's exception.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledReportThrows()
    {
        using var document = PdfDocumentReader.Open(Restricted(Part2, "B").ToBytes(), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(async () => await PdfDocumentConformance.GetConformanceAsync(document, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(() => PdfDocumentConformance.GetConformance(document, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>
    /// For each part, the report names the clause of a missing annotation appearance or Print flag, a forbidden action, a
    /// font that is not embedded and an embedded file the part does not allow.
    /// </summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conformance">The claimed conformance letter.</param>
    /// <param name="level">The level that claim names.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, "B", PdfALevel.PdfA1B)]
    [Arguments(2, "B", PdfALevel.PdfA2B)]
    [Arguments(3, "B", PdfALevel.PdfA3B)]
    [Arguments(Part4, null, PdfALevel.PdfA4)]
    [Arguments(Part4, "E", PdfALevel.PdfA4E)]
    [Arguments(Part4, "F", PdfALevel.PdfA4F)]
    public async Task RestrictionsAreReportedByClause(int part, string? conformance, PdfALevel level)
    {
        using var document = PdfDocumentReader.Open(Restricted(part, conformance).ToBytes(), null);

        var rules = PdfDocumentConformance.GetConformance(document).PdfAValidation!;

        await Assert.That(rules.Level).IsEqualTo(level);
        foreach (var rule in ExpectedFailures(level))
        {
            await Assert.That(PdfARuleResults.Failed(rules, rule)).IsTrue().Because($"{PdfALevels.Label(level)} should fail {rule}");
        }
    }

    /// <summary>Gets the rules a restricted page breaks at a level: annotation flags and appearance, action, font and embedded file.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The rules.</returns>
    private static PdfARule[] ExpectedFailures(PdfALevel level) => level switch
    {
        PdfALevel.PdfA1B => [PdfARule.AnnotationFlagsPdfA1, PdfARule.ActionTypePdfA1, PdfARule.FontEmbedded, PdfARule.EmbeddedFilesForbidden],
        PdfALevel.PdfA2B => [PdfARule.AnnotationFlagsPresent, PdfARule.AnnotationAppearancePdfA2, PdfARule.ActionTypePdfA2, PdfARule.FontEmbedded, PdfARule.EmbeddedFilePdfA12],
        PdfALevel.PdfA3B => [PdfARule.AnnotationFlagsPresent, PdfARule.AnnotationAppearancePdfA2, PdfARule.ActionTypePdfA2, PdfARule.FontEmbedded, PdfARule.EmbeddedFileMimeType],
        PdfALevel.PdfA4F => [PdfARule.AnnotationFlagsPresentPdfA4, PdfARule.AnnotationAppearancePdfA4, PdfARule.ActionTypePdfA4, PdfARule.FontEmbedded, PdfARule.EmbeddedFileMimeType],
        PdfALevel.PdfA4E => [PdfARule.AnnotationFlagsPresentPdfA4, PdfARule.AnnotationAppearancePdfA4, PdfARule.ActionTypePdfA4E, PdfARule.FontEmbedded, PdfARule.EmbeddedFileMimeType],
        _ => [PdfARule.AnnotationFlagsPresentPdfA4, PdfARule.AnnotationAppearancePdfA4, PdfARule.ActionTypePdfA4, PdfARule.FontEmbedded, PdfARule.EmbeddedFilePdfA124],
    };

    /// <summary>
    /// Builds a claimed page with a square annotation that has no appearance or flags, a Launch open action, text in an
    /// unembedded font and an embedded text file.
    /// </summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="conformance">The claimed conformance letter.</param>
    /// <returns>The builder.</returns>
    private static RenderTestPdf Restricted(int part, string? conformance)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, part, conformance, part == Part4 ? Part4Revision : null);
        pdf.Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>";
        pdf.Content = "BT /F1 12 Tf (a) Tj ET";
        var annotation = pdf.AddObject("<< /Type /Annot /Subtype /Square /Rect [10 10 50 50] >>");
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{annotation} 0 R]");
        var data = pdf.AddStream("/Type /EmbeddedFile", "notes");
        var spec = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Filespec /F (notes.txt) /UF (notes.txt) /EF << /F {data} 0 R /UF {data} 0 R >> >>"));
        pdf.CatalogEntries += string.Create(
            CultureInfo.InvariantCulture,
            $" /OpenAction << /S /Launch /F (calc) >> /Names << /EmbeddedFiles << /Names [(notes.txt) {spec} 0 R] >> >>");
        return pdf;
    }

    /// <summary>Gets results as <c>clause:test</c>.</summary>
    /// <param name="results">The results.</param>
    /// <returns>The ids.</returns>
    private static string[] Ids(PdfARuleResult[] results) => Array.ConvertAll(results, static result => $"{result.Clause}:{result.Test}");
}

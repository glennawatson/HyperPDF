// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks how <see cref="PdfAValidator"/> finds the claimed level and reports rules by clause.</summary>
public sealed class PdfAValidatorTests
{
    /// <summary>The PDF/A part a test file claims.</summary>
    private const int Part2 = 2;

    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>Gets every level.</summary>
    /// <returns>The levels.</returns>
    public static IEnumerable<PdfALevel> Levels() => Enum.GetValues<PdfALevel>();

    /// <summary>A valid fixture reports its claimed level, passes, and lists every rule of the level once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidFixturePassesItsClaimedLevel()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A2B), null);

        var report = PdfAValidator.Validate(document);

        await Assert.That(report).IsNotNull();
        await Assert.That(report!.Level).IsEqualTo(PdfALevel.PdfA2B);
        await Assert.That(report.Passed).IsTrue();
        await Assert.That(report.Checked).IsNotEmpty();
        await Assert.That(report.Checked.Length + report.Unchecked.Length).IsEqualTo(PdfARuleCatalog.For(PdfALevel.PdfA2B).Length);
        await Assert.That(Array.TrueForAll(report.Checked, static rule => rule.Specification == "ISO 19005-2:2011")).IsTrue();
    }

    /// <summary>A file without a <c>pdfaid</c> claim has no level and no report.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileHasNoReport()
    {
        using var document = PdfDocumentReader.Open(ConformancePdf.Page().ToBytes(), null);

        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsNull();
        await Assert.That(PdfAValidator.Validate(document)).IsNull();
    }

    /// <summary>A broken rule is reported with the clause and test number of the level checked.</summary>
    /// <param name="part">The claimed part.</param>
    /// <param name="clause">The clause the level numbers the rule by.</param>
    /// <param name="test">The test number within the clause.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Part1, "6.9", "1")]
    [Arguments(Part2, "6.4.1", "3")]
    public async Task NeedAppearancesFailsWithTheLevelsClause(int part, string clause, string test)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, part, "B", null);
        pdf.CatalogEntries += " /AcroForm << /Fields [] /NeedAppearances true >>";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);

        var report = PdfAValidator.Validate(document);

        await Assert.That(report).IsNotNull();
        var failure = report!.Failed.Single(rule => rule.Clause == clause && rule.Test == test);
        await Assert.That(failure.Failures).IsEqualTo(1);
        await Assert.That(failure.Context).IsEqualTo("interactive form");
        await Assert.That(failure.Description).Contains("NeedAppearances");
        await Assert.That(report.Passed).IsFalse();
    }

    /// <summary>A file can be checked against a level it does not claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksAnUnclaimedLevelOnRequest()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A2B), null);

        var report = PdfAValidator.Validate(document, PdfALevel.PdfA4);

        await Assert.That(report.Level).IsEqualTo(PdfALevel.PdfA4);
        await Assert.That(Array.TrueForAll(report.Checked, static rule => rule.Specification == "ISO 19005-4:2020")).IsTrue();
    }

    /// <summary>A cancelled validation stops with the token's exception.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledValidationThrows()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A1B), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(async () => await PdfAValidator.ValidateAsync(document, PdfALevel.PdfA1B, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Every level maps back from the claim it writes and has a label and standard.</summary>
    /// <param name="level">The level.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Levels))]
    public async Task LevelsRoundTripThroughTheirClaim(PdfALevel level)
    {
        var claim = new PdfAClaim(PdfALevels.Part(level), PdfALevels.Conformance(level), null);

        await Assert.That(PdfALevels.FromClaim(claim)).IsEqualTo(level);
        await Assert.That(PdfALevels.Label(level)).StartsWith("PDF/A-");
        await Assert.That(PdfALevels.Specification(level)).StartsWith("ISO 19005-");
        await Assert.That(PdfARuleCatalog.For(level)).IsNotEmpty();
    }
}

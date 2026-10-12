// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks that the writers keep a PDF/A claim only when the file they write meets it.</summary>
public sealed class PdfAClaimGuardTests
{
    /// <summary>The PDF/A part the broken file claims.</summary>
    private const int Part2 = 2;

    /// <summary>A valid PDF/A-2b file keeps its claim through a compact save, and the report says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveKeepsValidClaim()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A2B), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(report.Claim).IsNotNull();
        await Assert.That(report.Claim!.Removed).IsFalse();
        await Assert.That(report.Claim.Level).IsEqualTo(PdfALevel.PdfA2B);
        await Assert.That(ClaimOf(output)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>A file that breaks its level loses the claim in a compact save; the edit can be undone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveRemovesBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(BrokenPdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(report.Claim).IsNotNull();
        await Assert.That(report.Claim!.Removed).IsTrue();
        await Assert.That(report.Claim.Level).IsEqualTo(PdfALevel.PdfA2B);
        await Assert.That(Array.Exists(report.Claim.Broken, static rule => rule.Clause == "6.4.1" && rule.Test == "3")).IsTrue();
        await Assert.That(ClaimOf(output)).IsNull();
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsNull();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>An edited file that breaks its level loses the claim in the incremental update.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveRemovesBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(BrokenPdfA2B(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new PdfMetadataEdit { Title = "Edited" });
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfIncrementalWriter.Save(document.Objects, output, report);

        await Assert.That(report.Claim!.Removed).IsTrue();
        await Assert.That(ClaimOf(output)).IsNull();
    }

    /// <summary>An unedited save writes the original bytes, so it neither checks nor changes the claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UneditedSaveLeavesClaimAlone()
    {
        using var document = PdfDocumentReader.Open(BrokenPdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfIncrementalWriter.Save(document.Objects, output, report);

        await Assert.That(report.Claim).IsNull();
        await Assert.That(ClaimOf(output)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>A linearized save also removes a broken claim and reports it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedSaveRemovesBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(BrokenPdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfLinearizedWriter.SaveAsync(document, output, report, CancellationToken.None);

        await Assert.That(report.Claim!.Removed).IsTrue();
        await Assert.That(ClaimOf(output)).IsNull();
    }

    /// <summary>The optimiser keeps a valid claim and reports the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserKeepsValidClaim()
    {
        using var document = PdfDocumentReader.Open(PdfAFixtures.Create(PdfAFixtureLevel.A2B), null);
        await using var output = new MemoryStream();

        var report = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.KeepQuality);

        await Assert.That(report.PdfAClaim).IsNotNull();
        await Assert.That(report.PdfAClaim!.Removed).IsFalse();
        await Assert.That(ClaimOf(output)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>The optimiser removes a claim its copy does not meet, and reports the broken rules.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserRemovesBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(BrokenPdfA2B(), null);
        await using var output = new MemoryStream();

        var report = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.KeepQuality);

        await Assert.That(report.PdfAClaim).IsNotNull();
        await Assert.That(report.PdfAClaim!.Removed).IsTrue();
        await Assert.That(report.PdfAClaim.Broken).IsNotEmpty();
        await Assert.That(ClaimOf(output)).IsNull();
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>Builds a page that claims PDF/A-2b but asks viewers to regenerate form appearances, which the level forbids.</summary>
    /// <returns>The file.</returns>
    private static byte[] BrokenPdfA2B()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, Part2, "B", null);
        pdf.CatalogEntries += " /AcroForm << /Fields [] /NeedAppearances true >>";
        return pdf.ToBytes();
    }

    /// <summary>Reads the claim of a saved file.</summary>
    /// <param name="output">The saved file.</param>
    /// <returns>The claimed level, or <see langword="null"/>.</returns>
    private static PdfALevel? ClaimOf(MemoryStream output)
    {
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        return PdfAValidator.ClaimedLevel(saved);
    }
}

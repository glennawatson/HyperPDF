// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Checks that the writers keep a PDF/A claim only when the file they write meets it: a broken file is repaired when it
/// can be, and otherwise loses its claim.
/// </summary>
public sealed class PdfAClaimGuardTests
{
    /// <summary>The PDF/A part the broken files claim.</summary>
    private const int Part2 = 2;

    /// <summary>The PDF/A-2 rule against asking viewers to draw form appearances.</summary>
    private const string NeedAppearancesRule = "6.4.1:3";

    /// <summary>Gets the edit that makes an incremental save write an update.</summary>
    private static PdfMetadataEdit TitleEdit => new() { Title = "Edited" };

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
        await Assert.That(report.Claim.Repaired).IsFalse();
        await Assert.That(report.Claim.Level).IsEqualTo(PdfALevel.PdfA2B);
        await Assert.That(ClaimOf(output)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>
    /// A file that breaks its level is repaired in a compact save: the saved copy meets the level, the report lists the
    /// repair, and the document in memory is not changed.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveRepairsBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        var undo = PdfDocumentEditing.GetHistory(document).UndoCount;
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await AssertRepaired(report.Claim, output);
        await Assert.That(report.Claim!.Rewritten).IsFalse();
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsEqualTo(PdfALevel.PdfA2B);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(undo);
    }

    /// <summary>The asynchronous compact save repairs a broken file too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveAsyncRepairsBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, CancellationToken.None);

        await AssertRepaired(report.Claim, output);
    }

    /// <summary>An edited file that breaks its level is written in full, repaired, instead of as an incremental update.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveRepairsBrokenClaimInFull()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfIncrementalWriter.Save(document.Objects, output, report);

        await AssertRepaired(report.Claim, output);
        await Assert.That(report.Claim!.Rewritten).IsTrue();
    }

    /// <summary>The asynchronous incremental save repairs a broken file too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveAsyncRepairsBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, CancellationToken.None);

        await AssertRepaired(report.Claim, output);
        await Assert.That(report.Claim!.Rewritten).IsTrue();
    }

    /// <summary>An unedited save writes the original bytes, so it neither checks nor changes the claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UneditedSaveLeavesClaimAlone()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfIncrementalWriter.Save(document.Objects, output, report);

        await Assert.That(report.Claim).IsNull();
        await Assert.That(ClaimOf(output)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>A linearized save repairs a broken file and still writes a linearized file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedSaveRepairsBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfLinearizedWriter.SaveAsync(document, output, report, CancellationToken.None);

        await AssertRepaired(report.Claim, output);
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfDocumentLinearization.GetInfo(saved)).IsNotNull();
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
        await Assert.That(report.PdfAClaim.Repaired).IsFalse();
        await Assert.That(ClaimOf(output)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>The optimiser repairs a copy that breaks its level, and reports the repair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserRepairsBrokenClaim()
    {
        using var document = PdfDocumentReader.Open(RepairablePdfA2B(), null);
        await using var output = new MemoryStream();

        var report = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.KeepQuality, null, CancellationToken.None);

        await AssertRepaired(report.PdfAClaim, output);
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>
    /// A file the conversion cannot repair, DeviceCMYK paint with no CMYK profile, loses its claim in a compact save; the
    /// report gives the reasons, and the removal can be undone.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveRemovesUnrepairableClaim()
    {
        using var document = PdfDocumentReader.Open(UnrepairablePdfA2B(), null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await AssertRemoved(report.Claim, output);
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsNull();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>An edited file the conversion cannot repair loses its claim in the incremental update.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveRemovesUnrepairableClaim()
    {
        using var document = PdfDocumentReader.Open(UnrepairablePdfA2B(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, CancellationToken.None);

        await AssertRemoved(report.Claim, output);
        await Assert.That(report.Claim!.Rewritten).IsFalse();
    }

    /// <summary>
    /// A signed file is never rewritten to repair it, as that would break its signatures: its incremental update removes
    /// the claim instead and keeps the signed bytes.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedFileLosesClaimIncrementally()
    {
        var source = ConversionTestPdf.ClaimedSigned();
        using var document = PdfDocumentReader.Open(source, null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfIncrementalWriter.Save(document.Objects, output, report);

        await AssertRemoved(report.Claim, output);
        await Assert.That(output.ToArray().AsSpan(0, source.Length).SequenceEqual(source)).IsTrue();
        await Assert.That(Array.Exists(report.Claim!.Problems, static note => note.Description.Contains("signed", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>The optimiser removes a claim its copy cannot meet, and reports why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserRemovesUnrepairableClaim()
    {
        using var document = PdfDocumentReader.Open(UnrepairablePdfA2B(), null);
        await using var output = new MemoryStream();

        var report = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.KeepQuality);

        await AssertRemoved(report.PdfAClaim, output);
        await Assert.That(PdfAValidator.ClaimedLevel(document)).IsEqualTo(PdfALevel.PdfA2B);
    }

    /// <summary>Builds a page that claims PDF/A-2b but asks viewers to draw form appearances, which the level forbids.</summary>
    /// <returns>The file.</returns>
    internal static byte[] RepairablePdfA2B()
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, Part2, "B", null);
        pdf.CatalogEntries += " /AcroForm << /Fields [] /NeedAppearances true >>";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that claims PDF/A-2b and paints in DeviceCMYK with no output intent to give it meaning.</summary>
    /// <returns>The file.</returns>
    internal static byte[] UnrepairablePdfA2B()
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = "0 0 0 1 k 10 10 50 50 re f";
        ConformancePdf.Claim(pdf, Part2, "B", null);
        return pdf.ToBytes();
    }

    /// <summary>Checks a check reports a repair, and the saved file meets its level.</summary>
    /// <param name="check">The check.</param>
    /// <param name="output">The saved file.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRepaired(PdfAClaimCheck? check, MemoryStream output)
    {
        await Assert.That(check).IsNotNull();
        await Assert.That(check!.Repaired).IsTrue();
        await Assert.That(check.Removed).IsFalse();
        await Assert.That(check.Repairs).IsNotEmpty();
        await Assert.That(Array.Exists(check.Broken, static rule => $"{rule.Clause}:{rule.Test}" == NeedAppearancesRule)).IsTrue();
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfAValidator.ClaimedLevel(saved)).IsEqualTo(PdfALevel.PdfA2B);
        await Assert.That(PdfAValidator.Validate(saved)!.Passed).IsTrue();
    }

    /// <summary>Checks a check reports a removed claim with reasons, and the saved file claims nothing.</summary>
    /// <param name="check">The check.</param>
    /// <param name="output">The saved file.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRemoved(PdfAClaimCheck? check, MemoryStream output)
    {
        await Assert.That(check).IsNotNull();
        await Assert.That(check!.Removed).IsTrue();
        await Assert.That(check.Repaired).IsFalse();
        await Assert.That(check.Problems).IsNotEmpty();
        await Assert.That(check.Broken).IsNotEmpty();
        await Assert.That(ClaimOf(output)).IsNull();
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

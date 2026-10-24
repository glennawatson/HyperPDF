// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Tests.Accessibility;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>Checks that a file claiming PDF/UA-2 (ISO 14289-2:2024) is checked on save with part 2 clauses, and is not held to part 1 rules.</summary>
public sealed class PdfUaPart2ClaimGuardTests
{
    /// <summary>The title an edit sets, so the save has something to write.</summary>
    private const string EditedTitle = "Edited";

    /// <summary>A PDF/UA-2 claim in a PDF 1.7 file cannot be kept: the save refuses, names part 2 and cites clause 5.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaveRefusesAPart2ClaimInAPdf17File(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(PdfUaClaim.Part2), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        var error = await Assert.That(() => PdfLinearizedWriter.SaveAsync(document, output, report, cancellationToken)).Throws<PdfException>();

        using (Assert.Multiple())
        {
            await Assert.That(error!.Error).IsEqualTo(PdfError.ClaimUnmet);
            await Assert.That(error.Message).Contains("PDF/UA-2");
            await Assert.That(report.UaClaim!.Problems.Any(static p => p.Requirement == PdfUaRequirement.Part2Identification && p.Clause == "5")).IsTrue();
            await Assert.That(report.UaClaim.Problems.Any(static p => p.Requirement == PdfUaRequirement.StructureTree && p.Clause == "8.2")).IsTrue();
            await Assert.That(output.Length).IsEqualTo(0);
        }
    }

    /// <summary>Choosing to remove the claim saves the file without a PDF/UA part.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingAPart2ClaimSavesAnUnclaimedFile(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(PdfUaClaim.Part2), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });
        var report = new PdfSaveReport { UnmetUaClaim = PdfUaUnmetClaimAction.RemoveClaim };
        await using var output = new MemoryStream();

        await PdfLinearizedWriter.SaveAsync(document, output, report, cancellationToken);

        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var after = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(report.UaClaim!.Removed).IsTrue();
        await Assert.That(after.Claim.Part).IsNotEqualTo(PdfUaClaim.Part2);
    }

    /// <summary>A file with no claim is saved without any PDF/UA check, and a part 1 rule is not applied to a part 2 claim.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileSavesAndPart2SkipsPart1OnlyRules(CancellationToken cancellationToken)
    {
        using var plain = await PdfDocumentReader.OpenAsync(Build(0), null, cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(plain, new() { Title = EditedTitle });
        var plainReport = new PdfSaveReport();
        await using var plainOutput = new MemoryStream();

        await PdfLinearizedWriter.SaveAsync(plain, plainOutput, plainReport, cancellationToken);

        using var part2 = await PdfDocumentReader.OpenAsync(Build(PdfUaClaim.Part2), null, cancellationToken);
        var findings = PdfUaPreflight.Check(part2.Objects, false, PdfUaClaim.Part2, cancellationToken);
        await Assert.That(plainReport.UaClaim).IsNull();
        var part1Only = findings.Exists(static f => f.Requirement is PdfUaRequirement.EmbeddedFileFilenames or PdfUaRequirement.AssistiveTechnologyAccess or PdfUaRequirement.TrapNetAnnotations);
        await Assert.That(part1Only).IsFalse();
    }

    /// <summary>Builds a one page PDF 1.7 file with a PDF/UA identification.</summary>
    /// <param name="part">The claimed part; 0 for none.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int part) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /Metadata 3 0 R >>",
        "<< /Type /Pages /Count 1 /Kids [4 0 R] >>",
        MiniPdf.Stream("/Type /Metadata /Subtype /XML", AccessibilityPdfs.Packet(part, true, part == 0 ? null : "2024")),
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
    ]);
}

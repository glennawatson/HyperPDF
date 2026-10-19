// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Checks created claims and the description strings actually written to structure elements.</summary>
public sealed class PdfTagDescriptionScanTests
{
    /// <summary>The page width.</summary>
    private const float Width = 300F;

    /// <summary>The page height.</summary>
    private const float Height = 400F;

    /// <summary>The standard mathematical-expression type.</summary>
    private const string Formula = "Formula";

    /// <summary>The standard illustration type.</summary>
    private const string Figure = "Figure";

    /// <summary>A part 1 formula needs Alt even when ActualText is present.</summary>
    /// <param name="alternative">The requested Alt value, or null for none.</param>
    /// <param name="expectedClaim">Whether the created file may retain its part 1 claim.</param>
    /// <param name="cancellationToken">Cancels reading the written file.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(null, false)]
    [Arguments("", true)]
    [Arguments("x plus y", true)]
    public async Task Count_Formula_ControlsPartOneClaim(string? alternative, bool expectedClaim, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = Formula, Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part1 });
        _ = creator.BeginPage(Width, Height);
        _ = creator.Tags!.AddElement(creator.Tags.Root, Formula, new() { AlternativeText = alternative, ActualText = "x plus y" });
        var created = creator.Finish();
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(created.ClaimsPdfUa).IsEqualTo(expectedClaim);
        await Assert.That(report.Claim.IsClaimed).IsEqualTo(expectedClaim);
        var element = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        await Assert.That(element.Dictionary.GetText(HyperPdfLibrary.Objects.KnownName.Alt)).IsEqualTo(alternative);
    }

    /// <summary>An explicitly empty replacement string is retained and is not treated as a missing figure description.</summary>
    /// <param name="cancellationToken">Cancels reading the written file.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Count_FigureWithEmptyActualText_PreservesReplacement(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = Figure, Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part1 });
        _ = creator.BeginPage(Width, Height);
        _ = creator.Tags!.AddElement(creator.Tags.Root, Figure, new() { ActualText = string.Empty });
        var created = creator.Finish();
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var element = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

        await Assert.That(created.ClaimsPdfUa).IsTrue();
        await Assert.That(element.Dictionary.GetText(HyperPdfLibrary.Objects.KnownName.ActualText)).IsEqualTo(string.Empty);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(0);
    }

    /// <summary>The part 1 Alt-only rule does not replace PDF/UA-2's separate formula requirements.</summary>
    /// <param name="part">The requested PDF/UA part.</param>
    /// <param name="expected">The expected number of description defects.</param>
    /// <param name="cancellationToken">Cancels the description scan.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(PdfUaConformance.Part1, 1)]
    [Arguments(PdfUaConformance.Part2, 0)]
    public async Task Count_UsesRequestedPart(PdfUaConformance part, int expected, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        var section = tags.AddElement(tags.Root, "Sect");
        _ = tags.AddElement(section, Formula, new() { ActualText = "x" });
        _ = tags.AddElement(section, Figure, new() { ActualText = string.Empty });
        var count = PdfTagDescriptionScan.Count(tags.Root, creator.Builder.ImageScratchPools, part, cancellationToken);

        await Assert.That(count).IsEqualTo(expected);
    }

    /// <summary>An untagged document has no structure descriptions to scan.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Count_Untagged_ReturnsZero(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new());
        var count = PdfTagDescriptionScan.Count(null, creator.Builder.ImageScratchPools, PdfUaConformance.Part1, cancellationToken);

        await Assert.That(count).IsEqualTo(0);
    }

    /// <summary>A cancelled scan stops before leasing work storage.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Count_Cancelled_Throws()
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(() => PdfTagDescriptionScan.Count(creator.Tags!.Root, creator.Builder.ImageScratchPools, PdfUaConformance.Part1, cancellation.Token)).Throws<OperationCanceledException>();
    }
}

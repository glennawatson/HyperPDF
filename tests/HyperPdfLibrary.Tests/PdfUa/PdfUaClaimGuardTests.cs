// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.PdfUa;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Tests.Accessibility;
using HyperPdfLibrary.Tests.Creation;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.PdfUa;

/// <summary>
/// Checks that every writer keeps a PDF/UA-1 claim true (ISO 14289-1:2014, clauses 5 and 7): a valid file keeps its claim
/// through an edit, a defect a repair can fix is fixed and reported, and a defect it cannot fix stops the save with the
/// reason or, when the caller chose, removes the claim.
/// </summary>
public sealed class PdfUaClaimGuardTests
{
    /// <summary>The width of the test page.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test page.</summary>
    private const float PageHeight = 400F;

    /// <summary>The size of the test image in pixels.</summary>
    private const int ImageSize = 4;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The description of an annotation that can be tagged.</summary>
    private const string Described = "Marked for redaction";

    /// <summary>The left edge of the marked area.</summary>
    private const float MarkLeft = 10F;

    /// <summary>The bottom of the marked area.</summary>
    private const float MarkBottom = 10F;

    /// <summary>The right edge of the marked area.</summary>
    private const float MarkRight = 100F;

    /// <summary>The top of the marked area.</summary>
    private const float MarkTop = 40F;

    /// <summary>The level of the second heading.</summary>
    private const int SecondLevel = 2;

    /// <summary>The standard mathematical-expression type.</summary>
    private const string Formula = "Formula";

    /// <summary>Gets an area of the test page to mark for redaction.</summary>
    private static PdfRectangle MarkedArea { get; } = new(MarkLeft, MarkBottom, MarkRight, MarkTop);

    /// <summary>Gets an edit that makes the document changed.</summary>
    private static PdfMetadataEdit TitleEdit => new() { Title = "Edited" };

    /// <summary>Compact and incremental saves refuse a part 1 formula whose ActualText does not supply Alt.</summary>
    /// <param name="incremental">Whether to use the incremental writer.</param>
    /// <param name="cancellationToken">Cancels saving.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task FormulaWithoutAltRefusesSave(bool incremental, CancellationToken cancellationToken)
    {
        var bytes = AccessibilityPdfs.Build(new()
        {
            Content = AccessibilityPdfs.Lines(1),
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element(Formula, parent, "/K 0 /ActualText (x plus y)")),
        });
        using var document = PdfDocumentReader.Open(bytes, null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        var thrown = await Assert.ThrowsAsync<PdfException>(async () =>
        {
            if (incremental)
            {
                await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken);
            }
            else
            {
                await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
            }
        });

        await Assert.That(thrown!.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(thrown.Message).Contains("Clause 7.7");
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(report.UaClaim!.GetUnrepaired().Select(static problem => problem.Requirement)).Contains(PdfUaRequirement.FormulaAlternative);
    }

    /// <summary>Removing an unmet claim preserves the readable formula and its replacement text.</summary>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task FormulaWithoutAltCanRemoveClaim(CancellationToken cancellationToken)
    {
        var bytes = AccessibilityPdfs.Build(new()
        {
            Content = AccessibilityPdfs.Lines(1),
            Layout = static (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element(Formula, parent, "/K 0 /ActualText (x plus y)")),
        });
        using var document = PdfDocumentReader.Open(bytes, null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport { UnmetUaClaim = PdfUaUnmetClaimAction.RemoveClaim };
        await using var output = new MemoryStream();

        await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);

        await Assert.That(report.UaClaim!.Removed).IsTrue();
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(accessibility.Claim.Part).IsNotEqualTo(1);
        var formula = PdfDocumentTagged.GetStructureTree(saved)!.Roots[0].Kids[0].Element!;
        await Assert.That(formula.RawType).IsEqualTo(Formula);
        await Assert.That(formula.ActualText).IsEqualTo("x plus y");
    }

    /// <summary>Saving preserves an Alt string, including an empty string, without inventing a description.</summary>
    /// <param name="description">The formula's alternate text.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("x plus y")]
    public async Task FormulaWithAltPreservesClaim(string description, CancellationToken cancellationToken)
    {
        var spec = new AccessibilitySpec { Content = AccessibilityPdfs.Lines(1), Layout = (pdf, parent) => AccessibilityPdfs.Kids(pdf.Element(Formula, parent, $"/K 0 /Alt ({description})")) };
        using var document = PdfDocumentReader.Open(AccessibilityPdfs.Build(spec), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);

        await Assert.That(report.UaClaim!.Problems).IsEmpty();
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(saved, cancellationToken);
        await Assert.That(accessibility.Claim.Part).IsEqualTo(1);
        var formula = PdfDocumentTagged.GetStructureTree(saved)!.Roots[0].Kids[0].Element!;
        await Assert.That(formula.Dictionary.Get(KnownName.Alt).Kind).IsEqualTo(PdfKind.String);
        await Assert.That(formula.Dictionary.GetText(KnownName.Alt)).IsEqualTo(description);
    }

    /// <summary>A valid claimed file keeps its claim, and nothing is repaired, through a compact save after a metadata edit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MetadataEditKeepsValidClaim()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(report.UaClaim).IsNotNull();
        await Assert.That(report.UaClaim!.Problems).IsEmpty();
        await AssertValidClaim(output);
    }

    /// <summary>A file without a claim is not checked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnclaimedFileIsNotChecked()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Plain", Language = "en", Tagged = true });
        _ = creator.BeginPage(PageWidth, PageHeight);
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(report.UaClaim).IsNull();
    }

    /// <summary>An annotation added to a claimed file is tagged and given a tab order in the written file, and the report lists both repairs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationEditIsRepairedAndReported()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        AddAnnotation(document, Described);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfIncrementalWriter.Save(document.Objects, output, report);

        var repaired = report.UaClaim!.GetRepaired().Select(static problem => problem.Requirement).ToArray();
        await Assert.That(repaired).Contains(PdfUaRequirement.AnnotationTagged);
        await Assert.That(report.UaClaim.GetUnrepaired()).IsEmpty();
        await AssertValidClaim(output);
    }

    /// <summary>An annotation with no description cannot be tagged without inventing one, so the save is refused and says why.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndescribedAnnotationRefusesTheSave()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        AddAnnotation(document, null);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        var thrown = Assert.Throws<PdfException>(() => PdfIncrementalWriter.Save(document.Objects, output, report));

        await Assert.That(thrown!.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(thrown.Message).Contains("Clause 7.18.1");
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>The asynchronous incremental save repairs the same defects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncIncrementalSaveRepairsAnnotation()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        AddAnnotation(document, Described);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, CancellationToken.None);

        await Assert.That(report.UaClaim!.GetRepaired()).IsNotEmpty();
        await AssertValidClaim(output);
    }

    /// <summary>Applying a redaction to a claimed file leaves a file that still meets the claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactionKeepsValidClaim()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        _ = PdfRedactions.Add(document, 0, [MarkedArea], PdfRedactionAppearance.Black);
        await using var output = new MemoryStream();

        _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);

        await AssertValidClaim(output);
    }

    /// <summary>Copying a page of a claimed file into itself keeps the claim and the tags.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageCopyKeepsValidClaim()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        PdfDocumentPageOperations.InsertPages(document, 1, document, [0]);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(report.UaClaim!.GetUnrepaired()).IsEmpty();
        await AssertValidClaim(output);
    }

    /// <summary>The optimiser keeps a valid claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserKeepsValidClaim()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        await using var output = new MemoryStream();

        var report = await PdfOptimizer.OptimizeAsync(document, output, PdfOptimizeOptions.KeepQuality, null, CancellationToken.None);

        await Assert.That(report.PdfUaClaim).IsNotNull();
        await Assert.That(report.PdfUaClaim!.GetUnrepaired()).IsEmpty();
        await AssertValidClaim(output);
    }

    /// <summary>A defect no repair can fix refuses the save with the clause and the reason, and writes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UncorrectableDefectRefusesTheSave()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        RemoveStructureTree(document);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        var thrown = Assert.Throws<PdfException>(() => PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report));

        await Assert.That(thrown!.Error).IsEqualTo(PdfError.ClaimUnmet);
        await Assert.That(thrown.Message).Contains("PDF/UA-1");
        await Assert.That(thrown.Message).Contains("Clause 7.1");
        await Assert.That(output.Length).IsEqualTo(0);
        await Assert.That(report.UaClaim!.GetUnrepaired().Select(static problem => problem.Requirement)).Contains(PdfUaRequirement.StructureTree);
    }

    /// <summary>When the caller chooses to remove the claim, the file is saved without it and the report says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UncorrectableDefectCanRemoveTheClaim()
    {
        using var document = PdfDocumentReader.Open(CreateClaimed(), null);
        RemoveStructureTree(document);
        var report = new PdfSaveReport { UnmetUaClaim = PdfUaUnmetClaimAction.RemoveClaim };
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(report.UaClaim!.Removed).IsTrue();
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfDocumentAccessibility.GetAccessibilityReport(saved).Claim.Part).IsNotEqualTo(1);
    }

    /// <summary>The optimiser refuses a file whose claim it cannot keep, unless the options remove the claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimiserRefusesOrRemovesAnUnmetClaim()
    {
        using var refused = PdfDocumentReader.Open(AccessibilityPdfs.Build(new() { Tree = false }), null);
        await using var none = new MemoryStream();

        var thrown = Assert.ThrowsAsync<PdfException>(async () => await PdfOptimizer.OptimizeAsync(refused, none, PdfOptimizeOptions.KeepQuality, null, CancellationToken.None));

        await Assert.That((await thrown)!.Error).IsEqualTo(PdfError.ClaimUnmet);

        using var removed = PdfDocumentReader.Open(AccessibilityPdfs.Build(new() { Tree = false }), null);
        await using var output = new MemoryStream();
        var report = await PdfOptimizer.OptimizeAsync(removed, output, PdfOptimizeOptions.KeepQuality with { UnmetPdfUaClaim = PdfUaUnmetClaimAction.RemoveClaim }, null, CancellationToken.None);

        await Assert.That(report.PdfUaClaim!.Removed).IsTrue();
    }

    /// <summary>Makes a PDF/UA-1 file with a heading, a paragraph and a described image.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateClaimed()
    {
        var creator = new PdfDocumentCreator(new() { Title = "Accessible", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part1 });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var pixels = new byte[ImageSize * ImageSize * BgraBytes];
        Array.Fill(pixels, byte.MaxValue);
        var image = creator.AddRaster(new(PdfRasterFormat.Bgra32, ImageSize, ImageSize, pixels), PdfImageOptions.Default, CancellationToken.None);
        PdfFlowBlock[] blocks =
        [
            new PdfFlowHeading(1, "A Title"),
            new PdfFlowParagraph("First paragraph of text."),
            new PdfFlowHeading(SecondLevel, "A Section"),
            new PdfFlowImage(image, "A white square", 0),
        ];
        _ = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight }, CancellationToken.None);
        _ = creator.Finish();
        return creator.ToArray();
    }

    /// <summary>Removes the catalog's structure tree, which no repair can restore, as an edit.</summary>
    /// <param name="document">The document.</param>
    private static void RemoveStructureTree(PdfDocument document)
    {
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        _ = document.Objects.Catalog.Remove(KnownName.StructTreeRoot);
    }

    /// <summary>Adds a redact annotation to the first page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="contents">The annotation's description, or <see langword="null"/> for none.</param>
    private static void AddAnnotation(PdfDocument document, string? contents)
    {
        var annotation = PdfRedactions.CreateAnnotation(document.Objects, [MarkedArea], PdfRedactionAppearance.Black);
        if (contents is not null)
        {
            annotation.Set(KnownName.Contents, PdfValue.FromString(Encoding.ASCII.GetBytes(contents)));
        }

        _ = PdfPageAnnotations.Append(document.Objects, PdfDocumentPages.GetPage(document, 0), annotation);
    }

    /// <summary>Asserts that a saved file still claims PDF/UA-1 and that checking it again, with a save, finds nothing wrong.</summary>
    /// <param name="output">The saved file.</param>
    /// <returns>A task.</returns>
    private static async Task AssertValidClaim(MemoryStream output)
    {
        using var saved = PdfDocumentReader.Open(output.ToArray(), null);
        var accessibility = PdfDocumentAccessibility.GetAccessibilityReport(saved);
        await Assert.That(accessibility.Claim.Part).IsEqualTo(1);
        var report = new PdfSaveReport();
        await using var again = new MemoryStream();

        PdfCompactWriter.Save(saved.Objects, PdfCompactOptions.Default, again, report);

        await Assert.That(report.UaClaim).IsNotNull();
        await Assert.That(report.UaClaim!.Problems).IsEmpty();
    }
}

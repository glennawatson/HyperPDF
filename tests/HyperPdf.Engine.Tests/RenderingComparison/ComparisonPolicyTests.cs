// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Proves that renderer agreement cannot override an independent standards oracle.</summary>
public sealed class ComparisonPolicyTests
{
    /// <summary>Identical wrong outputs fail even when all three engines agree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharedWrongOutputCannotPass()
    {
        var wrong = ComparisonTestRasters.Solid(ComparisonTestRasters.Blue);
        var oracle = ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0);
        var similarity = RasterScoring.Compare(wrong, wrong, 0);
        var result = ComparisonPolicy.Evaluate(wrong, wrong, wrong, oracle);
        await Assert.That(similarity.IsAcceptable).IsTrue();
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Violated);
        await Assert.That(result.HyperPdfScore!.Value.ErrorPixels).IsEqualTo((long)ComparisonTestRasters.Edge * ComparisonTestRasters.Edge);
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.OwnStandardsExpectation);
    }

    /// <summary>Renderer agreement without an oracle requires review.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreviewedComparisonCannotImplyConformance()
    {
        var raster = ComparisonTestRasters.Solid(ComparisonTestRasters.Red);
        var result = ComparisonPolicy.Evaluate(raster, raster, raster, null);
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.ReviewRequired);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Unresolved);
        await Assert.That(result.HyperPdfScore).IsNull();
        await Assert.That(result.PdfiumScore).IsNull();
        await Assert.That(result.PdfJsScore).IsNull();
    }

    /// <summary>Equally correct renderers retain PDFium as the reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EquallyCorrectTieFavorsPdfium()
    {
        var correct = ComparisonTestRasters.Solid(ComparisonTestRasters.Red);
        var result = ComparisonPolicy.Evaluate(correct, correct, correct, ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0));
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.Pdfium);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Satisfied);
    }

    /// <summary>The pdf.js raster wins when it satisfies the oracle and PDFium does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IndependentlyCorrectPdfJsWinsOverMatchingWrongPdfium()
    {
        var wrong = ComparisonTestRasters.Solid(ComparisonTestRasters.Blue);
        var correct = ComparisonTestRasters.Solid(ComparisonTestRasters.Red);
        var result = ComparisonPolicy.Evaluate(wrong, wrong, correct, ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0));
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.PdfJs);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Violated);
        await Assert.That(result.PdfJsScore!.Value.IsAcceptable).IsTrue();
    }

    /// <summary>A documented standards-correct HyperPDF difference does not become a defect.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeitherReferenceCorrectChoosesOwnExpectation()
    {
        var correct = ComparisonTestRasters.Solid(ComparisonTestRasters.Red);
        var wrong = ComparisonTestRasters.Solid(ComparisonTestRasters.Blue);
        var blank = ComparisonTestRasters.Solid(ComparisonTestRasters.White);
        var result = ComparisonPolicy.Evaluate(correct, wrong, blank, ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0));
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.OwnStandardsExpectation);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Satisfied);
    }

    /// <summary>Both outputs within the rounding allowance are equally correct, so PDFium wins.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AllowedRoundingDifferencesStillFavorPdfium()
    {
        const byte almostWhite = byte.MaxValue - 1;
        var expected = ComparisonTestRasters.White;
        var correct = ComparisonTestRasters.Solid(expected);
        var approximate = ComparisonTestRasters.Solid(new(almostWhite, almostWhite, almostWhite, byte.MaxValue));
        var result = ComparisonPolicy.Evaluate(correct, approximate, correct, ComparisonTestRasters.Oracle(expected, 1));
        await Assert.That(result.PdfiumScore!.Value.IsAcceptable).IsTrue();
        await Assert.That(result.PdfiumScore!.Value.AllowedDifferencePixels).IsEqualTo((long)ComparisonTestRasters.Edge * ComparisonTestRasters.Edge);
        await Assert.That(result.PdfiumScore!.Value.ErrorPixels).IsEqualTo(0);
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.Pdfium);
    }

    /// <summary>Malformed recovery has a separate category and never implies standards conformance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedRecoveryRemainsUnresolved()
    {
        var result = ComparisonPolicy.Recovery();
        await Assert.That(result.Category).IsEqualTo(ComparisonCategory.MalformedRecovery);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Unresolved);
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.ReviewRequired);
    }

    /// <summary>Missing reference engines do not erase available independent checks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnavailableReferencesStillRequireOracle()
    {
        var correct = ComparisonTestRasters.Solid(ComparisonTestRasters.Red);
        var result = ComparisonPolicy.Evaluate(correct, null, null, ComparisonTestRasters.Oracle(ComparisonTestRasters.Red, 0));
        await Assert.That(result.Reference).IsEqualTo(ReferenceSelection.OwnStandardsExpectation);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Satisfied);
    }
}

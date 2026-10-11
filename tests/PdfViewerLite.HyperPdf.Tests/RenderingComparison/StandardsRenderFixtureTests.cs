// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Renders well-formed graphics PDFs with three engines and checks independent interior expectations.</summary>
[NotInParallel]
public sealed class StandardsRenderFixtureTests
{
    /// <summary>The bounded time for one browser reference, including one startup retry.</summary>
    private const int BrowserTestTimeoutMilliseconds = 180_000;

    /// <summary>Requires actual HyperPDF, PDFium and browser pdf.js output for each standards fixture.</summary>
    /// <param name="caseNumber">The graphics case.</param>
    /// <param name="cancellationToken">Cancels browser startup and rendering.</param>
    /// <returns>A task.</returns>
    [Test]
    [Timeout(BrowserTestTimeoutMilliseconds)]
    [Arguments((int)StandardsRenderCase.NonzeroFill)]
    [Arguments((int)StandardsRenderCase.EvenOddFill)]
    [Arguments((int)StandardsRenderCase.Clipping)]
    [Arguments((int)StandardsRenderCase.Transform)]
    [Arguments((int)StandardsRenderCase.NormalOpacity)]
    [Arguments((int)StandardsRenderCase.Multiply)]
    [Arguments((int)StandardsRenderCase.FormBoundingBox)]
    [Arguments((int)StandardsRenderCase.NonIsolatedMultiply)]
    public async Task AllEnginesAreIndependentlyChecked(int caseNumber, CancellationToken cancellationToken)
    {
        RequireBrowser();
        var fixture = StandardsRenderFixtures.Create((StandardsRenderCase)caseNumber);
        using var pair = new EnginePair(fixture.Pdf);
        await pair.HyperPdf.PreparePageAsync(0, cancellationToken);
        await pair.Pdfium.PreparePageAsync(0, cancellationToken);
        var hyper = ComparisonRendering.Render(pair.HyperPdf);
        var pdfium = ComparisonRendering.Render(pair.Pdfium);
        await using var browser = await PdfJsBrowserSession.CreateAsync(cancellationToken);
        var pdfJs = await browser.RenderAsync(fixture.Pdf, 0, 1, cancellationToken);
        var result = ComparisonPolicy.Evaluate(hyper, pdfium, pdfJs.Raster, fixture.Oracle);
        await ComparisonArtifacts.WriteAsync((StandardsRenderCase)caseNumber, fixture, hyper, pdfium, pdfJs, result, cancellationToken);
        await Assert.That(result.Category).IsEqualTo(ComparisonCategory.WellFormedStandards);
        await Assert.That(result.HyperPdfAssessment).IsEqualTo(OracleAssessment.Satisfied);
        await Assert.That(result.HyperPdfScore!.Value.DimensionsMatch).IsTrue();
        await Assert.That(result.HyperPdfScore!.Value.ErrorPixels).IsEqualTo(0);
        await Assert.That(result.HyperPdfScore!.Value.CheckedPixels).IsGreaterThan(0);
        await Assert.That(result.PdfiumScore.HasValue).IsTrue();
        await Assert.That(result.PdfJsScore.HasValue).IsTrue();
        await Assert.That(pdfJs.Metadata.PageIndex).IsEqualTo(0);
        await Assert.That(pdfJs.Metadata.Scale).IsEqualTo(1);
        await AssertSelection(result);
    }

    /// <summary>Classifies absent browser prerequisites explicitly, with an opt-in required mode for CI.</summary>
    private static void RequireBrowser()
    {
        try
        {
            _ = FirefoxBrowserDiscovery.FindExecutable();
        }
        catch (FileNotFoundException error) when (Environment.GetEnvironmentVariable("PVL_REQUIRE_PDFJS") != "1")
        {
            Skip.Test(error.Message);
        }
    }

    /// <summary>Requires the selected reference to follow independently scored acceptance.</summary>
    /// <param name="decision">The actual three-engine classification.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSelection(ComparisonDecision decision)
    {
        if (decision.PdfiumScore is { IsAcceptable: true })
        {
            await Assert.That(decision.Reference).IsEqualTo(ReferenceSelection.Pdfium);
        }
        else if (decision.PdfJsScore is { IsAcceptable: true })
        {
            await Assert.That(decision.Reference).IsEqualTo(ReferenceSelection.PdfJs);
        }
        else
        {
            await Assert.That(decision.Reference).IsEqualTo(ReferenceSelection.OwnStandardsExpectation);
        }
    }
}

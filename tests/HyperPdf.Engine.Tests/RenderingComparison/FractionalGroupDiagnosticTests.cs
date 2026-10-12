// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Preserves the fractional non-isolated case as an unresolved device-raster diagnostic.</summary>
[NotInParallel]
public sealed class FractionalGroupDiagnosticTests
{
    /// <summary>Requires all three engines while refusing to turn unspecified byte precision into conformance.</summary>
    /// <param name="cancellationToken">Cancels browser rendering and artifact writes.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FractionalDeviceRgbRemainsReviewRequired(CancellationToken cancellationToken)
    {
        try
        {
            _ = FirefoxBrowserDiscovery.FindExecutable();
        }
        catch (FileNotFoundException error) when (Environment.GetEnvironmentVariable("PVL_REQUIRE_PDFJS") != "1")
        {
            Skip.Test(error.Message);
        }

        var pdf = StandardsRenderFixtures.NonIsolatedGroup(true);
        using var input = new HyperPdfTestDocument(pdf);
        await input.Document.PreparePageAsync(0, cancellationToken);
        var hyper = ComparisonRendering.Render(input.Document);
        var pdfium = PdfiumReferenceRendering.RenderPage(pdf, 0, 1);
        await using var browser = await PdfJsBrowserSession.CreateAsync(cancellationToken);
        var pdfJs = await browser.RenderAsync(pdf, 0, 1, cancellationToken);
        var decision = ComparisonPolicy.Evaluate(hyper, pdfium, pdfJs.Raster, null);
        await FractionalGroupArtifacts.WriteAsync(pdf, hyper, pdfium, pdfJs, decision, cancellationToken);
        await Assert.That(decision.Category).IsEqualTo(ComparisonCategory.Unreviewed);
        await Assert.That(decision.Reference).IsEqualTo(ReferenceSelection.ReviewRequired);
        await Assert.That(decision.HyperPdfAssessment).IsEqualTo(OracleAssessment.Unresolved);
        await Assert.That(decision.HyperPdfScore.HasValue).IsFalse();
        await Assert.That(decision.PdfiumScore.HasValue).IsFalse();
        await Assert.That(decision.PdfJsScore.HasValue).IsFalse();
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Raster;

/// <summary>
/// Checks the raster report on the Library of Congress scans of Supreme Court opinions, which carry JBIG2 images and no
/// PDF/R marker. The files come from <c>tests/reference-pdfs/manifest.json</c>; a missing file skips the test, or fails
/// it when <c>HYPERPDF_REQUIRE_REFERENCE_PDFS</c> is <c>1</c>.
/// </summary>
public sealed class ScannedOpinionRasterReportTests
{
    /// <summary>The filter that every page image uses.</summary>
    private const string Jbig2Filter = "JBIG2Decode";

    /// <summary>Verifies that each scanned opinion reports as raster pages with JBIG2 filters and no PDF/R claim.</summary>
    /// <param name="id">The manifest id of the opinion.</param>
    /// <param name="pageCount">The pages the opinion has.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("loc-marbury-v-madison", 44)]
    [Arguments("loc-plessy-v-ferguson", 28)]
    [Arguments("loc-brown-v-board", 14)]
    [Arguments("loc-korematsu-v-united-states", 35)]
    [Arguments("loc-gideon-v-wainwright", 18)]
    public async Task ScannedOpinion_ReportsRasterPagesWithJbig2AndNoClaim(string id, int pageCount)
    {
        using var document = PdfDocumentReader.Open(ReferencePdfFiles.RequirePaths(id)[0], null);

        var report = PdfDocumentRaster.GetRasterReport(document, CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(report.Claim).IsNull();
            await Assert.That(report.Pages.Count).IsEqualTo(pageCount);
            await Assert.That(report.IsRasterOnly).IsTrue().Because(string.Join(',', report.GetNonRasterPages()));
            await Assert.That(report.UsesAllowedFilters).IsTrue();
            await Assert.That(report.FiltersSeen).Contains(Jbig2Filter);
        }

        foreach (var page in report.Pages)
        {
            await Assert.That(page.Images.Count).IsGreaterThan(0).Because($"page {page.PageIndex}");
            await Assert.That(page.Images[0].Filters).Contains(Jbig2Filter).Because($"page {page.PageIndex}");
        }
    }
}

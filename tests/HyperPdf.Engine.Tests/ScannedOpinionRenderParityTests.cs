// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Renders pages of the scanned JBIG2 Supreme Court opinions with PDFium and HyperPDF and compares the pixels. The files
/// come from <c>tests/reference-pdfs/manifest.json</c>; a missing file skips the test, or fails it when
/// <c>HYPERPDF_REQUIRE_REFERENCE_PDFS</c> is <c>1</c>.
/// </summary>
public sealed class ScannedOpinionRenderParityTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The divisor that finds the middle page.</summary>
    private const int Halves = 2;

    /// <summary>
    /// The largest mean absolute difference per channel, from 0 to 255, accepted for a bilevel scan (measured up to 8.4 at one
    /// pixel per point). The page is a 1-bit image of about 2,500 pixels across drawn at about 600, and the engines average the
    /// source pixels under each output pixel with different filters, so the grey levels along text edges differ.
    /// </summary>
    private const double ScanThreshold = 10;

    /// <summary>Verifies that the first, middle and last page of each scanned opinion render like PDFium.</summary>
    /// <param name="id">The manifest id of the opinion.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("loc-marbury-v-madison")]
    [Arguments("loc-plessy-v-ferguson")]
    [Arguments("loc-brown-v-board")]
    [Arguments("loc-korematsu-v-united-states")]
    [Arguments("loc-gideon-v-wainwright")]
    public async Task ScannedOpinion_RendersLikePdfium(string id)
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(id));
        var last = pair.Pdfium.PageCount - 1;
        int[] pages = [0, last / Halves, last];

        foreach (var page in pages)
        {
            var info = new PageRenderInfo(page, 1, PageRotation.None, 0, 0, RenderFlags.None);
            var pdfium = PdfiumReferenceRendering.Render(pair.Bytes, info);
            var actual = Render(pair.HyperPdf, info, pdfium.Width, pdfium.Height);
            var difference = MeanDifference(pdfium.Pixels.ToArray(), actual);

            TestContext.Current?.Output.WriteLine($"{id} page {page}: mean difference {difference:F4}");
            await Assert.That(difference).IsLessThan(ScanThreshold).Because($"{id} page {page}");
        }
    }

    /// <summary>Renders a page with HyperPDF.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The render request.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(IDocument document, in PageRenderInfo info, int width, int height)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        return document.Render(info, new(pixels, width, height, width * BytesPerPixel)) ? pixels : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Computes the mean absolute difference per channel between two BGRA images.</summary>
    /// <param name="expected">The reference pixels.</param>
    /// <param name="actual">The pixels under test.</param>
    /// <returns>The mean difference, from 0 to 255.</returns>
    private static double MeanDifference(byte[] expected, byte[] actual)
    {
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}

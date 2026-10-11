// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Compares downloaded reference PDFs with HyperPDF, native PDFium and browser pdf.js.</summary>
[NotInParallel]
public sealed class ReferencePdfRenderingTests
{
    /// <summary>The first pages compared in each cached document.</summary>
    private const int PagesPerDocument = 2;

    /// <summary>The pixels rendered per PDF point.</summary>
    private const float Scale = 0.5F;

    /// <summary>Renders each selected page with all three engines without a similarity conformance gate.</summary>
    /// <param name="cancellationToken">Cancels browser rendering and evidence writes.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DirectoryNotFoundException">The required reference PDFs are unavailable.</exception>
    [Test]
    public async Task DownloadedReferencePagesAreMarkedUnreviewed(CancellationToken cancellationToken)
    {
        RequireBrowser();
        var folder = Environment.GetEnvironmentVariable("PDFVIEWERLITE_REFERENCE_PDF_DIR") is { Length: > 0 } configured
            ? configured
            : throw new DirectoryNotFoundException("Set PDFVIEWERLITE_REFERENCE_PDF_DIR for the rendering comparison job.");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf").Order(StringComparer.Ordinal).ToArray() : [];
        if (files.Length == 0)
        {
            throw new DirectoryNotFoundException($"No downloaded reference PDFs in {folder}.");
        }

        await using var browser = await PdfJsBrowserSession.CreateAsync(cancellationToken);
        var pages = 0;
        foreach (var file in files)
        {
            pages += await CompareFileAsync(file, browser, cancellationToken);
        }

        TestContext.Current?.Output.WriteLine($"Reference PDFs: {files.Length} files, {pages} pages; independent conformance review remains unresolved.");
        await Assert.That(pages).IsGreaterThan(0);
    }

    /// <summary>Requires the browser used for the reference rendering comparison.</summary>
    internal static void RequireBrowser() => _ = FirefoxBrowserDiscovery.FindExecutable();

    /// <summary>Renders the first cached pages through all three engines.</summary>
    /// <param name="file">The cached PDF path.</param>
    /// <param name="browser">The reusable actual browser renderer.</param>
    /// <param name="cancellationToken">Cancels rendering and evidence writes.</param>
    /// <returns>The actual page count compared.</returns>
    internal static async Task<int> CompareFileAsync(string file, PdfJsBrowserSession browser, CancellationToken cancellationToken)
    {
        var pdf = await File.ReadAllBytesAsync(file, cancellationToken);
        using var input = new HyperPdfTestDocument(pdf);
        var pdfiumPageCount = PdfiumReferenceRendering.PageCount(pdf);
        var pages = Math.Min(PagesPerDocument, pdfiumPageCount);
        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            await input.Document.PreparePageAsync(pageIndex, cancellationToken);
            var hyperPdf = Render(input.Document, pageIndex);
            var pdfium = PdfiumReferenceRendering.RenderPage(pdf, pageIndex, Scale);
            var pdfJs = await browser.RenderAsync(pdf, pageIndex, Scale, cancellationToken);
            var classification = ComparisonPolicy.Evaluate(hyperPdf, pdfium, pdfJs.Raster, null);
            var artifact = new ReferencePdfComparisonArtifact(
                Path.GetFileName(file),
                Convert.ToHexString(SHA256.HashData(pdf)),
                "Unvalidated: cached source has no independent well-formedness, PDF/A or other conformance certificate in this suite.",
                RuntimeInformation.FrameworkDescription,
                pageIndex,
                Scale,
                typeof(HyperPdfEngine).Assembly.FullName!,
                "PDFium native bitmap renderer",
                input.Document.PageCount,
                pdfiumPageCount,
                new(hyperPdf.Width, hyperPdf.Height),
                new(pdfium.Width, pdfium.Height),
                new(pdfJs.Raster.Width, pdfJs.Raster.Height),
                classification,
                ReferencePdfComparisonArtifacts.Compare(hyperPdf, pdfium),
                ReferencePdfComparisonArtifacts.Compare(hyperPdf, pdfJs.Raster),
                ReferencePdfComparisonArtifacts.Compare(pdfium, pdfJs.Raster),
                pdfJs.Metadata);
            var directory = await ReferencePdfComparisonArtifacts.WriteAsync(artifact, pdf, hyperPdf, pdfium, pdfJs.Raster, cancellationToken);
            TestContext.Current?.Output.WriteLine($"{artifact.PdfFile} page {pageIndex + 1}: Unreviewed/ReviewRequired; evidence {directory}");
            await Assert.That(classification.Category).IsEqualTo(ComparisonCategory.Unreviewed);
            await Assert.That(classification.Reference).IsEqualTo(ReferenceSelection.ReviewRequired);
            await Assert.That(classification.HyperPdfAssessment).IsEqualTo(OracleAssessment.Unresolved);
            await Assert.That(pdfJs.Metadata.PageIndex).IsEqualTo(pageIndex);
        }

        return pages;
    }

    /// <summary>Uses each engine's own page geometry and standard default rendering flags.</summary>
    /// <param name="document">The opened renderer document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The actual white-background BGRA32 raster.</returns>
    /// <exception cref="InvalidOperationException">The engine does not render the requested page.</exception>
    private static ComparisonRaster Render(IDocument document, int pageIndex)
    {
        var size = document.GetPageSizes()[pageIndex];
        TileGrid.GetPagePixelSize(size, PageRotation.None, Scale, out var width, out var height);
        var pixels = new byte[checked(width * height * ComparisonRaster.BytesPerPixel)];
        Array.Fill(pixels, byte.MaxValue);
        if (!document.Render(new(pageIndex, Scale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * ComparisonRaster.BytesPerPixel)))
        {
            throw new InvalidOperationException("HyperPDF did not render the reference page.");
        }

        return new(width, height, pixels);
    }
}

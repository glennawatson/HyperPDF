// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Pdfium;

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Collects three-engine cached corpus evidence while leaving standards review unresolved.</summary>
[NotInParallel]
public sealed class CorpusRenderingComparisonTests
{
    /// <summary>The first pages compared in each cached document.</summary>
    private const int PagesPerDocument = 2;

    /// <summary>The pixels rendered per PDF point.</summary>
    private const float Scale = 0.5F;

    /// <summary>Renders each selected page with all three engines without a similarity conformance gate.</summary>
    /// <param name="cancellationToken">Cancels browser rendering and evidence writes.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DirectoryNotFoundException">The required corpus is unavailable.</exception>
    [Test]
    public async Task CachedPagesRemainExplicitlyUnreviewed(CancellationToken cancellationToken)
    {
        RequireBrowser();
        var folder = Environment.GetEnvironmentVariable("PDFVIEWERLITE_CORPUS_DIR") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf").Order(StringComparer.Ordinal).ToArray() : [];
        if (files.Length == 0)
        {
            if (Environment.GetEnvironmentVariable("PVL_REQUIRE_CORPUS") == "1")
            {
                throw new DirectoryNotFoundException($"No cached comparison corpus PDFs in {folder}.");
            }

            Skip.Test($"No cached comparison corpus PDFs in {folder}.");
        }

        await using var browser = await PdfJsBrowserSession.CreateAsync(cancellationToken);
        var pages = 0;
        foreach (var file in files)
        {
            pages += await CompareFileAsync(file, browser, cancellationToken);
        }

        TestContext.Current?.Output.WriteLine($"Corpus: {files.Length} documents, {pages} pages; all Unreviewed, ReviewRequired and Unresolved; independent PDF conformance validation unavailable.");
        await Assert.That(pages).IsGreaterThan(0);
    }

    /// <summary>Records missing browser prerequisites explicitly and requires them in CI.</summary>
    internal static void RequireBrowser()
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

    /// <summary>Renders the first cached pages through all three engines.</summary>
    /// <param name="file">The cached PDF path.</param>
    /// <param name="browser">The reusable actual browser renderer.</param>
    /// <param name="cancellationToken">Cancels rendering and evidence writes.</param>
    /// <returns>The actual page count compared.</returns>
    internal static async Task<int> CompareFileAsync(string file, PdfJsBrowserSession browser, CancellationToken cancellationToken)
    {
        var pdf = await File.ReadAllBytesAsync(file, cancellationToken);
        using var pair = new EnginePair(pdf);
        var pages = Math.Min(PagesPerDocument, pair.Pdfium.PageCount);
        for (var pageIndex = 0; pageIndex < pages; pageIndex++)
        {
            await pair.HyperPdf.PreparePageAsync(pageIndex, cancellationToken);
            await pair.Pdfium.PreparePageAsync(pageIndex, cancellationToken);
            var hyperPdf = Render(pair.HyperPdf, pageIndex);
            var pdfium = Render(pair.Pdfium, pageIndex);
            var pdfJs = await browser.RenderAsync(pdf, pageIndex, Scale, cancellationToken);
            var classification = ComparisonPolicy.Evaluate(hyperPdf, pdfium, pdfJs.Raster, null);
            var artifact = new CorpusComparisonArtifact(
                Path.GetFileName(file),
                Convert.ToHexString(SHA256.HashData(pdf)),
                "Unvalidated: cached source has no independent well-formedness, PDF/A or other conformance certificate in this suite.",
                RuntimeInformation.FrameworkDescription,
                pageIndex,
                Scale,
                typeof(HyperPdfEngine).Assembly.FullName!,
                typeof(PdfiumEngine).Assembly.FullName!,
                pair.HyperPdf.PageCount,
                pair.Pdfium.PageCount,
                new(hyperPdf.Width, hyperPdf.Height),
                new(pdfium.Width, pdfium.Height),
                new(pdfJs.Raster.Width, pdfJs.Raster.Height),
                classification,
                CorpusComparisonArtifacts.Compare(hyperPdf, pdfium),
                CorpusComparisonArtifacts.Compare(hyperPdf, pdfJs.Raster),
                CorpusComparisonArtifacts.Compare(pdfium, pdfJs.Raster),
                pdfJs.Metadata);
            var directory = await CorpusComparisonArtifacts.WriteAsync(artifact, pdf, hyperPdf, pdfium, pdfJs.Raster, cancellationToken);
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
            throw new InvalidOperationException("The corpus comparison renderer did not render the page.");
        }

        return new(width, height, pixels);
    }
}

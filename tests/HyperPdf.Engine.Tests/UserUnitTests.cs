// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Printing;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests for <c>/UserUnit</c> (ISO 32000-2 7.7.3.3): physical size, rendering scale, printing and the PDFium-compatible mode.</summary>
public sealed class UserUnitTests
{
    /// <summary>The page edge in user space units.</summary>
    private const int PageEdge = 200;

    /// <summary>The user unit under test.</summary>
    private const int Unit = 2;

    /// <summary>The page entry that doubles the user unit.</summary>
    private const string DoubleUnit = "/UserUnit 2";

    /// <summary>The width of A4 paper in points.</summary>
    private const float PaperWidth = 595;

    /// <summary>The height of A4 paper in points.</summary>
    private const float PaperHeight = 842;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The largest mean absolute difference per channel accepted between engines.</summary>
    private const double Threshold = 0.5;

    /// <summary>A page that fills with red and draws a blue square.</summary>
    private const string Content = "1 0 0 rg 0 0 200 200 re f 0 0 1 rg 50 50 100 100 re f";

    /// <summary>A page reports its size in user space units and twice that physically.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PhysicalSizeIsUserSizeTimesUserUnit()
    {
        using var pair = new EnginePair(Create(DoubleUnit));
        var page = PdfDocumentPages.GetPage(pair.Library, 0);

        await Assert.That(page.UserUnit).IsEqualTo((float)Unit);
        await Assert.That(page.Width).IsEqualTo((float)PageEdge);
        await Assert.That(page.PhysicalWidth).IsEqualTo((float)(PageEdge * Unit));
        await Assert.That(pair.HyperPdf.GetPageSizes()[0].Width).IsEqualTo((float)(PageEdge * Unit));
        await Assert.That(pair.HyperPdf.GetPageSizes()[0].Height).IsEqualTo((float)(PageEdge * Unit));
    }

    /// <summary>A missing, zero, negative or non-numeric user unit reads as 1.</summary>
    /// <param name="entry">The page entry.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("/UserUnit 0")]
    [Arguments("/UserUnit -3")]
    [Arguments("/UserUnit /Two")]
    public async Task UnusableUserUnitReadsAsOne(string entry)
    {
        using var pair = new EnginePair(Create(entry));

        await Assert.That(PdfDocumentPages.GetPage(pair.Library, 0).UserUnit).IsEqualTo(1F);
    }

    /// <summary>A scale of 1 draws the page at its physical size (ISO 32000-2 7.7.3.3); the reference renderer reaches the same size with twice the scale.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliedUserUnitRendersAtPhysicalScale()
    {
        var bytes = Create(DoubleUnit);
        using var pair = new EnginePair(bytes);
        var reference = PdfiumReferenceRendering.Render(bytes, new(0, Unit, PageRotation.None, 0, 0, RenderFlags.None));
        var expected = reference.Pixels.ToArray();
        var actual = Render(pair.HyperPdf, new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), reference.Width, reference.Height);

        await Assert.That(reference.Width).IsEqualTo(PageEdge * Unit);
        await Assert.That(MeanDifference(expected, actual)).IsLessThan(Threshold);
    }

    /// <summary>Printing at actual size scales the page's user space by the user unit, so it prints at its physical size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ActualSizePrintScalesByUserUnit()
    {
        using var pair = new EnginePair(Create(DoubleUnit));
        var page = PdfDocumentPages.GetPage(pair.Library, 0);
        var layout = SheetLayout.Default with { FitToPaper = true, Scaling = PrintScaling.ActualSize };
        var placement = HyperPdfPageExport.GetFitPlacement(default, page, new(PaperWidth, PaperHeight), layout, out _);

        await Assert.That(placement.Transform.M11).IsEqualTo((float)Unit);
    }

    /// <summary>A poster tile keeps the page's physical size by carrying the user unit to the sheet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PosterSheetKeepsUserUnit()
    {
        var source = Path.Combine(Path.GetTempPath(), $"hyperpdf-userunit-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(source, Create(DoubleUnit));
        try
        {
            var layout = new SheetLayout(1, PaperSize.A4, false) { Imposition = PrintImposition.Poster, PosterTiles = 1 };
            using var run = ExportRun.Create(source, [0], layout);
            var sizes = run.Output!.GetPageSizes();

            await Assert.That(run.Written).IsTrue();
            await Assert.That(sizes[0].Width).IsEqualTo((float)(PageEdge * Unit));
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Builds a one page document with a user unit entry.</summary>
    /// <param name="entry">The page entry, such as <c>/UserUnit 2</c>.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create(string entry) => new RenderTestPdf(PageEdge, PageEdge) { Content = Content, PageEntries = entry }.ToBytes();

    /// <summary>Renders a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The request.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(HyperPdfDocument document, in PageRenderInfo info, int width, int height)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        return document.Render(info, new(pixels, width, height, width * BytesPerPixel)) ? pixels : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Computes the mean absolute difference per channel between two images.</summary>
    /// <param name="expected">The reference pixels.</param>
    /// <param name="actual">The pixels under test.</param>
    /// <returns>The mean difference.</returns>
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

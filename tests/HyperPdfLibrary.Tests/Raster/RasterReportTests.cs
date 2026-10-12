// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Raster;

/// <summary>Checks the raster-only report on hand-built pages.</summary>
public sealed class RasterReportTests
{
    /// <summary>The tolerance for a resolution computed from floats.</summary>
    private const float DpiTolerance = 0.01F;

    /// <summary>One CCITT image and the marker: raster-only, 100 dpi, claimed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleCcittImageWithMarkerIsRasterOnly()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, MiniPdf.Stream(RasterPdfs.CcittEntries, "data"), true));
        var page = report.Pages[0];
        var image = page.Images[0];

        await Assert.That(report.Claim).IsEqualTo("PDF-raster-1.0");
        await Assert.That(report.IsRasterOnly).IsTrue();
        await Assert.That(report.UsesAllowedFilters).IsTrue();
        await Assert.That(report.FiltersSeen).IsEquivalentTo(["CCITTFaxDecode"]);
        await Assert.That(page.Images.Count).IsEqualTo(1);
        await Assert.That(image.Width).IsEqualTo(RasterPdfs.ImageWidth);
        await Assert.That(image.Height).IsEqualTo(RasterPdfs.ImageHeight);
        await Assert.That(image.ColorSpace).IsEqualTo("DeviceGray");
        await Assert.That(image.BitsPerComponent).IsEqualTo(1);
        await Assert.That(image.HorizontalDpi).IsEqualTo(RasterPdfs.ExpectedDpi).Within(DpiTolerance);
        await Assert.That(image.VerticalDpi).IsEqualTo(RasterPdfs.ExpectedDpi).Within(DpiTolerance);
    }

    /// <summary>Without the marker there is no claim, but the structure still reads as raster-only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoMarkerMeansNoClaim()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, RasterPdfs.CcittEntries));

        await Assert.That(report.Claim).IsNull();
        await Assert.That(report.IsRasterOnly).IsTrue();
    }

    /// <summary>A filled path makes the page vector content and not raster-only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VectorPathBreaksTheProfile()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page($"{RasterPdfs.PaintImage}0 0 100 100 re f\n", RasterPdfs.CcittEntries));
        var page = report.Pages[0];

        await Assert.That(page.HasVectorContent).IsTrue();
        await Assert.That(page.IsRasterOnly).IsFalse();
        await Assert.That(report.GetNonRasterPages()).IsEquivalentTo([0]);
    }

    /// <summary>A clip path that paints nothing does not break the profile.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipWithoutPaintingStaysRasterOnly()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page("q 0 0 432 576 re W n 432 0 0 576 0 0 cm /Im0 Do Q\n", RasterPdfs.CcittEntries));

        await Assert.That(report.Pages[0].IsRasterOnly).IsTrue();
    }

    /// <summary>Invisible text over the image is an OCR layer and keeps the page raster-only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvisibleTextIsAnOcrLayer()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page($"{RasterPdfs.PaintImage}BT /F1 12 Tf 3 Tr 72 500 Td (Hello scan) Tj ET\n", RasterPdfs.CcittEntries));
        var page = report.Pages[0];

        await Assert.That(page.HasOcrText).IsTrue();
        await Assert.That(page.HasVisibleText).IsFalse();
        await Assert.That(page.IsRasterOnly).IsTrue();
        await Assert.That(page.OcrCharCount).IsGreaterThan(0);
    }

    /// <summary>Visible text is not allowed on a raster page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VisibleTextBreaksTheProfile()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page($"{RasterPdfs.PaintImage}BT /F1 12 Tf 72 500 Td (Hello) Tj ET\n", RasterPdfs.CcittEntries));
        var page = report.Pages[0];

        await Assert.That(page.HasVisibleText).IsTrue();
        await Assert.That(page.HasOcrText).IsFalse();
        await Assert.That(page.IsRasterOnly).IsFalse();
    }

    /// <summary>The render mode is part of the saved state, so <c>Q</c> brings back visible text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderModeIsRestoredByQ()
    {
        const string content = $"{RasterPdfs.PaintImage}BT /F1 12 Tf 72 500 Td q 3 Tr (a) Tj Q (b) Tj ET\n";
        var page = RasterPdfs.Report(RasterPdfs.Page(content, RasterPdfs.CcittEntries)).Pages[0];

        await Assert.That(page.HasOcrText).IsTrue();
        await Assert.That(page.HasVisibleText).IsTrue();
    }

    /// <summary>A page that paints no image is not raster-only.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageWithoutImageIsNotRasterOnly()
    {
        var report = RasterPdfs.Report(RasterPdfs.Page(string.Empty, RasterPdfs.CcittEntries));

        await Assert.That(report.Pages[0].Images.Count).IsEqualTo(0);
        await Assert.That(report.IsRasterOnly).IsFalse();
    }

    /// <summary>Flate and DCT images pass the filter check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlateAndDctPassTheFilterCheck()
    {
        const string flate = "/Type /XObject /Subtype /Image /Width 600 /Height 800 /BitsPerComponent 8 /ColorSpace /DeviceRGB /Filter /FlateDecode";
        const string dct = "/Type /XObject /Subtype /Image /Width 600 /Height 800 /BitsPerComponent 8 /ColorSpace [/ICCBased 7 0 R] /Filter [/DCTDecode]";

        var flateReport = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, flate));
        var dctReport = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, dct));

        await Assert.That(flateReport.UsesAllowedFilters).IsTrue();
        await Assert.That(flateReport.Pages[0].Images[0].ColorSpace).IsEqualTo("DeviceRGB");
        await Assert.That(dctReport.UsesAllowedFilters).IsTrue();
        await Assert.That(dctReport.Pages[0].Images[0].ColorSpace).IsEqualTo("ICCBased");
    }

    /// <summary>An LZW image, or any chain that includes a filter outside the list, fails the filter check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LzwFailsTheFilterCheck()
    {
        const string lzw = "/Type /XObject /Subtype /Image /Width 600 /Height 800 /BitsPerComponent 8 /ColorSpace /DeviceGray /Filter /LZWDecode";
        const string chain = "/Type /XObject /Subtype /Image /Width 600 /Height 800 /BitsPerComponent 8 /ColorSpace /DeviceGray /Filter [/ASCII85Decode /DCTDecode]";

        var lzwReport = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, lzw));
        var chainReport = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, chain));

        await Assert.That(lzwReport.UsesAllowedFilters).IsFalse();
        await Assert.That(lzwReport.FiltersSeen).IsEquivalentTo(["LZWDecode"]);
        await Assert.That(lzwReport.Pages[0].IsRasterOnly).IsTrue();
        await Assert.That(chainReport.UsesAllowedFilters).IsFalse();
        await Assert.That(chainReport.FiltersSeen).IsEquivalentTo(["ASCII85Decode", "DCTDecode"]);
    }

    /// <summary>An inline image is listed, with abbreviations expanded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImageIsListed()
    {
        const string content = "q 432 0 0 576 0 0 cm BI /W 600 /H 800 /BPC 1 /CS /G /F /Fl ID abc EI Q\n";
        var image = RasterPdfs.Report(RasterPdfs.Page(content, RasterPdfs.CcittEntries)).Pages[0].Images[0];

        await Assert.That(image.IsInline).IsTrue();
        await Assert.That(image.Width).IsEqualTo(RasterPdfs.ImageWidth);
        await Assert.That(image.ColorSpace).IsEqualTo("DeviceGray");
        await Assert.That(image.Filters).IsEquivalentTo(["FlateDecode"]);
        await Assert.That(image.HorizontalDpi).IsEqualTo(RasterPdfs.ExpectedDpi).Within(DpiTolerance);
    }

    /// <summary>A stencil mask reports no colour space and its one-bit depth.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilMaskIsReported()
    {
        const string mask = "/Type /XObject /Subtype /Image /Width 600 /Height 800 /ImageMask true /Filter /CCITTFaxDecode";
        var image = RasterPdfs.Report(RasterPdfs.Page(RasterPdfs.PaintImage, mask)).Pages[0].Images[0];

        await Assert.That(image.ColorSpace).IsEqualTo("ImageMask");
        await Assert.That(image.BitsPerComponent).IsEqualTo(1);
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Tests.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.HyperPdf.Tests.RenderingComparison;
using PdfViewerLite.TestAssets;
using SkiaSharp;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Renders the same pages with PDFium and HyperPDF and compares the pixels.</summary>
public sealed class RenderParityTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The largest mean absolute difference per channel, from 0 to 255, accepted for pages drawn only with paths and images.</summary>
    private const double ShapeThreshold = 0.5;

    /// <summary>The largest mean absolute difference accepted for an upscaled image, where the engines smooth differently.</summary>
    private const double ImageThreshold = 2.5;

    /// <summary>The largest mean absolute difference accepted for a page with text, which HyperPDF does not draw until fonts are native.</summary>
    private const double TextThreshold = 1.5;

    /// <summary>The size of the test shapes page in points.</summary>
    private const int ShapesSize = 200;

    /// <summary>The width and height of the scan image.</summary>
    private const int ScanSize = 64;

    /// <summary>The tile edge used for tile parity.</summary>
    private const int TileEdge = 256;

    /// <summary>The tile offset used for tile parity.</summary>
    private const int TileOffset = 100;

    /// <summary>The scale used for scaled parity.</summary>
    private const float Scale = 1.5F;

    /// <summary>The content of the shapes page: fills, strokes, curves, a clip, transparency and a dash.</summary>
    private const string ShapesContent =
        "1 0 0 rg 20 20 80 80 re f 0 0 1 RG 8 w 10 190 m 190 10 l S 0 0.6 0 rg 100 100 m 150 100 190 150 150 190 c 100 190 l f "
        + "q 120 20 60 60 re W n 1 0.5 0 rg 100 0 100 100 re f Q /GS gs 0.2 0.2 0.8 rg 60 60 100 100 re f [6 4] 0 d 2 w 0 G 5 5 190 190 re S";

    /// <summary>The resources of the shapes page.</summary>
    private const string ShapesResources = "/ExtGState << /GS << /Type /ExtGState /ca 0.5 >> >>";

    /// <summary>The quality given to the PNG encoder; PNG ignores it.</summary>
    private const int PngQuality = 100;

    /// <summary>The page index of the only page.</summary>
    private const int FirstPage = 0;

    /// <summary>The largest mean difference accepted for transparency groups, soft masks and knockout (measured 0.13 to 0.45).</summary>
    private const double TransparencyThreshold = 1;

    /// <summary>
    /// The largest mean difference accepted for a non-isolated group with a Multiply object (measured 11.79). PDFium blends
    /// the group against a transparent backdrop, as if it were isolated; HyperPDF follows ISO 32000-2 §11.4.8 and blends
    /// it with the page beneath, so the group's square differs by design.
    /// </summary>
    private const double NonIsolatedThreshold = 12.5;

    /// <summary>The largest mean difference accepted for gradients, where the engines sample colours differently (measured 0.94).</summary>
    private const double ShadingThreshold = 1.5;

    /// <summary>The largest mean difference accepted for tiling patterns (measured 0).</summary>
    private const double PatternThreshold = 0.5;

    /// <summary>The largest mean difference accepted for dots and hairlines, whose anti-aliasing differs (measured 0.15).</summary>
    private const double LineThreshold = 0.5;

    /// <summary>The largest mean difference accepted for generated annotation appearances (measured 0.01 to 0.23).</summary>
    private const double AnnotationThreshold = 0.5;

    /// <summary>The half alpha graphics state.</summary>
    private const string HalfState = "/Half << /Type /ExtGState /ca 0.5 /CA 0.5 >>";

    /// <summary>A soft mask transfer function that inverts coverage.</summary>
    private const string InvertTransfer = "/TR << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >>";

    /// <summary>The red-to-blue axial function.</summary>
    private const string RedToBlue = "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>";

    /// <summary>A page of paths, clips, dashes and transparency matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task ShapesMatchPdfium() =>
        AssertParity(nameof(ShapesMatchPdfium), CreateShapes(), new(FirstPage, 1, PageRotation.None, 0, 0, RenderFlags.None), ShapeThreshold);

    /// <summary>A page of paths at a fractional scale matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task ScaledShapesMatchPdfium() =>
        AssertParity(nameof(ScaledShapesMatchPdfium), CreateShapes(), new(FirstPage, Scale, PageRotation.None, 0, 0, RenderFlags.None), ShapeThreshold);

    /// <summary>A page rotated by the viewer matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task RotatedShapesMatchPdfium() =>
        AssertParity(nameof(RotatedShapesMatchPdfium), CreateShapes(), new(FirstPage, 1, PageRotation.Rotate90, 0, 0, RenderFlags.None), ShapeThreshold);

    /// <summary>An image-only page, like a scan, matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScanMatchesPdfium()
    {
        var grey = new byte[ScanSize * ScanSize];
        for (var i = 0; i < grey.Length; i++)
        {
            grey[i] = (byte)(i % byte.MaxValue);
        }

        await AssertParity(nameof(ScanMatchesPdfium), TestPdf.CreateScan(grey, ScanSize, ScanSize), new(FirstPage, 1, PageRotation.None, 0, 0, RenderFlags.None), ImageThreshold);
    }

    /// <summary>A layered page with a hidden layer matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task LayersMatchPdfium() =>
        AssertParity(nameof(LayersMatchPdfium), TestPdf.CreateWithLayers(), new(FirstPage, 1, PageRotation.None, 0, 0, RenderFlags.None), ShapeThreshold);

    /// <summary>A tile cut from the middle of a page matches PDFium's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task TileOffsetMatchesPdfium() =>
        AssertParity(nameof(TileOffsetMatchesPdfium), CreateShapes(), new(FirstPage, Scale, PageRotation.None, TileOffset, TileOffset, RenderFlags.None), ShapeThreshold, TileEdge);

    /// <summary>A page with text differs from PDFium only by the text, which HyperPDF does not draw yet.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task TextPageIsCloseToPdfium() =>
        AssertParity(nameof(TextPageIsCloseToPdfium), TestPdf.Create(1), new(FirstPage, 1, PageRotation.None, 0, 0, RenderFlags.None), TextThreshold);

    /// <summary>A translucent stroke over its fill knocks the fill out, as in PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task StrokeKnockoutMatchesPdfium() =>
        AssertParity(
            nameof(StrokeKnockoutMatchesPdfium),
            Page("1 0 0 rg 0 0 1 RG /Half gs 20 w 50 50 100 100 re B", $"/ExtGState << {HalfState} >>"),
            PageInfo(RenderFlags.None),
            TransparencyThreshold);

    /// <summary>A non-isolated group with a Multiply object over a coloured page differs from PDFium only where the specification requires.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task NonIsolatedGroupIsCloseToPdfium() =>
        AssertParity(nameof(NonIsolatedGroupIsCloseToPdfium), GroupPage("/I false"), PageInfo(RenderFlags.None), NonIsolatedThreshold);

    /// <summary>An isolated group matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task IsolatedGroupMatchesPdfium() =>
        AssertParity(nameof(IsolatedGroupMatchesPdfium), GroupPage("/I true"), PageInfo(RenderFlags.None), TransparencyThreshold);

    /// <summary>A luminosity soft mask with a backdrop colour and a transfer function matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task SoftMaskBackdropAndTransferMatchPdfium()
    {
        var pdf = new RenderTestPdf(ShapesSize, ShapesSize) { Content = "/GS gs 1 0 0 rg 0 0 200 200 re f" };
        var group = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Group << /S /Transparency /CS /DeviceGray >>", "0.25 g 0 0 100 200 re f 1 g 100 0 50 200 re f");
        var state = pdf.AddObject($"<< /Type /ExtGState /SMask << /Type /Mask /S /Luminosity /G {group} 0 R /BC [0.5] {InvertTransfer} >> >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        return AssertParity(nameof(SoftMaskBackdropAndTransferMatchPdfium), pdf.ToBytes(), PageInfo(RenderFlags.None), TransparencyThreshold);
    }

    /// <summary>A DeviceCMYK image drawn with overprint covers the page, as the shipped PDFium draws it, while simulation is off by default.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task CmykImageOverprintMatchesPdfium()
    {
        var pdf = new RenderTestPdf(ShapesSize, ShapesSize) { Content = "1 1 0 rg 0 0 200 200 re f /OP gs q 100 0 0 100 50 50 cm /Im Do Q" };
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 2 /Height 1 /ColorSpace /DeviceCMYK /BitsPerComponent 8 /Filter /ASCIIHexDecode", "FF000000 00FF0000>");
        pdf.Resources = $"/XObject << /Im {image} 0 R >> /ExtGState << /OP << /Type /ExtGState /OP true /op true /OPM 0 >> >>";
        return AssertParity(nameof(CmykImageOverprintMatchesPdfium), pdf.ToBytes(), PageInfo(RenderFlags.None), ImageThreshold);
    }

    /// <summary>Axial shadings extended at one end only match PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task MixedExtendMatchesPdfium() =>
        AssertParity(
            nameof(MixedExtendMatchesPdfium),
            Page(
                "q 0 0 200 100 re W n /S1 sh Q q 0 100 200 100 re W n /S2 sh Q",
                $"/Shading << /S1 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [50 0 150 0] {RedToBlue} /Extend [true false] >> "
                + $"/S2 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [50 0 150 0] {RedToBlue} /Extend [false true] >> >>"),
            PageInfo(RenderFlags.None),
            ShadingThreshold);

    /// <summary>Tiling patterns whose cells overlap and whose steps leave gaps match PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task TilingStepsMatchPdfium()
    {
        var pdf = new RenderTestPdf(ShapesSize, ShapesSize) { Content = "/Pattern cs /P1 scn 0 0 200 100 re f /P2 scn 0 100 200 100 re f" };
        var overlap = pdf.AddStream("/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 20 20] /XStep 10 /YStep 10 /Resources << >>", "1 0 0 rg 10 0 10 5 re f");
        var gaps = pdf.AddStream("/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 10 10] /XStep 20 /YStep 20 /Resources << >>", "0 0 1 rg 0 0 10 10 re f");
        pdf.Resources = $"/Pattern << /P1 {overlap} 0 R /P2 {gaps} 0 R >>";
        return AssertParity(nameof(TilingStepsMatchPdfium), pdf.ToBytes(), PageInfo(RenderFlags.None), PatternThreshold);
    }

    /// <summary>Zero-length dashes with round caps and lines thinner than a pixel match PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task DotsAndThinLinesMatchPdfium() =>
        AssertParity(
            nameof(DotsAndThinLinesMatchPdfium),
            Page("0 G 6 w 1 J [0 12] 0 d 10 50 m 190 50 l S [] 0 d 0.05 w 10 100.5 m 190 100.5 l S 0 w 10 150.5 m 190 150.5 l S", string.Empty),
            PageInfo(RenderFlags.None),
            LineThreshold);

    /// <summary>Square, Circle, Ink and Text annotations without appearances are generated as PDFium generates them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task GeneratedShapeAnnotationsMatchPdfium() =>
        AssertParity(
            nameof(GeneratedShapeAnnotationsMatchPdfium),
            AnnotationPage(
                "/Subtype /Square /Rect [10 10 90 90] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >> /F 4",
                "/Subtype /Circle /Rect [110 10 190 90] /C [0 0.5 0] /IC [1 1 0] /F 4",
                "/Subtype /Ink /Rect [10 110 90 190] /InkList [[20 120 80 180 80 120]] /C [1 0 1] /BS << /W 3 >> /F 4",
                "/Subtype /Text /Rect [120 120 180 180] /F 4"),
            PageInfo(RenderFlags.Annotations),
            AnnotationThreshold);

    /// <summary>Highlight, Underline, StrikeOut and Squiggly annotations without appearances are generated as PDFium generates them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task GeneratedMarkupAnnotationsMatchPdfium() =>
        AssertParity(
            nameof(GeneratedMarkupAnnotationsMatchPdfium),
            AnnotationPage(
                "/Subtype /Highlight /Rect [10 170 190 190] /QuadPoints [10 190 190 190 10 170 190 170] /C [0 1 1] /F 4",
                "/Subtype /Underline /Rect [10 120 190 140] /QuadPoints [10 140 190 140 10 120 190 120] /F 4",
                "/Subtype /StrikeOut /Rect [10 70 190 90] /QuadPoints [10 90 190 90 10 70 190 70] /C [1 0 0] /F 4",
                "/Subtype /Squiggly /Rect [10 20 190 40] /QuadPoints [10 40 190 40 10 20 190 20] /C [0 0 1] /F 4"),
            PageInfo(RenderFlags.Annotations),
            AnnotationThreshold);

    /// <summary>Printing leaves out annotations without the Print flag and includes NoView ones that print, as PDFium does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task PrintingAnnotationsMatchPdfium() =>
        AssertParity(
            nameof(PrintingAnnotationsMatchPdfium),
            AnnotationPage(
                "/Subtype /Square /Rect [10 10 90 90] /IC [1 0 0] /F 0",
                "/Subtype /Square /Rect [110 10 190 90] /IC [0 1 0] /F 36",
                "/Subtype /Square /Rect [10 110 90 190] /IC [0 0 1] /F 6",
                "/Subtype /Square /Rect [110 110 190 190] /IC [1 1 0] /F 4"),
            PageInfo(RenderFlags.Annotations | RenderFlags.Printing),
            AnnotationThreshold);

    /// <summary>Builds a page with content and resources.</summary>
    /// <param name="content">The content.</param>
    /// <param name="resources">The resource entries.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Page(string content, string resources) =>
        new RenderTestPdf(ShapesSize, ShapesSize) { Content = content, Resources = resources }.ToBytes();

    /// <summary>Gets a whole-page request at one pixel per point.</summary>
    /// <param name="flags">The render flags.</param>
    /// <returns>The request.</returns>
    private static PageRenderInfo PageInfo(RenderFlags flags) => new(FirstPage, 1, PageRotation.None, 0, 0, flags);

    /// <summary>Builds a yellow page drawing a transparency group at half alpha whose blue square multiplies.</summary>
    /// <param name="isolation">The /I entry.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] GroupPage(string isolation)
    {
        var pdf = new RenderTestPdf(ShapesSize, ShapesSize) { Content = "1 1 0 rg 0 0 200 200 re f /Half gs /Fm Do" };
        var form = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 200 200] /Group << /S /Transparency {isolation} >>", "/Mul gs 0 0 1 rg 40 40 120 120 re f");
        pdf.Resources = $"/ExtGState << {HalfState} /Mul << /Type /ExtGState /BM /Multiply >> >> /XObject << /Fm {form} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with annotations that have no appearance streams.</summary>
    /// <param name="annotations">Each annotation's entries other than /Type.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] AnnotationPage(params ReadOnlySpan<string> annotations)
    {
        var pdf = new RenderTestPdf(ShapesSize, ShapesSize);
        var references = new System.Text.StringBuilder();
        foreach (var entries in annotations)
        {
            _ = references.Append(System.Globalization.CultureInfo.InvariantCulture, $"{pdf.AddObject($"<< /Type /Annot {entries} >>")} 0 R ");
        }

        pdf.PageEntries = $"/Annots [{references}]";
        return pdf.ToBytes();
    }

    /// <summary>Creates the shapes page.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateShapes() => new RenderTestPdf(ShapesSize, ShapesSize) { Content = ShapesContent, Resources = ShapesResources }.ToBytes();

    /// <summary>Renders a page tile.</summary>
    /// <param name="document">The document.</param>
    /// <param name="info">The tile request.</param>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(IDocument document, in PageRenderInfo info, int width, int height)
    {
        var pixels = new byte[width * height * BytesPerPixel];
        var rendered = document.Render(info, new(pixels, width, height, width * BytesPerPixel));
        return rendered ? pixels : throw new InvalidOperationException("The page could not be rendered.");
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

    /// <summary>Saves pixels as a PNG in the test output folder.</summary>
    /// <param name="name">The file name without extension.</param>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    private static void SavePng(string name, byte[] pixels, int width, int height)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "render-failures");
        _ = Directory.CreateDirectory(folder);
        using var image = SKImage.FromPixelCopy(new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul), pixels, width * BytesPerPixel);
        using var data = image.Encode(SKEncodedImageFormat.Png, PngQuality);
        using var file = File.Create(Path.Combine(folder, $"{name}.png"));
        data.SaveTo(file);
    }

    /// <summary>Renders a page with both engines and asserts the images are close.</summary>
    /// <param name="name">The test name, used for saved images.</param>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="info">The tile request.</param>
    /// <param name="threshold">The largest mean absolute difference per channel accepted.</param>
    /// <returns>A task.</returns>
    private static Task AssertParity(string name, byte[] pdf, in PageRenderInfo info, double threshold) => AssertParity(name, pdf, info, threshold, 0);

    /// <summary>Renders a page or a square tile of it with both engines and asserts the images are close.</summary>
    /// <param name="name">The test name, used for saved images.</param>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="info">The tile request.</param>
    /// <param name="threshold">The largest mean absolute difference per channel accepted.</param>
    /// <param name="tileEdge">The tile edge in pixels, or 0 for the whole page.</param>
    /// <returns>A task.</returns>
    private static async Task AssertParity(string name, byte[] pdf, PageRenderInfo info, double threshold, int tileEdge)
    {
        using var input = new HyperPdfTestDocument(pdf);
        var reference = PdfiumReferenceRendering.Render(pdf, info, tileEdge, tileEdge);
        var width = reference.Width;
        var height = reference.Height;
        var expected = reference.Pixels.ToArray();
        var actual = Render(input.Document, info, width, height);
        var difference = MeanDifference(expected, actual);
        if (difference >= threshold)
        {
            SavePng($"{name}-pdfium", expected, width, height);
            SavePng($"{name}-hyperpdf", actual, width, height);
        }

        await Assert.That(difference).IsLessThan(threshold);
    }
}

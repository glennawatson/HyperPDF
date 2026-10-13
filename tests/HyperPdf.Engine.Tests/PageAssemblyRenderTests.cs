// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>Draws overlays, underlays and watermarks with the library, saves the result and renders it with HyperPDF and PDFium.</summary>
public sealed class PageAssemblyRenderTests
{
    /// <summary>The quarter turns in a half turn.</summary>
    private const float QuarterTurns = 2;

    /// <summary>The scale of the shrunk page.</summary>
    private const float HalfSize = 0.5F;

    /// <summary>The low edge of the clip.</summary>
    private const float ClipLow = 60;

    /// <summary>The high edge of the clip.</summary>
    private const float ClipHigh = 140;

    /// <summary>A column that lies on the shrunk page but outside the clip.</summary>
    private const int JustOutsideClip = 55;

    /// <summary>The offset of green in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The target page edge in points.</summary>
    private const int Edge = 200;

    /// <summary>The overlay page edge in points: half the target's.</summary>
    private const int SourceEdge = 100;

    /// <summary>The middle of the target page.</summary>
    private const int Centre = 100;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The largest mean absolute difference per channel accepted for shapes.</summary>
    private const double ShapeThreshold = 0.5;

    /// <summary>The largest mean absolute difference per channel accepted where text is drawn.</summary>
    private const double TextThreshold = 2;

    /// <summary>A point inside the overlay's blue square, in user space.</summary>
    private const int InsideSquare = 30;

    /// <summary>The same point in a fitted overlay, which is twice the size.</summary>
    private const int InsideFittedSquare = 60;

    /// <summary>A point in the underlay's blue square outside the page's green square.</summary>
    private const int BlueOnly = 50;

    /// <summary>A point in both the blue square and the page's green square.</summary>
    private const int Overlapped = 20;

    /// <summary>The green square's edge.</summary>
    private const int GreenEdge = 40;

    /// <summary>The folder (under the test output) the saved files go to, so they can be checked with other tools.</summary>
    private const string OutputFolder = "page-assembly-output";

    /// <summary>The edge of the test image in pixels.</summary>
    private const int ImageEdge = 4;

    /// <summary>The width an image watermark is drawn at, in points.</summary>
    private const float ImageWidth = 80;

    /// <summary>The distance from the page edge to a corner watermark.</summary>
    private const float CornerMargin = 10;

    /// <summary>A column inside the corner watermark.</summary>
    private const int CornerColumn = 150;

    /// <summary>A row inside the corner watermark.</summary>
    private const int CornerRow = 50;

    /// <summary>A column beside the centred watermark of a user unit page.</summary>
    private const int BesideWatermark = 130;

    /// <summary>The size of the text watermark.</summary>
    private const float TextSize = 40;

    /// <summary>The turn of the text watermark in degrees.</summary>
    private const float TextTurn = 45;

    /// <summary>The opacity of the text watermark.</summary>
    private const float TextOpacity = 0.5F;

    /// <summary>A pixel the text watermark does not reach.</summary>
    private const int FarCorner = 5;

    /// <summary>The red page content.</summary>
    private const string RedPage = "1 0 0 rg 0 0 200 200 re f";

    /// <summary>The blue square drawn by the overlay page.</summary>
    private const string BlueSquare = "0 0 1 rg 10 10 50 50 re f";

    /// <summary>A page drawn over the lower left of the target appears there and matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlayMatchesPdfium()
    {
        var saved = await Assemble(
            nameof(OverlayMatchesPdfium),
            Target(RedPage),
            Source(BlueSquare),
            static (target, source) => PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], PdfOverlayOptions.Default));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsBlue(pixels, InsideSquare, Edge - InsideSquare)).IsTrue();
        await Assert.That(IsRed(pixels, Edge - InsideSquare, InsideSquare)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>An underlay is hidden by the page's own content where they overlap, and matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnderlayIsCoveredByThePageContent()
    {
        var saved = await Assemble(
            nameof(UnderlayIsCoveredByThePageContent),
            Target($"0 1 0 rg 0 0 {GreenEdge} {GreenEdge} re f"),
            Source(BlueSquare),
            static (target, source) => PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], new() { Placement = PdfOverlayPlacement.Underlay }));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsBlue(pixels, BlueOnly, Edge - BlueOnly)).IsTrue();
        await Assert.That(IsBlue(pixels, Overlapped, Edge - Overlapped)).IsFalse();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>On a rotated target the overlay stays upright on the displayed page, as PDFium shows it.</summary>
    /// <param name="rotation">The target's /Rotate.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(90)]
    [Arguments(180)]
    [Arguments(270)]
    public async Task OverlayOnARotatedPageMatchesPdfium(int rotation)
    {
        var saved = await Assemble(
            $"{nameof(OverlayOnARotatedPageMatchesPdfium)}-{rotation}",
            Target(RedPage, $"/Rotate {rotation}"),
            Source(BlueSquare),
            static (target, source) => PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], PdfOverlayOptions.Default));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsBlue(pixels, InsideSquare, Edge - InsideSquare)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A small page fitted to a larger target is enlarged, centred and matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FittedOverlayMatchesPdfium()
    {
        var saved = await Assemble(
            nameof(FittedOverlayMatchesPdfium),
            Target(RedPage),
            Source(BlueSquare),
            static (target, source) => PdfDocumentOverlays.OverlayPages(target, source, [new(0, 0)], new() { Fit = PdfOverlayFit.Fit }));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        // The source fills the target at twice its size, so its square covers 20 to 120.
        await Assert.That(IsBlue(pixels, InsideFittedSquare, Edge - InsideFittedSquare)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A turned, translucent text watermark in the middle of the page is close to PDFium's drawing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWatermarkIsCloseToPdfium()
    {
        var saved = await Assemble(
            nameof(TextWatermarkIsCloseToPdfium),
            Target("1 1 0 rg 0 0 200 200 re f"),
            null,
            static (target, _) => PdfDocumentWatermarks.AddText(target, [0], "DRAFT", new() { FontSize = TextSize }, new() { RotationDegrees = TextTurn, Opacity = TextOpacity }));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsPlainYellow(pixels, FarCorner, FarCorner)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(TextThreshold);
    }

    /// <summary>A watermark in the top right corner of a rotated page lands in the displayed corner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CornerWatermarkOnARotatedPageMatchesPdfium()
    {
        var saved = await Assemble(
            nameof(CornerWatermarkOnARotatedPageMatchesPdfium),
            Target(RedPage, "/Rotate 90"),
            null,
            static (target, _) => PdfDocumentWatermarks.AddImage(
                target,
                [0],
                new(OpaqueBlue(), ImageEdge, ImageEdge, ImageWidth),
                new() { Anchor = PdfWatermarkAnchor.TopRight, Margin = CornerMargin }));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsBlue(pixels, CornerColumn, CornerRow)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A watermark on a page with a user unit is sized in points of physical size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WatermarkOnAUserUnitPageKeepsItsPhysicalSize()
    {
        var saved = await Assemble(
            nameof(WatermarkOnAUserUnitPageKeepsItsPhysicalSize),
            Target(RedPage, "/UserUnit 2"),
            null,
            static (target, _) => PdfDocumentWatermarks.AddImage(target, [0], new(OpaqueBlue(), ImageEdge, ImageEdge, ImageWidth), PdfWatermarkOptions.Default));
        using var pair = new EnginePair(saved);

        // The page is 400 points of physical size; half a pixel per point gives the 200 pixel image the reference draws per unit.
        var pixels = Render(pair, HalfSize);

        // 80 points at 2 points a unit is 40 units wide, centred: blue from 80 to 120.
        await Assert.That(IsBlue(pixels, Centre, Centre)).IsTrue();
        await Assert.That(IsBlue(pixels, BesideWatermark, Centre)).IsFalse();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A page made from an image shows the image the right way up and matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagePageMatchesPdfium()
    {
        var saved = PdfImagePages.CreateDocument([new(Quadrants(), Edge, Edge)], PdfImagePageOptions.Default, CancellationToken.None);
        await SaveForChecking(nameof(ImagePageMatchesPdfium), saved);
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsRed(pixels, FarCorner, FarCorner)).IsTrue();
        await Assert.That(IsPlainYellow(pixels, Edge - FarCorner, Edge - FarCorner)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A part split from a document renders as the page does in the whole document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitPartMatchesTheWholeDocument()
    {
        using var whole = PdfDocumentReader.Open(Target(RedPage), null);
        var part = PdfDocumentSplitting.ExtractPart(whole, new(null, 0, 1), CancellationToken.None);
        await SaveForChecking(nameof(SplitPartMatchesTheWholeDocument), part);
        using var pair = new EnginePair(part);
        var pixels = Render(pair);

        await Assert.That(IsRed(pixels, Centre, Centre)).IsTrue();
        await Assert.That(Difference(part, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A turned page with an annotation shows the annotation in the turned place, as PDFium draws it.</summary>
    /// <param name="turn">The quarter turns about the page centre.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task TurnedAnnotationMatchesPdfium(int turn)
    {
        var turned = Matrix3x2.CreateTranslation(-Centre, -Centre) * Matrix3x2.CreateRotation(turn * MathF.PI / QuarterTurns) * Matrix3x2.CreateTranslation(Centre, Centre);
        var saved = await Assemble(
            $"{nameof(TurnedAnnotationMatchesPdfium)}-{turn}",
            WithAnnotation(),
            null,
            (target, _) => PdfDocumentContentTransforms.TransformPages(target, [0], turned, PdfContentTransformOptions.Default));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>A shrunk page with a clip hides what lies outside the clip and keeps the annotation in step, as PDFium draws it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClippedShrunkPageMatchesPdfium()
    {
        var shrink = Matrix3x2.CreateScale(HalfSize) * Matrix3x2.CreateTranslation(Centre / QuarterTurns, Centre / QuarterTurns);
        var saved = await Assemble(
            nameof(ClippedShrunkPageMatchesPdfium),
            WithAnnotation(),
            null,
            (target, _) => PdfDocumentContentTransforms.TransformPages(target, [0], shrink, new() { Clip = new(ClipLow, ClipLow, ClipHigh, ClipHigh) }));
        using var pair = new EnginePair(saved);
        var pixels = Render(pair);

        await Assert.That(IsPlainWhite(pixels, FarCorner, FarCorner)).IsTrue();
        await Assert.That(IsPlainWhite(pixels, JustOutsideClip, Centre)).IsTrue();
        await Assert.That(IsRed(pixels, Centre, Centre)).IsTrue();
        await Assert.That(Difference(saved, pixels)).IsLessThan(ShapeThreshold);
    }

    /// <summary>Builds a red page with a square annotation whose appearance is a blue and green rectangle.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] WithAnnotation()
    {
        var pdf = new RenderTestPdf(Edge, Edge) { Content = RedPage };
        var appearance = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 40 20]", "0 0 1 rg 0 0 20 20 re f 0 1 0 rg 20 0 20 20 re f");
        pdf.PageEntries = $"/Annots [<< /Type /Annot /Subtype /Square /Rect [10 10 50 30] /F 4 /AP << /N {appearance} 0 R >> >>]";
        return pdf.ToBytes();
    }

    /// <summary>Determines whether a pixel is white.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row from the top.</param>
    /// <returns><see langword="true"/> when white.</returns>
    private static bool IsPlainWhite(byte[] pixels, int x, int y) => HasColour(pixels, x, y, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>Makes an image with a red top left, green top right, blue bottom left and yellow bottom right quarter.</summary>
    /// <returns>The BGRA pixels.</returns>
    private static byte[] Quadrants()
    {
        var pixels = new byte[Edge * Edge * BytesPerPixel];
        for (var y = 0; y < Edge; y++)
        {
            for (var x = 0; x < Edge; x++)
            {
                var at = ((y * Edge) + x) * BytesPerPixel;
                var right = x >= Centre;
                var bottom = y >= Centre;
                pixels[at] = bottom && !right ? byte.MaxValue : (byte)0;
                pixels[at + GreenOffset] = right ? byte.MaxValue : (byte)0;
                pixels[at + RedOffset] = !right || bottom ? byte.MaxValue : (byte)0;
                pixels[at + AlphaOffset] = byte.MaxValue;
            }
        }

        return pixels;
    }

    /// <summary>Builds a target page.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Target(string content) => Target(content, string.Empty);

    /// <summary>Builds a target page.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="entries">Extra page dictionary entries.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Target(string content, string entries) => new RenderTestPdf(Edge, Edge) { Content = content, PageEntries = entries }.ToBytes();

    /// <summary>Builds the page that is drawn over the target.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Source(string content) => new RenderTestPdf(SourceEdge, SourceEdge) { Content = content }.ToBytes();

    /// <summary>Makes an opaque blue image.</summary>
    /// <returns>The BGRA pixels.</returns>
    private static byte[] OpaqueBlue()
    {
        var pixels = new byte[ImageEdge * ImageEdge * BytesPerPixel];
        for (var at = 0; at < pixels.Length; at += BytesPerPixel)
        {
            pixels[at] = byte.MaxValue;
            pixels[at + BytesPerPixel - 1] = byte.MaxValue;
        }

        return pixels;
    }

    /// <summary>Opens the target, applies an edit and saves it.</summary>
    /// <param name="name">The test name, used for the saved file.</param>
    /// <param name="targetBytes">The target PDF.</param>
    /// <param name="sourceBytes">The PDF whose pages are drawn, or <see langword="null"/>.</param>
    /// <param name="edit">The edit.</param>
    /// <returns>The saved PDF.</returns>
    private static async Task<byte[]> Assemble(string name, byte[] targetBytes, byte[]? sourceBytes, Action<PdfDocument, PdfDocument> edit)
    {
        using var target = PdfDocumentReader.Open(targetBytes, null);
        using var source = PdfDocumentReader.Open(sourceBytes ?? targetBytes, null);
        edit(target, source);
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(target, output, CancellationToken.None);
        await SaveForChecking(name, output.ToArray());
        return output.ToArray();
    }

    /// <summary>Writes a saved file under the test output so other tools can check it.</summary>
    /// <param name="name">The test name.</param>
    /// <param name="bytes">The PDF bytes.</param>
    /// <returns>A task.</returns>
    private static async Task SaveForChecking(string name, byte[] bytes)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, OutputFolder);
        _ = Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, $"{name}.pdf"), bytes);
    }

    /// <summary>Renders the first page with HyperPDF at one pixel per point.</summary>
    /// <param name="pair">The document opened with both engines.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(EnginePair pair) => Render(pair, 1);

    /// <summary>Renders the first page at a scale per point of physical size.</summary>
    /// <param name="pair">The document in both engines.</param>
    /// <param name="scale">Device pixels per point of physical size.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(EnginePair pair, float scale)
    {
        var pixels = new byte[Edge * Edge * BytesPerPixel];
        var rendered = pair.HyperPdf.Render(new(0, scale, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, Edge, Edge, Edge * BytesPerPixel));
        return rendered ? pixels : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Gets the mean absolute difference per channel between HyperPDF's pixels and PDFium's.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="actual">HyperPDF's pixels.</param>
    /// <returns>The mean difference.</returns>
    private static double Difference(byte[] pdf, byte[] actual)
    {
        var expected = PdfiumReferenceRendering.Render(pdf, new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations)).Pixels.ToArray();
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }

    /// <summary>Determines whether a pixel is blue.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row from the top.</param>
    /// <returns><see langword="true"/> when blue.</returns>
    private static bool IsBlue(byte[] pixels, int x, int y) => HasColour(pixels, x, y, 0, 0, byte.MaxValue);

    /// <summary>Determines whether a pixel is red.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row from the top.</param>
    /// <returns><see langword="true"/> when red.</returns>
    private static bool IsRed(byte[] pixels, int x, int y) => HasColour(pixels, x, y, byte.MaxValue, 0, 0);

    /// <summary>Determines whether a pixel is yellow.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row from the top.</param>
    /// <returns><see langword="true"/> when yellow.</returns>
    private static bool IsPlainYellow(byte[] pixels, int x, int y) => HasColour(pixels, x, y, byte.MaxValue, byte.MaxValue, 0);

    /// <summary>Determines whether a pixel is close to a colour.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="x">The pixel's column.</param>
    /// <param name="y">The pixel's row from the top.</param>
    /// <param name="red">The red level.</param>
    /// <param name="green">The green level.</param>
    /// <param name="blue">The blue level.</param>
    /// <returns><see langword="true"/> when each channel is within 40 of the colour.</returns>
    private static bool HasColour(byte[] pixels, int x, int y, byte red, byte green, byte blue)
    {
        const int Tolerance = 40;
        var at = ((y * Edge) + x) * BytesPerPixel;
        return Math.Abs(pixels[at] - blue) < Tolerance && Math.Abs(pixels[at + GreenOffset] - green) < Tolerance && Math.Abs(pixels[at + RedOffset] - red) < Tolerance;
    }
}

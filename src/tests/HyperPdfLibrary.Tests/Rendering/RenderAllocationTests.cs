// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that replaying a recorded page into a tile allocates nothing on the managed heap.</summary>
/// <remarks>
/// Runs alone. Alongside other tests it once measured 5,032 bytes on net11.0 only, and 0 on its own. Debug builds of
/// the engine allocated on every read of a wide-element span table (now fixed by Optimize in the library project), and
/// one-off work can land in a pass on a busy machine, so the smallest of several passes is kept. The EventPipe allocation
/// audit of the render benchmark is the authoritative measurement.
/// </remarks>
[NotInParallel(nameof(RenderAllocationTests))]
public sealed class RenderAllocationTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 200;

    /// <summary>The tile size in pixels.</summary>
    private const int TileSize = 128;

    /// <summary>The renders made before measuring, so the picture is recorded and the code is warm.</summary>
    private const int Warmup = 4;

    /// <summary>The most measured passes made; the smallest counts.</summary>
    private const int Passes = 10;

    /// <summary>The renders measured.</summary>
    private const int Measured = 8;

    /// <summary>The offset of the second tile.</summary>
    private const int Offset = 64;

    /// <summary>The page content: a shading, alpha, a fill, a stroke, a clip and an image.</summary>
    private const string BusyContent = "/S1 sh /GS gs 1 0 0 rg 20 20 100 100 re f 0 0 1 RG 6 w 10 10 m 190 190 l S q 50 50 100 100 re W n q 100 0 0 100 50 50 cm /Im Do Q Q";

    /// <summary>The descriptor flags of the text page's non-symbolic font.</summary>
    private const int TextFontFlags = 32;

    /// <summary>The text page content: lines of filled, stroked, spaced and clipping text.</summary>
    private const string TextContent = "BT /F1 12 Tf 14 TL 5 90 Td (Hello, world! The quick brown fox) Tj T* 1 Tr (jumps over the lazy dog) Tj "
        + "T* 2 Tr 2 Tw [(AV) -120 (Wa) 50 (ter)] TJ T* 7 Tr (clip) Tj ET 0 0 1 rg 0 0 200 200 re f";

    /// <summary>Thin lines and a box added to the text page for the quality settings.</summary>
    private const string Lines = " 0.4 w 10 20.3 m 190 20.3 l S 10 40 50 20 re S";

    /// <summary>The axial shading painted behind everything.</summary>
    private const string BusyShading = "<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 200 0] /Function << /FunctionType 2 /Domain [0 1] /C0 [1 1 0] /C1 [0 1 1] /N 1 >> /Extend [true true] >>";

    /// <summary>A page with fills, strokes, a clip, an image, a shading, transparency and an annotation renders a tile with no allocation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TileRenderDoesNotAllocate()
    {
        using var page = new RenderTestPage(CreateBusyPage());
        PdfRenderFlags[] flagSets = [PdfRenderFlags.None, PdfRenderFlags.Annotations, PdfRenderFlags.Annotations | PdfRenderFlags.Grayscale];

        foreach (var flags in flagSets)
        {
            await Assert.That(MeasureTile(page, flags)).IsEqualTo(0L);
        }
    }

    /// <summary>
    /// A page whose annotations have no appearance streams renders a tile with no allocation once warm: the appearances are
    /// generated once, when the page's annotations are recorded, and the cached picture replays them.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GeneratedAnnotationTileRenderDoesNotAllocate()
    {
        using var page = new RenderTestPage(CreateGeneratedAnnotationPage());

        await Assert.That(MeasureTile(page, PdfRenderFlags.Annotations)).IsEqualTo(0L);
        await Assert.That(MeasureTile(page, PdfRenderFlags.Annotations | PdfRenderFlags.Printing)).IsEqualTo(0L);
    }

    /// <summary>A page of text in an embedded TrueType font, filled, stroked and clipped, renders a tile with no allocation once warm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextTileRenderDoesNotAllocate()
    {
        var spec = new FontSpec { Subtype = "TrueType", Program = TestFont.Create(), Flags = TextFontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = TextContent, };
        using var page = new RenderTestPage(FontTestDocument.Build(spec));

        await Assert.That(MeasureTile(page, PdfRenderFlags.None)).IsEqualTo(0L);
    }

    /// <summary>
    /// The same text page drawn with sharp edges, and with thin lines fitted to pixels, renders a tile with no allocation
    /// once warm.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QualityTileRenderDoesNotAllocate()
    {
        var spec = new FontSpec { Subtype = "TrueType", Program = TestFont.Create(), Flags = TextFontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = TextContent + Lines };
        using var page = new RenderTestPage(FontTestDocument.Build(spec));
        PdfRenderQuality[] settings =
        [
            PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Sharp, SmoothGraphics = false },
            PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Always, MinimumLineWidth = 1, SmoothImages = false },
        ];

        foreach (var quality in settings)
        {
            using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
            await Assert.That(MeasureTile(renderer, PdfRenderFlags.None)).IsEqualTo(0L);
        }
    }

    /// <summary>Renders a tile repeatedly and returns the bytes allocated by the measured renders.</summary>
    /// <param name="page">The page.</param>
    /// <param name="flags">The render options.</param>
    /// <returns>The bytes allocated on this thread.</returns>
    private static long MeasureTile(RenderTestPage page, PdfRenderFlags flags) => MeasureTile(page.Renderer, flags);

    /// <summary>Renders a tile repeatedly and returns the bytes allocated by the measured renders.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="flags">The render options.</param>
    /// <returns>The bytes allocated on this thread.</returns>
    private static long MeasureTile(PdfPageRenderer renderer, PdfRenderFlags flags)
    {
        var pixels = new byte[TileSize * TileSize * RenderedImage.BytesPerPixel];
        var request = new PdfTileRequest(0, 1, 0, Offset, Offset, flags);
        for (var i = 0; i < Warmup; i++)
        {
            _ = Render(renderer, request, pixels);
        }

        // One-off work can land in a measured pass on a busy machine: lazy statics first reached on a path the warm-up
        // missed, or code still running at tier 0 because the background tier-up was starved. A render that allocates
        // every time costs bytes in every pass, so the smallest pass decides and the loop stops at the first zero.
        var smallest = long.MaxValue;
        for (var pass = 0; pass < Passes && smallest > 0; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Measured; i++)
            {
                _ = Render(renderer, request, pixels);
            }

            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }

    /// <summary>Renders one tile.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="request">The tile request.</param>
    /// <param name="pixels">The target pixels.</param>
    /// <returns><see langword="true"/> when the tile rendered.</returns>
    private static bool Render(PdfPageRenderer renderer, in PdfTileRequest request, byte[] pixels) =>
        renderer.Render(request, new(pixels, TileSize, TileSize, TileSize * RenderedImage.BytesPerPixel));

    /// <summary>Creates a page whose annotations all have their appearances generated: shapes, markup, a note, a link border, a caret and a redaction mark.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateGeneratedAnnotationPage()
    {
        string[] annotations =
        [
            "/Subtype /Square /Rect [10 10 60 60] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >> /F 4",
            "/Subtype /Circle /Rect [70 10 120 60] /IC [0 1 0] /F 4",
            "/Subtype /Highlight /Rect [10 70 120 90] /QuadPoints [10 90 120 90 10 70 120 70] /F 4",
            "/Subtype /Underline /Rect [10 100 120 120] /QuadPoints [10 120 120 120 10 100 120 100] /F 4",
            "/Subtype /Ink /Rect [130 10 190 60] /InkList [[130 10 160 60 190 10]] /C [1 0 1] /F 4",
            "/Subtype /Text /Rect [130 70 160 100] /F 4",
            "/Subtype /Link /Rect [10 130 60 180] /C [1 0 0] /Border [0 0 2] /F 4",
            "/Subtype /Caret /Rect [70 130 120 180] /C [0 0 1] /F 4",
            "/Subtype /Redact /Rect [130 130 190 180] /OverlayText (X) /F 4",
        ];
        var pdf = new RenderTestPdf(Size, Size) { Content = "0.9 g 0 0 200 200 re f" };
        var references = string.Concat(annotations.Select(entries => $"{pdf.AddObject($"<< /Type /Annot {entries} >>")} 0 R "));
        pdf.PageEntries = $"/Annots [{references}]";
        return pdf.ToBytes();
    }

    /// <summary>Creates a page that uses most of the drawing features.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateBusyPage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = BusyContent };
        var image = pdf.AddStream(
            "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode",
            "FF0000 00FF00 0000FF FFFF00>");
        var state = pdf.AddObject("<< /Type /ExtGState /ca 0.5 >>");
        var appearance = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", "0 1 0 rg 0 0 10 10 re f");
        var annotation = pdf.AddObject($"<< /Type /Annot /Subtype /Square /Rect [100 100 140 140] /F 4 /AP << /N {appearance} 0 R >> >>");
        pdf.Resources = $"/XObject << /Im {image} 0 R >> /ExtGState << /GS {state} 0 R >> /Shading << /S1 {BusyShading} >>";
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        return pdf.ToBytes();
    }
}

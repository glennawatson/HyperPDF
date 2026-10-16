// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdf.Examples;

/// <summary>
/// Demonstrates creating a PDF document with bundled fonts, SkiaSharp raster images, flow layout blocks, and
/// rendering it back to a PNG image via the Skia backend.
/// </summary>
public static class EndToEndExample
{
    /// <summary>The banner image width in pixels.</summary>
    private const int BannerWidth = 240;

    /// <summary>The banner image height in pixels.</summary>
    private const int BannerHeight = 80;

    /// <summary>The banner display width in PDF points.</summary>
    private const float BannerDisplayWidth = 240F;

    /// <summary>The banner background red channel.</summary>
    private const byte BannerBgRed = 30;

    /// <summary>The banner background green channel.</summary>
    private const byte BannerBgGreen = 64;

    /// <summary>The banner background blue channel.</summary>
    private const byte BannerBgBlue = 124;

    /// <summary>The banner accent red channel.</summary>
    private const byte BannerAccentRed = 72;

    /// <summary>The banner accent green channel.</summary>
    private const byte BannerAccentGreen = 149;

    /// <summary>The banner accent blue channel.</summary>
    private const byte BannerAccentBlue = 239;

    /// <summary>The accent bar inset in pixels.</summary>
    private const float AccentInset = 12F;

    /// <summary>The accent bar width in pixels.</summary>
    private const float AccentWidth = 48F;

    /// <summary>The rule thickness in PDF points.</summary>
    private const float RuleThickness = 1.5F;

    /// <summary>The rule colour red channel in [0, 1].</summary>
    private const float RuleRed = 0.2F;

    /// <summary>The rule colour green channel in [0, 1].</summary>
    private const float RuleGreen = 0.35F;

    /// <summary>The rule colour blue channel in [0, 1].</summary>
    private const float RuleBlue = 0.6F;

    /// <summary>The proportional weight of the description column.</summary>
    private const float DescriptionWeight = 3F;

    /// <summary>The fixed width of the status column in points.</summary>
    private const float StatusColumnWidth = 90F;

    /// <summary>The status text in the summary table.</summary>
    private const string StatusReady = "Ready";

    /// <summary>The rendering scale in device pixels per PDF point.</summary>
    private const float RenderScale = 2F;

    /// <summary>The number of bytes per BGRA32 pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The PNG encoding quality.</summary>
    private const int PngQuality = 100;

    /// <summary>Creates a multi-block PDF document in memory, reads back its text, and renders its first page to a PNG image.</summary>
    /// <param name="cancellationToken">A token used to cancel asynchronous reading and rendering.</param>
    /// <returns>The generated PDF bytes, extracted text, and rendered first-page PNG bytes.</returns>
    public static async ValueTask<EndToEndExampleReport> RunAsync(CancellationToken cancellationToken)
    {
        var pdfBytes = CreateDocumentBytes(cancellationToken);
        var drawing = new PdfDrawingServices(new PdfRenderBackendRegistration().UseSkia());
        using var document = await PdfDocumentReader.OpenWithAsync(
            new MemoryPdfByteSource(pdfBytes),
            ownsSource: true,
            new() { DrawingServices = drawing },
            cancellationToken).ConfigureAwait(false);

        var textPage = await PdfDocumentText.GetTextPageAsync(document, 0, cancellationToken).ConfigureAwait(false);
        var page = PdfDocumentPages.GetPage(document, 0);
        var width = (int)MathF.Ceiling(page.Width * RenderScale);
        var height = (int)MathF.Ceiling(page.Height * RenderScale);
        var stride = width * BytesPerPixel;
        var pixels = new byte[height * stride];

        using var renderer = new PdfPageRenderer(document);
        await renderer.RenderAsync(
            new(0, RenderScale, 0, 0, 0, PdfRenderFlags.Annotations),
            pixels,
            width,
            height,
            stride,
            cancellationToken).ConfigureAwait(false);

        var pngBytes = EncodeBgraToPng(pixels, width, height);
        return new(pdfBytes, document.PageCount, textPage.Text, pngBytes, width, height);
    }

    /// <summary>Creates the sample PDF document in memory using <see cref="PdfDocumentCreator"/> and <see cref="PdfTextFlow"/>.</summary>
    /// <param name="cancellationToken">A token used to cancel image encoding and layout.</param>
    /// <returns>The saved PDF bytes.</returns>
    public static byte[] CreateDocumentBytes(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = "HyperPDF End-to-End Example", Author = "HyperPDF", Language = "en-AU", Tagged = true, UaClaim = PdfUaConformance.Part2 });

        var bodyFont = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, bold: false, italic: false).Source!);
        var headingFont = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, bold: true, italic: false).Source!);
        var bannerImage = CreateSkiaBannerImage(creator, cancellationToken);
        var flowOptions = new PdfFlowOptions(bodyFont) { HeadingFont = headingFont };

        PdfFlowBlock[] blocks =
        [
            new PdfFlowHeading(1, "HyperPDF End-to-End Report"),
            new PdfFlowRule { Thickness = RuleThickness, Color = new(RuleRed, RuleGreen, RuleBlue) },
            new PdfFlowParagraph("This document was created with PdfDocumentCreator and PdfTextFlow, including a SkiaSharp raster image, a horizontal rule, and an available-width table."),
            new PdfFlowImage(bannerImage, "Decorative blue banner", BannerDisplayWidth) { Alignment = PdfTableAlignment.Center, Fit = PdfFlowImageFit.ScaleToPage },
            new PdfFlowHeading(1, "Component Status"),
            CreateSummaryTable(),
        ];

        _ = PdfTextFlow.Add(creator, blocks, flowOptions, cancellationToken);
        _ = creator.Finish();
        return creator.ToArray();
    }

    /// <summary>
    /// Draws a raster banner in <see cref="SKColorType.Bgra8888"/> with SkiaSharp and embeds its raw pixel bytes into
    /// <paramref name="creator"/> via <see cref="PdfDocumentCreator.AddRaster"/>.
    /// </summary>
    /// <param name="creator">The PDF document creator.</param>
    /// <param name="cancellationToken">A token used to cancel image encoding.</param>
    /// <returns>The embedded PDF image handle.</returns>
    private static PdfCreatedImage CreateSkiaBannerImage(PdfDocumentCreator creator, CancellationToken cancellationToken)
    {
        var info = new SKImageInfo(BannerWidth, BannerHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new(BannerBgRed, BannerBgGreen, BannerBgBlue));
            using var paint = new SKPaint { Color = new(BannerAccentRed, BannerAccentGreen, BannerAccentBlue), IsAntialias = true };
            canvas.DrawRect(AccentInset, AccentInset, AccentWidth, BannerHeight - (AccentInset + AccentInset), paint);
        }

        var raster = new PdfRasterImage(PdfRasterFormat.Bgra32, bitmap.Width, bitmap.Height, bitmap.Bytes);
        return creator.AddRaster(raster, PdfImageOptions.Default, cancellationToken);
    }

    /// <summary>Builds a sample <see cref="PdfFlowTable"/> that fills the available content width.</summary>
    /// <returns>The configured flow table.</returns>
    private static PdfFlowTable CreateSummaryTable() =>
        new(
            [
                PdfTableRow.FromText("Component", "Description", "Status"),
                PdfTableRow.FromText("PdfDocumentCreator", "Creates PDF 1.7 / 2.0 documents with bundled fonts and tagged structure.", StatusReady),
                PdfTableRow.FromText("PdfTextFlow", "Flows headings, paragraphs, rules, images, code blocks, and tables across pages.", StatusReady),
                PdfTableRow.FromText("PdfPageRenderer", "Renders PDF pages to premultiplied BGRA pixel buffers via SkiaSharp.", StatusReady),
            ]) { FillAvailableWidth = true, ColumnSizing = [PdfTableColumnWidth.Auto, PdfTableColumnWidth.Proportional(DescriptionWeight), PdfTableColumnWidth.Fixed(StatusColumnWidth)] };

    /// <summary>Encodes a premultiplied BGRA8888 pixel buffer to PNG bytes using SkiaSharp.</summary>
    /// <param name="pixels">The premultiplied BGRA8888 pixel buffer.</param>
    /// <param name="width">The image width in pixels.</param>
    /// <param name="height">The image height in pixels.</param>
    /// <returns>The encoded PNG bytes.</returns>
    private static byte[] EncodeBgraToPng(ReadOnlySpan<byte> pixels, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        pixels.CopyTo(bitmap.GetPixelSpan());
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, PngQuality);
        return data.ToArray();
    }
}

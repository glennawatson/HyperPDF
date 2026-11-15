// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Render.Managed;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>The managed rasteriser follows the render quality: where text is placed, how its edges look, how dark it is and how thin lines are fitted.</summary>
public class ManagedRenderQualityTests
{
    /// <summary>A black line 1.4 pixels wide, a little off level.</summary>
    private const string BetweenLine = "0.7 w 20 20 m 180 20.3 l S";

    /// <summary>The font descriptor flags of a nonsymbolic font.</summary>
    private const int FontFlags = 32;

    /// <summary>The pixels drawn for each point.</summary>
    private const int Scale = 2;

    /// <summary>The darkest level of text.</summary>
    private const int DarkestText = 3;

    /// <summary>The thinnest line asked for, in device pixels.</summary>
    private const float ThickHairline = 3F;

    /// <summary>The parts a weight is divided into when two renderings are compared.</summary>
    private const int Whole = 10;

    /// <summary>The fewest parts of the other rendering's weight that count as about the same.</summary>
    private const int LowShare = 9;

    /// <summary>The most parts of the other rendering's weight that count as about the same.</summary>
    private const int HighShare = 11;

    /// <summary>A line of black text that starts between pixels.</summary>
    private const string Text = "BT /F1 24 Tf 20.3 60.4 Td (Hello) Tj ET";

    /// <summary>A black line far thinner than a pixel, a little off level.</summary>
    private const string Hairline = "0.05 w 20 20 m 180 20.3 l S";

    /// <summary>Smooth text has grey edge pixels and no colour, which is what every other option is compared with.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmoothTextHasGreyEdgesAndNoColour()
    {
        var look = Measure(Text, PdfRenderQuality.Default);

        await Assert.That(look.Grey).IsGreaterThan(0);
        await Assert.That(look.Coloured).IsEqualTo(0);
    }

    /// <summary>Sharp text turns every pixel fully on or off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharpTextHasNoGreyPixels()
    {
        var look = Measure(Text, PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Sharp });

        await Assert.That(look.Darkness).IsGreaterThan(0);
        await Assert.That(look.Grey).IsEqualTo(0);
    }

    /// <summary>Text smoothed with the screen's colour stripes has edge pixels whose red, green and blue differ, and weighs about the same as smooth text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StripeSmoothedTextColoursItsEdges()
    {
        var smooth = Measure(Text, PdfRenderQuality.Default);
        var striped = Measure(Text, PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Lcd });

        await Assert.That(striped.Coloured).IsGreaterThan(0);
        await Assert.That(striped.Darkness).IsGreaterThan(smooth.Darkness * LowShare / Whole);
        await Assert.That(striped.Darkness).IsLessThan(smooth.Darkness * HighShare / Whole);
    }

    /// <summary>Darker text weighs more than plain text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DarkerTextWeighsMore()
    {
        var plain = Measure(Text, PdfRenderQuality.Default);
        var darker = Measure(Text, PdfRenderQuality.Default with { TextContrast = DarkestText });

        await Assert.That(darker.Darkness).IsGreaterThan(plain.Darkness);
    }

    /// <summary>Hinted text is moved onto whole pixels, so it is drawn differently from text left between pixels, and weighs about the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HintedTextMovesOntoWholePixels()
    {
        var free = Render(Text, PdfRenderQuality.Default);
        var hinted = Render(Text, PdfRenderQuality.Default with { TextHinting = PdfTextHinting.Full });
        var freeLook = Measure(free);
        var hintedLook = Measure(hinted);

        await Assert.That(hinted.AsSpan().SequenceEqual(free)).IsFalse();
        await Assert.That(hintedLook.Darkness).IsGreaterThan(freeLook.Darkness * LowShare / Whole);
        await Assert.That(hintedLook.Darkness).IsLessThan(freeLook.Darkness * HighShare / Whole);
    }

    /// <summary>A minimum line width draws a thin line heavier, and fitting lines to whole pixels draws a line of 1.4 pixels as one pixel.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThinLinesAreDrawnHeavier()
    {
        var plain = Measure(Hairline, PdfRenderQuality.Default);
        var widened = Measure(Hairline, PdfRenderQuality.Default with { MinimumLineWidth = ThickHairline });
        var between = Measure(BetweenLine, PdfRenderQuality.Default);
        var fitted = Measure(BetweenLine, PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Always });

        await Assert.That(widened.Darkness).IsGreaterThan(plain.Darkness);
        await Assert.That(fitted.Darkness).IsLessThan(between.Darkness);
    }

    /// <summary>Shapes drawn without smoothing have no grey pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsmoothedShapesHaveNoGreyPixels()
    {
        const string triangle = "0 g 20.3 20.4 m 150.7 30.2 l 80.5 90.6 l h f";
        var smooth = Measure(triangle, PdfRenderQuality.Default);
        var hard = Measure(triangle, PdfRenderQuality.Default with { SmoothGraphics = false });

        await Assert.That(smooth.Grey).IsGreaterThan(0);
        await Assert.That(hard.Grey).IsEqualTo(0);
    }

    /// <summary>Renders a page and measures it.</summary>
    /// <param name="content">The page's content stream.</param>
    /// <param name="quality">The render quality.</param>
    /// <returns>What the page looks like.</returns>
    private static PageLook Measure(string content, PdfRenderQuality quality) => Measure(Render(content, quality));

    /// <summary>Measures the pixels of a page drawn on white paper.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The pixels that are neither paper nor fully inked, the pixels whose colours differ, and the ink summed over the page.</returns>
    private static PageLook Measure(byte[] pixels)
    {
        var grey = 0;
        var coloured = 0;
        long darkness = 0;
        for (var i = 0; i < pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            var first = pixels[i];
            var second = pixels[i + 1];
            var third = pixels[i + 1 + 1];
            if (first != second || second != third)
            {
                coloured++;
            }
            else if (first is not (byte.MinValue or byte.MaxValue))
            {
                grey++;
            }

            darkness += (byte.MaxValue * RenderedImage.BytesPerPixel) - first - second - third - byte.MaxValue;
        }

        return new(grey, coloured, darkness);
    }

    /// <summary>Renders the first page of a document with the managed rasteriser.</summary>
    /// <param name="content">The page's content stream.</param>
    /// <param name="quality">The render quality.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(string content, PdfRenderQuality quality)
    {
        var pdf = FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = FontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = content });
        var registration = new PdfRenderBackendRegistration().UseSkia().UseManaged();
        using var document = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = PdfFontLoader.Load, DrawingServices = new(registration) });
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { Quality = quality });
        const int width = FontTestDocument.PageWidth * Scale;
        const int height = FontTestDocument.PageHeight * Scale;
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel));
        return pixels;
    }

    /// <summary>What a rendered page looks like.</summary>
    /// <param name="Grey">The pixels that are neither paper nor fully inked.</param>
    /// <param name="Coloured">The pixels whose red, green and blue differ.</param>
    /// <param name="Darkness">The ink summed over the page: how far every colour byte is from white.</param>
    private readonly record struct PageLook(int Grey, int Coloured, long Darkness);
}

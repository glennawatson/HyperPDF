// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks small text drawn from cached glyph masks: its look against filled outlines, contrast, hinting, LCD edges and allocation.</summary>
[NotInParallel(nameof(RenderAllocationTests))]
public sealed class GlyphMaskRenderTests
{
    /// <summary>The descriptor flags of the non-symbolic test font.</summary>
    private const int FontFlags = 32;

    /// <summary>The renders made before measuring, so the picture is recorded and the code is warm.</summary>
    private const int Warmup = 4;

    /// <summary>The most measured passes made; the smallest counts.</summary>
    private const int Passes = 10;

    /// <summary>The renders measured.</summary>
    private const int Measured = 8;

    /// <summary>The height of the text's baseline, in points.</summary>
    private const double Baseline = 50;

    /// <summary>The share of ink a mask render may differ from an outline render by.</summary>
    private const double InkTolerance = 0.12;

    /// <summary>The share by which the strongest contrast must add ink.</summary>
    private const double ContrastGain = 1.05;

    /// <summary>The least difference between two channels of a pixel that counts as a colour fringe.</summary>
    private const int Fringe = 8;

    /// <summary>The offset of a baseline from a whole pixel, in points.</summary>
    private const double BaselineShift = 0.4;

    /// <summary>The position of the green channel in a BGRA pixel.</summary>
    private const int GreenSlot = 1;

    /// <summary>The position of the red channel in a BGRA pixel.</summary>
    private const int RedSlot = 2;

    /// <summary>The settings that draw from masks and nothing else.</summary>
    private static readonly PdfRenderQuality Masks = PdfRenderQuality.Default with { GlyphMasks = true };

    /// <summary>Verifies that text drawn from masks puts about as much ink on the page as the same text filled from outlines.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Masks_DrawAboutTheSameInkAsOutlines()
    {
        var outlines = Ink(RenderText(PdfRenderQuality.Default, Content(Baseline)));
        var masks = Ink(RenderText(Masks, Content(Baseline)));

        await Assert.That(outlines).IsGreaterThan(0);
        await Assert.That(Math.Abs(masks - outlines) / outlines).IsLessThan(InkTolerance).Because($"outlines {outlines}, masks {masks}");
    }

    /// <summary>Verifies that a higher contrast level puts more ink on the edges.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Contrast_AddsInkToTheEdges()
    {
        var plain = Ink(RenderText(Masks, Content(Baseline)));
        var strong = Ink(RenderText(Masks with { TextContrast = PdfRenderQuality.MaxTextContrast }, Content(Baseline)));

        await Assert.That(strong).IsGreaterThan(plain * ContrastGain);
    }

    /// <summary>Verifies that slight hinting puts text on whole rows, so a baseline off a whole pixel draws like one on it, and that without hinting it does not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SlightHinting_SnapsTheBaselineToARow()
    {
        var slight = Masks with { TextHinting = PdfTextHinting.Slight };

        var onRow = RenderText(slight, Content(Baseline));
        var offRow = RenderText(slight, Content(Baseline + BaselineShift));
        var unhintedOn = RenderText(Masks, Content(Baseline));
        var unhintedOff = RenderText(Masks, Content(Baseline + BaselineShift));

        await Assert.That(onRow.Pixels.AsSpan().SequenceEqual(offRow.Pixels)).IsTrue();
        await Assert.That(unhintedOn.Pixels.AsSpan().SequenceEqual(unhintedOff.Pixels)).IsFalse();
    }

    /// <summary>Verifies that LCD edges colour the fringes of black text, which grey edges never do.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Lcd_ColoursTheEdges()
    {
        var grey = RenderText(Masks, Content(Baseline));
        var lcd = RenderText(Masks with { TextEdges = PdfTextEdges.Lcd }, Content(Baseline));

        await Assert.That(HasFringe(grey)).IsFalse();
        await Assert.That(HasFringe(lcd)).IsTrue();
        await Assert.That(Ink(lcd)).IsGreaterThan(0);
    }

    /// <summary>Verifies that sharp edges from masks are fully dark or fully clear.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SharpEdges_HaveNoGreyLevels()
    {
        var image = RenderText(Masks with { TextEdges = PdfTextEdges.Sharp }, Content(Baseline));

        var greys = 0;
        for (var i = GreenSlot; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            greys += image.Pixels[i] is not (0 or byte.MaxValue) ? 1 : 0;
        }

        await Assert.That(greys).IsEqualTo(0);
        await Assert.That(Ink(image)).IsGreaterThan(0);
    }

    /// <summary>Verifies that text that is too large for a mask is still drawn, filled from its outline.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LargeText_IsStillDrawn()
    {
        var image = RenderText(Masks, "BT /F1 150 Tf 10 20 Td (H) Tj ET");

        await Assert.That(Ink(image)).IsGreaterThan(0);
    }

    /// <summary>Verifies that a page of text drawn from masks, with every option on, renders a tile with no allocation once warm.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MaskRender_DoesNotAllocate()
    {
        var all = Masks with { TextHinting = PdfTextHinting.Full, TextContrast = 1, TextEdges = PdfTextEdges.Lcd };
        PdfRenderQuality[] settings = [Masks, all];
        foreach (var quality in settings)
        {
            using var page = new RenderTestPage(Build(Content(Baseline)));
            using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });

            await Assert.That(MeasureRender(renderer)).IsEqualTo(0L);
        }
    }

    /// <summary>Makes the page content: a line of small black text.</summary>
    /// <param name="baseline">The height of the baseline in points.</param>
    /// <returns>The content.</returns>
    private static string Content(double baseline) =>
        string.Create(CultureInfo.InvariantCulture, $"BT /F1 10 Tf 10 {baseline} Td (Hello world, quick brown fox) Tj ET");

    /// <summary>Makes a document whose page holds content in an embedded font.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build(string content) =>
        FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = FontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = content });

    /// <summary>Renders the text page at scale 1.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <param name="content">The page content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderText(PdfRenderQuality quality, string content)
    {
        using var page = new RenderTestPage(Build(content));
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
        var pixels = new byte[FontTestDocument.PageWidth * FontTestDocument.PageHeight * RenderedImage.BytesPerPixel];
        _ = Render(renderer, pixels);
        return new(pixels, FontTestDocument.PageWidth, FontTestDocument.PageHeight);
    }

    /// <summary>Renders the page into pixels.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="pixels">The target pixels.</param>
    /// <returns><see langword="true"/> when it rendered.</returns>
    private static bool Render(PdfPageRenderer renderer, byte[] pixels) =>
        renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, FontTestDocument.PageWidth, FontTestDocument.PageHeight, FontTestDocument.PageWidth * RenderedImage.BytesPerPixel));

    /// <summary>Renders repeatedly and returns the bytes allocated by the measured renders.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>The bytes allocated on this thread; the smallest pass counts.</returns>
    private static long MeasureRender(PdfPageRenderer renderer)
    {
        var pixels = new byte[FontTestDocument.PageWidth * FontTestDocument.PageHeight * RenderedImage.BytesPerPixel];
        for (var i = 0; i < Warmup; i++)
        {
            _ = Render(renderer, pixels);
        }

        var smallest = long.MaxValue;
        for (var pass = 0; pass < Passes && smallest > 0; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Measured; i++)
            {
                _ = Render(renderer, pixels);
            }

            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }

    /// <summary>Adds up how dark the page is, by the green channel.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The ink.</returns>
    private static double Ink(RenderedImage image)
    {
        double ink = 0;
        for (var i = GreenSlot; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            ink += byte.MaxValue - image.Pixels[i];
        }

        return ink;
    }

    /// <summary>Determines whether any pixel has colour channels that differ.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns><see langword="true"/> when a pixel is coloured rather than grey.</returns>
    private static bool HasFringe(RenderedImage image)
    {
        for (var i = 0; i + RedSlot < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            if (Math.Abs(image.Pixels[i] - image.Pixels[i + RedSlot]) > Fringe)
            {
                return true;
            }
        }

        return false;
    }
}

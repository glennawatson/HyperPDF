// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders thin lines, zero-length dashes, miter limits, clip rules, ignored operators and text clips in hidden content.</summary>
[NotInParallel]
public sealed class StrokeEdgeRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 10;

    /// <summary>The largest difference accepted for a one pixel hairline, whose anti-aliasing varies.</summary>
    private const int HairlineTolerance = 64;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The device row a line at y 50.5 runs through.</summary>
    private const int HairlineRow = 49;

    /// <summary>The device column of a dash dot.</summary>
    private const int DotColumn = 25;

    /// <summary>The device column between two dash dots.</summary>
    private const int GapColumn = 20;

    /// <summary>The device row near the tip of a mitered corner.</summary>
    private const int MiterTipRow = 12;

    /// <summary>A device column inside the even-odd hole.</summary>
    private const int HoleColumn = 50;

    /// <summary>A device column inside the ring and the second clip.</summary>
    private const int RingColumn = 20;

    /// <summary>A device column outside the second clip.</summary>
    private const int OutsideColumn = 5;

    /// <summary>The device column in the clipping glyph.</summary>
    private const int GlyphColumn = 20;

    /// <summary>The device row in the clipping glyph.</summary>
    private const int GlyphRow = 80;

    /// <summary>The device row above the clipping glyph.</summary>
    private const int AboveGlyph = 40;

    /// <summary>A line far thinner than a pixel is still drawn one pixel wide, as PDFium draws it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThinLinesAreAtLeastOnePixelWide()
    {
        var image = Render("0.05 w 0 G 10 50.5 m 90 50.5 l S");

        await RenderCheck.Near(image, Middle, HairlineRow, Rgb.Black, HairlineTolerance, nameof(ThinLinesAreAtLeastOnePixelWide));
    }

    /// <summary>A zero-length dash with round caps draws a dot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZeroLengthDashesDrawRoundDots()
    {
        var image = Render("4 w 1 J [0 10] 0 d 0 G 5 50 m 95 50 l S");

        await RenderCheck.Near(image, DotColumn, Middle, Rgb.Black, Tolerance, nameof(ZeroLengthDashesDrawRoundDots));
        await RenderCheck.Near(image, GapColumn, Middle, Rgb.White, Tolerance, nameof(ZeroLengthDashesDrawRoundDots));
    }

    /// <summary>A sharp corner within the miter limit is mitered to a point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CornersWithinTheMiterLimitAreMitered()
    {
        var image = Render("0 G 10 w 0 j 10 M 20 20 m 50 80 l 80 20 l S");

        await RenderCheck.Near(image, Middle, MiterTipRow, Rgb.Black, Tolerance, nameof(CornersWithinTheMiterLimitAreMitered));
    }

    /// <summary>A corner beyond the miter limit is bevelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CornersBeyondTheMiterLimitAreBevelled()
    {
        var image = Render("0 G 10 w 0 j 2 M 20 20 m 50 80 l 80 20 l S");

        await RenderCheck.Near(image, Middle, MiterTipRow, Rgb.White, Tolerance, nameof(CornersBeyondTheMiterLimitAreBevelled));
    }

    /// <summary>A negative miter limit is treated as bevelling every corner rather than keeping Skia's default.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativeMiterLimitBevels()
    {
        var image = Render("0 G 10 w 0 j -1 M 20 20 m 50 80 l 80 20 l S");

        await RenderCheck.Near(image, Middle, MiterTipRow, Rgb.White, Tolerance, nameof(NegativeMiterLimitBevels));
    }

    /// <summary>Clips intersect, each with its own fill rule: an even-odd ring keeps its hole inside a non-zero clip.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipsIntersectWithTheirOwnRules()
    {
        var image = Render("0 0 100 100 re 30 30 40 40 re W* n 10 10 80 80 re W n 1 0 0 rg 0 0 100 100 re f");

        await RenderCheck.Near(image, HoleColumn, Middle, Rgb.White, Tolerance, nameof(ClipsIntersectWithTheirOwnRules));
        await RenderCheck.Near(image, RingColumn, Middle, Rgb.Red255, Tolerance, nameof(ClipsIntersectWithTheirOwnRules));
        await RenderCheck.Near(image, OutsideColumn, Middle, Rgb.White, Tolerance, nameof(ClipsIntersectWithTheirOwnRules));
    }

    /// <summary>A non-zero clip of two rectangles wound the same way has no hole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonZeroClipHasNoHole()
    {
        var image = Render("0 0 100 100 re 30 30 40 40 re W n 1 0 0 rg 0 0 100 100 re f");

        await RenderCheck.Near(image, HoleColumn, Middle, Rgb.Red255, Tolerance, nameof(NonZeroClipHasNoHole));
    }

    /// <summary>Rendering intent and flatness, by operator and in a graphics state, are parsed and ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IntentAndFlatnessAreIgnored()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Perceptual ri 50 i /GS gs 1 0 0 rg 0 0 100 100 re f" };
        pdf.Resources = "/ExtGState << /GS << /Type /ExtGState /RI /Saturation /FL 20 /SM 0.5 /SA true /HT /Default >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, Middle, Middle, Rgb.Red255, Tolerance, nameof(IntentAndFlatnessAreIgnored));
    }

    /// <summary>Clipping text in hidden optional content draws nothing but still clips what follows, as PDFium's clip state does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenClippingTextStillClips()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/OC /L1 BDC BT /F1 20 Tf 7 Tr 10 10 Td (A) Tj ET EMC 0 1 0 rg 0 0 100 100 re f" };
        var layer = pdf.AddObject("<< /Type /OCG /Name (Off) >>");
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >> /Properties << /L1 {layer} 0 R >>";
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{layer} 0 R] /D << /OFF [{layer} 0 R] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new SquareFont(dictionary));
        var image = page.RenderPage();

        await RenderCheck.Near(image, GlyphColumn, GlyphRow, Rgb.Green255, Tolerance, nameof(HiddenClippingTextStillClips));
        await RenderCheck.Near(image, GlyphColumn, AboveGlyph, Rgb.White, Tolerance, nameof(HiddenClippingTextStillClips));
    }

    /// <summary>Renders a page at one pixel per point.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content)
    {
        using var page = new RenderTestPage(new RenderTestPdf(Size, Size) { Content = content }.ToBytes());
        return page.RenderPage();
    }
}

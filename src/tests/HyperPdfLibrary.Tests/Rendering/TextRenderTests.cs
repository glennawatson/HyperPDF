// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders text with a test font, to check text positioning and the font factory.</summary>
[NotInParallel]
public sealed class TextRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 6;

    /// <summary>The device column in the middle of the first glyph.</summary>
    private const int FirstGlyph = 20;

    /// <summary>The device column in the middle of the second glyph, one advance of 10 points later.</summary>
    private const int SecondGlyph = 30;

    /// <summary>The device column in the middle of the third glyph, after a TJ adjustment.</summary>
    private const int ThirdGlyph = 45;

    /// <summary>The device row in the middle of the glyphs.</summary>
    private const int GlyphRow = 80;

    /// <summary>The device row of a point above the glyphs.</summary>
    private const int AboveGlyphs = 40;

    /// <summary>The fill-only mode.</summary>
    private const int FillMode = 0;

    /// <summary>The stroke-only mode.</summary>
    private const int StrokeMode = 1;

    /// <summary>The fill-and-stroke mode.</summary>
    private const int FillStrokeMode = 2;

    /// <summary>The invisible mode.</summary>
    private const int InvisibleMode = 3;

    /// <summary>The fill-and-clip mode.</summary>
    private const int FillClipMode = 4;

    /// <summary>The stroke-and-clip mode.</summary>
    private const int StrokeClipMode = 5;

    /// <summary>The fill, stroke and clip mode.</summary>
    private const int FillStrokeClipMode = 6;

    /// <summary>The clip-only mode.</summary>
    private const int ClipMode = 7;

    /// <summary>A column inside the four-point stroke but outside the glyph outline.</summary>
    private const int StrokeColumn = 9;

    /// <summary>The column inside a second glyph, fifty points after the first.</summary>
    private const int SeparatedGlyph = 70;

    /// <summary>The column between the separated glyph outlines.</summary>
    private const int GlyphGap = 45;

    /// <summary>Painting that covers the page with green.</summary>
    private const string PaintGreen = " 0 1 0 rg 0 0 100 100 re f";

    /// <summary>Text shows glyphs from the font at the text position, advancing by their widths.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAdvancesByGlyphWidths()
    {
        var image = RenderText("BT /F1 20 Tf 10 10 Td (AA) Tj ET");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(SecondGlyph, GlyphRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FirstGlyph, AboveGlyphs, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>A TJ adjustment moves the next glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrayAdjustmentsMoveTheTextPosition()
    {
        var image = RenderText("BT /F1 20 Tf 10 10 Td [(A) -1000 (A)] TJ ET");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(ThirdGlyph, GlyphRow, Rgb.White, Tolerance)).IsFalse();
    }

    /// <summary>Invisible text render mode draws nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvisibleTextIsNotDrawn()
    {
        var image = RenderText("BT /F1 20 Tf 3 Tr 10 10 Td (A) Tj ET");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>The clip render mode restricts later painting to the glyph shapes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipModeClipsToGlyphs()
    {
        var image = RenderText("BT /F1 20 Tf 7 Tr 10 10 Td (A) Tj ET 0 1 0 rg 0 0 100 100 re f");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FirstGlyph, AboveGlyphs, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Each ISO text mode paints its fill and stroke and applies clipping after the text object ends.</summary>
    /// <param name="mode">The text rendering mode from ISO 32000-1 Table 106.</param>
    /// <param name="fills">Whether the glyph interior is painted.</param>
    /// <param name="strokes">Whether the glyph outline is stroked.</param>
    /// <param name="clips">Whether later painting is restricted to the glyph outline.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FillMode, true, false, false)]
    [Arguments(StrokeMode, false, true, false)]
    [Arguments(FillStrokeMode, true, true, false)]
    [Arguments(InvisibleMode, false, false, false)]
    [Arguments(FillClipMode, true, false, true)]
    [Arguments(StrokeClipMode, false, true, true)]
    [Arguments(FillStrokeClipMode, true, true, true)]
    [Arguments(ClipMode, false, false, true)]
    public async Task TextModesPaintAndClip(int mode, bool fills, bool strokes, bool clips)
    {
        var content = string.Create(
            CultureInfo.InvariantCulture,
            $"1 0 0 rg 0 0 1 RG 4 w BT /F1 20 Tf {mode} Tr 10 10 Td (A) Tj ET");
        var painted = RenderText(content);
        var clipped = RenderText(content + PaintGreen);

        // The square occupies x=10..30 and y=70..90. These samples avoid its antialiased edges.
        var strokeColor = strokes ? Rgb.Blue255 : Rgb.White;
        await AssertPixelAsync(painted, FirstGlyph, GlyphRow, fills ? Rgb.Red255 : Rgb.White);
        await AssertPixelAsync(painted, StrokeColumn, GlyphRow, strokeColor);
        await AssertPixelAsync(clipped, FirstGlyph, GlyphRow, Rgb.Green255);
        await AssertPixelAsync(clipped, StrokeColumn, GlyphRow, clips ? strokeColor : Rgb.Green255);
        await AssertPixelAsync(clipped, FirstGlyph, AboveGlyphs, clips ? Rgb.White : Rgb.Green255);
    }

    /// <summary>Text clipping combines the outlines of separate glyphs.</summary>
    /// <param name="mode">A text mode that adds glyphs to the clipping path.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FillClipMode)]
    [Arguments(StrokeClipMode)]
    [Arguments(FillStrokeClipMode)]
    [Arguments(ClipMode)]
    public async Task TextClipCombinesGlyphOutlines(int mode)
    {
        var content = string.Create(
            CultureInfo.InvariantCulture,
            $"BT /F1 20 Tf {mode} Tr 10 10 Td (A) Tj 50 0 Td (A) Tj ET");
        var image = RenderText(content + PaintGreen);

        await AssertPixelAsync(image, FirstGlyph, GlyphRow, Rgb.Green255);
        await AssertPixelAsync(image, SeparatedGlyph, GlyphRow, Rgb.Green255);
        await AssertPixelAsync(image, GlyphGap, GlyphRow, Rgb.White);
        await AssertPixelAsync(image, FirstGlyph, AboveGlyphs, Rgb.White);
    }

    /// <summary>Restoring graphics state removes the clipping contributed by a text object.</summary>
    /// <param name="mode">A text mode that adds glyphs to the clipping path.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FillClipMode)]
    [Arguments(StrokeClipMode)]
    [Arguments(FillStrokeClipMode)]
    [Arguments(ClipMode)]
    public async Task RestoreRemovesTextClip(int mode)
    {
        var content = string.Create(
            CultureInfo.InvariantCulture,
            $"q BT /F1 20 Tf {mode} Tr 10 10 Td (A) Tj ET Q");
        var image = RenderText(content + PaintGreen);

        await AssertPixelAsync(image, FirstGlyph, GlyphRow, Rgb.Green255);
        await AssertPixelAsync(image, GlyphGap, GlyphRow, Rgb.Green255);
        await AssertPixelAsync(image, FirstGlyph, AboveGlyphs, Rgb.Green255);
    }

    /// <summary>Text shows nothing when no font factory is set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWithoutAFactoryShowsNothing()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "BT /F1 20 Tf 10 10 Td (A) Tj ET" };
        pdf.Resources = $"/Font << /F1 {pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>")} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes(), null);
        var image = page.RenderPage();

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Renders a page whose font /F1 is the square test font.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderText(string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        pdf.Resources = $"/Font << /F1 {pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>")} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new SquareFont(dictionary));
        return page.RenderPage();
    }

    /// <summary>Checks an interior pixel against an exact colour.</summary>
    /// <param name="image">The rendered pixels.</param>
    /// <param name="x">The device column.</param>
    /// <param name="y">The device row.</param>
    /// <param name="expected">The expected colour.</param>
    /// <returns>A task.</returns>
    private static async Task AssertPixelAsync(RenderedImage image, int x, int y, Rgb expected) =>
        await Assert.That(image.IsNear(x, y, expected, 0)).IsTrue().Because(image.Describe(x, y));
}

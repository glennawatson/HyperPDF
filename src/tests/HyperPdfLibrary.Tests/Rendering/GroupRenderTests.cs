// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders knockout and non-isolated transparency groups, fill and stroke knockout, soft mask backdrops and transfers, and text knockout.</summary>
[NotInParallel]
public sealed class GroupRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A channel at half strength over white.</summary>
    private const int Half = 128;

    /// <summary>A channel at quarter strength: half alpha twice over white.</summary>
    private const int Quarter = 64;

    /// <summary>A channel at three quarters: half of full plus half of half.</summary>
    private const int ThreeQuarters = 192;

    /// <summary>A device coordinate inside both squares of the group tests.</summary>
    private const int Overlap = 50;

    /// <summary>The device column inside only the first square.</summary>
    private const int FirstOnlyColumn = 20;

    /// <summary>The device row inside only the first square.</summary>
    private const int FirstOnlyRow = 80;

    /// <summary>The device column inside the stroke and the fill.</summary>
    private const int StrokeOverFill = 35;

    /// <summary>The device column inside the stroke outside the fill.</summary>
    private const int StrokeOnly = 25;

    /// <summary>A device column in the left half.</summary>
    private const int Left = 25;

    /// <summary>A device column in the right half.</summary>
    private const int Right = 75;

    /// <summary>The device column where two square glyphs overlap.</summary>
    private const int GlyphOverlap = 25;

    /// <summary>The device row through the glyphs.</summary>
    private const int GlyphRow = 80;

    /// <summary>The group content: a red square then an overlapping blue one, both at half alpha.</summary>
    private const string TwoSquares = "/Half gs 1 0 0 rg 10 10 50 50 re f 0 0 1 rg 40 40 50 50 re f";

    /// <summary>The half alpha graphics state.</summary>
    private const string HalfState = "/Half << /Type /ExtGState /ca 0.5 /CA 0.5 >>";

    /// <summary>In a knockout group the second square replaces the first where they overlap instead of showing it through.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnockoutGroupObjectsReplaceEachOther()
    {
        var image = RenderGroup("/I true /K true", TwoSquares, "/Fm Do", string.Empty);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Half, Rgb.Blue255.Blue), Tolerance, nameof(KnockoutGroupObjectsReplaceEachOther));
        await RenderCheck.Near(image, FirstOnlyColumn, FirstOnlyRow, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(KnockoutGroupObjectsReplaceEachOther));
    }

    /// <summary>Without knockout the overlapping squares composite over each other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonKnockoutGroupCompositesObjects()
    {
        var image = RenderGroup("/I true", TwoSquares, "/Fm Do", string.Empty);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Quarter, ThreeQuarters), Tolerance, nameof(NonKnockoutGroupCompositesObjects));
    }

    /// <summary>A non-isolated group's blend modes mix with the page beneath it, even when the group is composited with alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonIsolatedGroupBlendsWithTheBackdrop()
    {
        var image = RenderGroup("/I false", "/Mul gs 0 0 1 rg 20 20 60 60 re f", "1 1 0 rg 0 0 100 100 re f /Half gs /Fm Do", "/Mul << /Type /ExtGState /BM /Multiply >>");

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Half, 0), Tolerance, nameof(NonIsolatedGroupBlendsWithTheBackdrop));
    }

    /// <summary>An isolated group's blend modes see a transparent backdrop, so Multiply leaves the colour unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IsolatedGroupIgnoresTheBackdrop()
    {
        var image = RenderGroup("/I true", "/Mul gs 0 0 1 rg 20 20 60 60 re f", "1 1 0 rg 0 0 100 100 re f /Half gs /Fm Do", "/Mul << /Type /ExtGState /BM /Multiply >>");

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Half, Half), Tolerance, nameof(IsolatedGroupIgnoresTheBackdrop));
    }

    /// <summary>A translucent stroke replaces the fill under it, as PDFium draws a fill and stroke pair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TranslucentStrokeKnocksOutItsFill()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 0 0 1 RG /Half gs 20 w 30 30 40 40 re B", Resources = $"/ExtGState << {HalfState} >>" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, StrokeOverFill, Overlap, new(Half, Half, Rgb.Blue255.Blue), Tolerance, nameof(TranslucentStrokeKnocksOutItsFill));
        await RenderCheck.Near(image, StrokeOnly, Overlap, new(Half, Half, Rgb.Blue255.Blue), Tolerance, nameof(TranslucentStrokeKnocksOutItsFill));
        await RenderCheck.Near(image, Overlap, Overlap, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(TranslucentStrokeKnocksOutItsFill));
    }

    /// <summary>A luminosity mask's /BC backdrop shows where its group paints nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMaskBackdropColourFillsUnpaintedAreas()
    {
        var image = RenderMasked("0 g 0 0 50 100 re f", "/BC [1]");

        await RenderCheck.Near(image, Left, Overlap, Rgb.White, Tolerance, nameof(SoftMaskBackdropColourFillsUnpaintedAreas));
        await RenderCheck.Near(image, Right, Overlap, Rgb.Red255, Tolerance, nameof(SoftMaskBackdropColourFillsUnpaintedAreas));
    }

    /// <summary>A luminosity mask's /TR transfer function maps its coverage, here inverting it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMaskTransferFunctionMapsCoverage()
    {
        var image = RenderMasked("1 g 0 0 50 100 re f", "/TR << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >>");

        await RenderCheck.Near(image, Left, Overlap, Rgb.White, Tolerance, nameof(SoftMaskTransferFunctionMapsCoverage));
        await RenderCheck.Near(image, Right, Overlap, Rgb.Red255, Tolerance, nameof(SoftMaskTransferFunctionMapsCoverage));
    }

    /// <summary>With text knockout on, overlapping glyphs of one show composite once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextKnockoutCompositesAShowOnce()
    {
        var image = RenderText(string.Empty);

        await RenderCheck.Near(image, GlyphOverlap, GlyphRow, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(TextKnockoutCompositesAShowOnce));
    }

    /// <summary>With text knockout off, overlapping glyphs show through each other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWithoutKnockoutCompositesEachGlyph()
    {
        var image = RenderText("/TK false");

        await RenderCheck.Near(image, GlyphOverlap, GlyphRow, new(Rgb.Red255.Red, Quarter, Quarter), Tolerance, nameof(TextWithoutKnockoutCompositesEachGlyph));
    }

    /// <summary>Renders a page that draws a transparency group form.</summary>
    /// <param name="group">The extra /Group entries.</param>
    /// <param name="formContent">The form's content.</param>
    /// <param name="pageContent">The page content, which draws the form as /Fm.</param>
    /// <param name="states">Extra ExtGState entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderGroup(string group, string formContent, string pageContent, string states)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = pageContent };
        var form = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency {group} >>", formContent);
        pdf.Resources = $"/ExtGState << {HalfState} {states} >> /XObject << /Fm {form} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }

    /// <summary>Renders a red page through a luminosity soft mask.</summary>
    /// <param name="maskContent">The mask group's content.</param>
    /// <param name="maskEntries">Extra soft mask entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderMasked(string maskContent, string maskEntries)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs 1 0 0 rg 0 0 100 100 re f" };
        var group = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceGray >>", maskContent);
        var state = pdf.AddObject($"<< /Type /ExtGState /SMask << /Type /Mask /S /Luminosity /G {group} 0 R {maskEntries} >> >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }

    /// <summary>Renders two overlapping square glyphs in red at half alpha.</summary>
    /// <param name="extraState">Extra ExtGState entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderText(string extraState)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg /GS gs BT /F1 20 Tf 10 10 Td (AA) Tj ET" };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >> /ExtGState << /GS << /Type /ExtGState /ca 0.5 {extraState} >> >>";
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new SquareFont(dictionary));
        return page.RenderPage();
    }
}

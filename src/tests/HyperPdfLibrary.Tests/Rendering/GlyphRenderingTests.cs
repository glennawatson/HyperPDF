// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Draws square test glyphs through the recording device and checks the pixels they leave.</summary>
[NotInParallel]
public sealed class GlyphRenderingTests
{
    /// <summary>The page width.</summary>
    private const int Width = 160;

    /// <summary>The page height.</summary>
    private const int Height = 80;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>Opaque white.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>Opaque black.</summary>
    private const uint Black = 0xFF000000;

    /// <summary>Opaque red.</summary>
    private const uint Red = 0xFFFF0000;

    /// <summary>The row of pixels through the middle of the first line of text.</summary>
    private const int FirstLineRow = 50;

    /// <summary>The row of pixels through the middle of the second line of text.</summary>
    private const int SecondLineRow = 20;

    /// <summary>A column inside the first glyph of a line.</summary>
    private const int InsideFirstGlyph = 20;

    /// <summary>A column between the first and second glyph when the glyphs are spaced apart.</summary>
    private const int InGap = 31;

    /// <summary>A column inside the third glyph when the glyphs are spaced apart.</summary>
    private const int InsideThirdGlyph = 64;

    /// <summary>A column covered by two neighbouring glyphs when the glyphs are not spaced apart.</summary>
    private const int InsideOverlap = 35;

    /// <summary>A column beyond the last of five glyphs that are not spaced apart.</summary>
    private const int PastLastGlyph = 75;

    /// <summary>A column in the first glyph, left of the red rectangle.</summary>
    private const int LeftOfRectangle = 15;

    /// <summary>A column inside the red rectangle.</summary>
    private const int InsideRectangle = 25;

    /// <summary>Glyphs with a gap between them leave the gap empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparateGlyphsLeaveTheirGapsEmpty()
    {
        var pixels = Render(CreatePdf("BT /F1 20 Tf 12 Tc 10 20 Td (AAAAA) Tj ET"));

        await Assert.That(PixelAt(pixels, InsideFirstGlyph, FirstLineRow)).IsEqualTo(Black);
        await Assert.That(PixelAt(pixels, InGap, FirstLineRow)).IsEqualTo(White);
        await Assert.That(PixelAt(pixels, InsideThirdGlyph, FirstLineRow)).IsEqualTo(Black);
    }

    /// <summary>Glyphs that overlap paint one solid area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingGlyphsPaintOneArea()
    {
        var pixels = Render(CreatePdf("BT /F1 20 Tf 10 20 Td (AAAAA) Tj ET"));

        await Assert.That(PixelAt(pixels, InsideFirstGlyph, FirstLineRow)).IsEqualTo(Black);
        await Assert.That(PixelAt(pixels, InsideOverlap, FirstLineRow)).IsEqualTo(Black);
        await Assert.That(PixelAt(pixels, PastLastGlyph, FirstLineRow)).IsEqualTo(White);
    }

    /// <summary>A shape drawn between two lines of text paints over the first and under the second.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShapeBetweenTextKeepsPaintOrder()
    {
        var pixels = Render(CreatePdf("BT /F1 20 Tf 12 Tc 10 20 Td (AAAA) Tj ET 1 0 0 rg 20 20 24 20 re f 0 0 0 rg BT /F1 20 Tf 12 Tc 10 50 Td (AAAA) Tj ET"));

        await Assert.That(PixelAt(pixels, LeftOfRectangle, FirstLineRow)).IsEqualTo(Black);
        await Assert.That(PixelAt(pixels, InsideRectangle, FirstLineRow)).IsEqualTo(Red);
        await Assert.That(PixelAt(pixels, InsideFirstGlyph, SecondLineRow)).IsEqualTo(Black);
    }

    /// <summary>Reads one pixel of a BGRA surface as a packed colour.</summary>
    /// <param name="pixels">The surface pixels.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The colour with alpha in the top byte and blue in the bottom byte.</returns>
    private static uint PixelAt(byte[] pixels, int x, int y) => BitConverter.ToUInt32(pixels, ((y * Width) + x) * PixelBytes);

    /// <summary>Creates a page with a square test font.</summary>
    /// <param name="content">The page's graphics operators.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePdf(string content)
    {
        var pdf = new RenderTestPdf(Width, Height) { Content = content };
        pdf.Resources = $"/Font << /F1 {pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>")} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Records a page directly and draws the recording on a software surface.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The software pixels.</returns>
    /// <exception cref="InvalidOperationException">The test surface cannot be read.</exception>
    private static unsafe byte[] Render(byte[] pdf)
    {
        using var document = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions
        {
            FontFactory = static dictionary => new SquareFont(dictionary),
            DrawingServices = SkiaTestRegistration.CreateServices(),
        });
        var page = PdfDocumentPages.GetPage(document, 0);
        var bounds = new PdfRect(0, 0, page.Width, page.Height);
        using var device = new SkiaContentDevice(new(0, 0, page.Width, page.Height));
        var builder = new PdfPathBuilder();
        builder.AddRect(bounds);
        device.Clip(builder.Detach(), false, Matrix3x2.Identity);
        using (var interpreter = ContentInterpreter.Rent(PdfDocumentRendering.GetRenderCache(document), device, 0))
        {
            ContentExecution.RunPage(interpreter, page);
        }

        using var picture = device.Finish();
        var info = new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawPicture(((SkiaRenderPicture)picture).Native);
        using var image = surface.Snapshot();
        var pixels = new byte[Width * Height * PixelBytes];
        fixed (byte* pointer = pixels)
        {
            if (!image.ReadPixels(info, (nint)pointer, Width * PixelBytes))
            {
                throw new InvalidOperationException("Could not read the raster surface.");
            }
        }

        return pixels;
    }
}

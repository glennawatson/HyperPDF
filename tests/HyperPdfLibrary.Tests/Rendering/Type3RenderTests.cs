// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders Type 3 glyphs that give their own colour (<c>d0</c>) or only a shape (<c>d1</c>).</summary>
[NotInParallel]
public sealed class Type3RenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>The device column in the glyph.</summary>
    private const int GlyphColumn = 20;

    /// <summary>The device row in the glyph.</summary>
    private const int GlyphRow = 80;

    /// <summary>A d0 glyph sets its own colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColouredGlyphUsesItsOwnColour()
    {
        var image = Render("1000 0 d0 0 1 0 rg 0 0 1000 1000 re f");

        await RenderCheck.Near(image, GlyphColumn, GlyphRow, Rgb.Green255, Tolerance, nameof(ColouredGlyphUsesItsOwnColour));
    }

    /// <summary>A d1 glyph's colour operators are ignored, so it paints in the text's fill colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShapeGlyphUsesTheFillColour()
    {
        var image = Render("1000 0 0 0 1000 1000 d1 0 1 0 rg 0 0 1000 1000 re f");

        await RenderCheck.Near(image, GlyphColumn, GlyphRow, Rgb.Red255, Tolerance, nameof(ShapeGlyphUsesTheFillColour));
    }

    /// <summary>A d1 glyph's image paints its square in the fill colour, as PDFium draws shape glyphs as masks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShapeGlyphImagePaintsTheFillColour()
    {
        var image = Render("1000 0 0 0 1000 1000 d1 q 1000 0 0 1000 0 0 cm /Im Do Q");

        await RenderCheck.Near(image, GlyphColumn, GlyphRow, Rgb.Red255, Tolerance, nameof(ShapeGlyphImagePaintsTheFillColour));
    }

    /// <summary>Renders red text in a Type 3 font with one glyph procedure.</summary>
    /// <param name="procedure">The glyph procedure.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string procedure)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg BT /F1 20 Tf 10 10 Td (A) Tj ET" };
        var glyph = pdf.AddStream(string.Empty, procedure);
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode", "0000FF>");
        var font = pdf.AddObject(
            $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /a {glyph} 0 R >> "
            + $"/Encoding << /Differences [65 /a] >> /FirstChar 65 /LastChar 65 /Widths [1000] /Resources << /XObject << /Im {image} 0 R >> >> >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new TestType3Font(dictionary));
        return page.RenderPage();
    }
}

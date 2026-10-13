// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders pages under transfer functions (ISO 32000-2 10.5), which map the final device colour of every paint.</summary>
[NotInParallel]
public sealed class TransferRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The largest channel difference accepted for flat colours.</summary>
    private const int Tolerance = 2;

    /// <summary>The largest channel difference accepted for gradient colours, which differ a little between engines.</summary>
    private const int GradientTolerance = 10;

    /// <summary>The device column near the left edge of the page.</summary>
    private const int LeftEdge = 2;

    /// <summary>The device row in the middle of the glyph square.</summary>
    private const int GlyphRow = 70;

    /// <summary>The device column in the middle of the glyph square.</summary>
    private const int GlyphColumn = 30;

    /// <summary>A full channel.</summary>
    private const int Full = 255;

    /// <summary>The channel value of the quarter gray the tests paint.</summary>
    private const int QuarterGray = 64;

    /// <summary>The channel value an inverting function gives the quarter gray.</summary>
    private const int InvertedQuarterGray = 191;

    /// <summary>The red channel of the left edge of a red to blue gradient, inverted.</summary>
    private const int EdgeRed = 5;

    /// <summary>The blue channel of the left edge of a red to blue gradient, inverted.</summary>
    private const int EdgeBlue = 250;

    /// <summary>The graphics states the tests name.</summary>
    private const string States =
        "/ExtGState << /Inv << /TR2 << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> >> "
        + "/OldInv << /TR << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> >> "
        + "/Default << /TR2 /Default /TR << /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> >> "
        + "/Red << /TR [<< /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >> << /FunctionType 2 /Domain [0 1] /C0 [0] /C1 [1] /N 1 >> "
        + "<< /FunctionType 2 /Domain [0 1] /C0 [1] /C1 [0] /N 1 >>] >> >>";

    /// <summary>An axial shading from red at the left edge to blue at the right.</summary>
    private const string RedToBlue =
        "/Shading << /Sh0 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] "
        + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >> >> >>";

    /// <summary>The colour of the quarter gray.</summary>
    private static readonly Rgb Quarter = new(QuarterGray, QuarterGray, QuarterGray);

    /// <summary>The colour of the quarter gray after inversion.</summary>
    private static readonly Rgb InvertedQuarter = new(InvertedQuarterGray, InvertedQuarterGray, InvertedQuarterGray);

    /// <summary>Cyan, the inversion of red.</summary>
    private static readonly Rgb Cyan = new(0, Full, Full);

    /// <summary>Magenta, the inversion of green.</summary>
    private static readonly Rgb Magenta = new(Full, 0, Full);

    /// <summary>A /TR2 function inverts a gray fill, as PDFium does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Tr2InvertsAGrayFill()
    {
        var plain = Render("0.25 g 0 0 100 100 re f");
        var inverted = Render("/Inv gs 0.25 g 0 0 100 100 re f");

        await RenderCheck.Near(plain, Middle, Middle, Quarter, Tolerance, nameof(Tr2InvertsAGrayFill));
        await RenderCheck.Near(inverted, Middle, Middle, InvertedQuarter, Tolerance, nameof(Tr2InvertsAGrayFill));
    }

    /// <summary>The older /TR key does the same, /TR2 wins over it, and /Default turns the function off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Tr2WinsOverTrAndDefaultTurnsItOff()
    {
        var old = Render("/OldInv gs 1 0 0 rg 0 0 100 100 re f");
        var overridden = Render("/Default gs 1 0 0 rg 0 0 100 100 re f");

        await RenderCheck.Near(old, Middle, Middle, Cyan, Tolerance, nameof(Tr2WinsOverTrAndDefaultTurnsItOff));
        await RenderCheck.Near(overridden, Middle, Middle, Rgb.Red255, Tolerance, nameof(Tr2WinsOverTrAndDefaultTurnsItOff));
    }

    /// <summary>An array of functions maps each of red, green and blue by its own function.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrayMapsEachChannelByItsOwnFunction()
    {
        var image = Render("/Red gs 0 0 0 rg 0 0 100 100 re f");

        await RenderCheck.Near(image, Middle, Middle, Magenta, Tolerance, nameof(ArrayMapsEachChannelByItsOwnFunction));
    }

    /// <summary>Strokes are mapped by the same function.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokesAreMapped()
    {
        var image = Render("/Inv gs 1 0 0 RG 40 w 0 50 m 100 50 l S");

        await RenderCheck.Near(image, Middle, Middle, Cyan, Tolerance, nameof(StrokesAreMapped));
    }

    /// <summary>Glyphs are mapped by the same function.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextIsMapped()
    {
        var image = Render("/Inv gs 0 0 1 rg BT /F1 40 Tf 10 10 Td (A) Tj ET");

        await RenderCheck.Near(image, GlyphColumn, GlyphRow, Rgb.Yellow, Tolerance, nameof(TextIsMapped));
    }

    /// <summary>Images are mapped pixel by pixel, and a stencil mask takes the mapped fill colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagesAndMasksAreMapped()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Inv gs 100 0 0 100 0 0 cm /Im Do" };
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode", "00FF00>");
        pdf.Resources = $"{States} /XObject << /Im {image} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        var picture = page.RenderPage();

        var maskPdf = new RenderTestPdf(Size, Size) { Content = "/Inv gs 0 0 1 rg 100 0 0 100 0 0 cm /Mask Do" };
        var mask = maskPdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ImageMask true /BitsPerComponent 1 /Filter /ASCIIHexDecode", "00>");
        maskPdf.Resources = $"{States} /XObject << /Mask {mask} 0 R >>";
        using var maskPage = new RenderTestPage(maskPdf.ToBytes());
        var stencil = maskPage.RenderPage();

        await RenderCheck.Near(picture, Middle, Middle, Magenta, Tolerance, nameof(ImagesAndMasksAreMapped));
        await RenderCheck.Near(stencil, Middle, Middle, Rgb.Yellow, Tolerance, nameof(ImagesAndMasksAreMapped));
    }

    /// <summary>Inline images are mapped too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImagesAreMapped()
    {
        var image = Render("/Inv gs q 100 0 0 100 0 0 cm BI /W 1 /H 1 /CS /RGB /BPC 8 /F /AHx ID 00FF00> EI Q");

        await RenderCheck.Near(image, Middle, Middle, Magenta, Tolerance, nameof(InlineImagesAreMapped));
    }

    /// <summary>A shading painted by <c>sh</c> is mapped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingsAreMapped()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Inv gs /Sh0 sh" };
        pdf.Resources = $"{States} {RedToBlue}";
        using var page = new RenderTestPage(pdf.ToBytes());
        var image = page.RenderPage();

        await RenderCheck.Near(image, LeftEdge, Middle, new(EdgeRed, Full, EdgeBlue), GradientTolerance, nameof(ShadingsAreMapped));
    }

    /// <summary><c>Q</c> restores the function, so later paint is not mapped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestoreEndsTheFunction()
    {
        var image = Render("q /Inv gs Q 0.25 g 0 0 100 100 re f");

        await RenderCheck.Near(image, Middle, Middle, Quarter, Tolerance, nameof(RestoreEndsTheFunction));
    }

    /// <summary>Renders content on a white page with the test graphics states and a square-glyph font.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        pdf.Resources = $"{States} /Font << /F1 {font} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new SquareFont(dictionary));
        return page.RenderPage();
    }
}

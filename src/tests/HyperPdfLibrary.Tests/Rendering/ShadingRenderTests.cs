// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders shadings and patterns.</summary>
public sealed class ShadingRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>Content that paints the shading named S1.</summary>
    private const string PaintShading = "/S1 sh";

    /// <summary>The largest channel difference accepted for the coarse mesh colours.</summary>
    private const int MeshTolerance = 72;

    /// <summary>The largest channel difference accepted for solid areas.</summary>
    private const int Tolerance = 6;

    /// <summary>The largest channel difference accepted for gradients.</summary>
    private const int GradientTolerance = 24;

    /// <summary>A coordinate near the low end of the page.</summary>
    private const int Low = 5;

    /// <summary>A coordinate near the high end of the page.</summary>
    private const int High = 95;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The red-to-blue axial function.</summary>
    private const string RedToBlue = "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>";

    /// <summary>The device row of the pattern cell's painted square, with y flipped.</summary>
    private const int CellRow = 95;

    /// <summary>The device column in the second repeat of the pattern cell.</summary>
    private const int SecondCellColumn = 25;

    /// <summary>The device column in the unpainted part of the pattern cell.</summary>
    private const int EmptyCellColumn = 15;

    /// <summary>The expected half-strength channel in the middle of a gradient.</summary>
    private const int HalfChannel = 128;

    /// <summary>The expected red of a point three quarters across a red-green function shading.</summary>
    private const int ThreeQuarterChannel = 191;

    /// <summary>The expected channel a quarter of the way along.</summary>
    private const int QuarterChannel = 64;

    /// <summary>The device row of the point a quarter up the page.</summary>
    private const int QuarterRow = 75;

    /// <summary>The device column of the point three quarters across the page.</summary>
    private const int ThreeQuarterColumn = 75;

    /// <summary>An axial shading runs from red at the left to blue at the right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialShadingBlendsAlongTheAxis()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        pdf.Resources = $"/Shading << /S1 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] {RedToBlue} /Extend [true true] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Low, Middle, new(Rgb.Red255.Red - Low, 0, Low), GradientTolerance)).IsTrue();
        await Assert.That(image.IsNear(Middle, Middle, new(HalfChannel, 0, HalfChannel), GradientTolerance)).IsTrue();
        await Assert.That(image.IsNear(High, Middle, new(Low, 0, Rgb.Blue255.Blue - Low), GradientTolerance)).IsTrue();
    }

    /// <summary>A radial shading without extension leaves the area outside its circle unpainted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadialShadingStopsAtItsCircle()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        pdf.Resources = $"/Shading << /S1 << /ShadingType 3 /ColorSpace /DeviceRGB /Coords [50 50 0 50 50 50] {RedToBlue} >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.Red255, GradientTolerance)).IsTrue();
        await Assert.That(image.IsNear(1, 1, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>A function-based shading evaluates its function for each point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FunctionShadingEvaluatesItsFunction()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        var function = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1] /Range [0 1 0 1 0 1]", "{ 0 }");
        pdf.Resources = $"/Shading << /S1 << /ShadingType 1 /ColorSpace /DeviceRGB /Domain [0 1 0 1] /Matrix [100 0 0 100 0 0] /Function {function} 0 R >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(ThreeQuarterColumn, QuarterRow, new(ThreeQuarterChannel, QuarterChannel, 0), GradientTolerance)).IsTrue();
    }

    /// <summary>A Gouraud triangle mesh blends its vertex colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshShadingBlendsVertexColors()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        var mesh = pdf.AddStream(
            "/ShadingType 4 /ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [0 100 0 100 0 1 0 1 0 1] /Filter /ASCIIHexDecode",
            "00 00 00 FF 00 00  00 FF 00 00 FF 00  00 00 FF 00 00 FF>");
        pdf.Resources = $"/Shading << /S1 {mesh} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Low, High, Rgb.Red255, MeshTolerance)).IsTrue();
        await Assert.That(image.IsNear(Middle, Middle, Rgb.White, Tolerance)).IsFalse();
        await Assert.That(image.IsNear(High, Low, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>A coons patch mesh is drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CoonsPatchIsDrawn()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        var mesh = pdf.AddStream(
            "/ShadingType 6 /ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [0 100 0 100 0 1 0 1 0 1] /Filter /ASCIIHexDecode",
            "00 00 00 00 55 00 AA 00 FF 55 FF AA FF FF FF FF AA FF 55 FF 00 AA 00 55 00 00 FF 00 00 FF 00 00 00 FF FF FF FF>");
        pdf.Resources = $"/Shading << /S1 {mesh} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.White, Tolerance)).IsFalse();
    }

    /// <summary>A tiling pattern repeats its cell by the step.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TilingPatternRepeatsItsCell()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern cs /P1 scn 0 0 100 100 re f" };
        var pattern = pdf.AddStream(
            "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 20 20] /XStep 20 /YStep 20 /Resources << >>",
            "1 0 0 rg 0 0 10 10 re f");
        pdf.Resources = $"/Pattern << /P1 {pattern} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Low, CellRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(EmptyCellColumn, CellRow, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(SecondCellColumn, CellRow, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>An uncoloured tiling pattern takes the colour that selected it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UncoloredPatternTakesTheSelectedColor()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/CsP cs 0 0 1 /P1 scn 0 0 100 100 re f" };
        var pattern = pdf.AddStream(
            "/Type /Pattern /PatternType 1 /PaintType 2 /TilingType 1 /BBox [0 0 20 20] /XStep 20 /YStep 20 /Resources << >>",
            "0 1 0 rg 0 0 10 10 re f");
        pdf.Resources = $"/Pattern << /P1 {pattern} 0 R >> /ColorSpace << /CsP [/Pattern /DeviceRGB] >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Low, CellRow, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>A shading pattern fills a path with a gradient.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingPatternFillsAPath()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern cs /P1 scn 0 0 50 100 re f" };
        var pattern = pdf.AddObject($"<< /Type /Pattern /PatternType 2 /Shading << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] {RedToBlue} /Extend [true true] >> >>");
        pdf.Resources = $"/Pattern << /P1 {pattern} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Low, Middle, new(Rgb.Red255.Red - Low, 0, Low), GradientTolerance)).IsTrue();
        await Assert.That(image.IsNear(High, Middle, Rgb.White, Tolerance)).IsTrue();
    }
}

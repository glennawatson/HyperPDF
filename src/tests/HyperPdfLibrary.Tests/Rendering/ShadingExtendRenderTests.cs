// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders mixed /Extend, /Background, mesh shadings used to stroke, and adaptive patch meshes.</summary>
public sealed class ShadingExtendRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A column before the gradient starts.</summary>
    private const int Before = 10;

    /// <summary>A column after the gradient ends.</summary>
    private const int After = 90;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>A row away from the stroked line.</summary>
    private const int OffLine = 20;

    /// <summary>A small scale for the mesh cache test.</summary>
    private const float SmallScale = 1;

    /// <summary>A scale in the same power-of-two bucket as <see cref="LargeScale"/>.</summary>
    private const float NearLargeScale = 12;

    /// <summary>A large scale for the mesh cache test.</summary>
    private const float LargeScale = 16;

    /// <summary>Content that paints the shading named S1.</summary>
    private const string PaintShading = "/S1 sh";

    /// <summary>The red-to-blue function.</summary>
    private const string RedToBlue = "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>";

    /// <summary>Two triangles covering the page in green: flag, x, y and RGB per vertex, eight bits each.</summary>
    private const string GreenTriangles = "00 00 00 00FF00 00 FF 00 00FF00 00 00 FF 00FF00 00 FF 00 00FF00 00 FF FF 00FF00 00 00 FF 00FF00>";

    /// <summary>A Coons patch over the page with red, green, blue and yellow corners.</summary>
    private const string CoonsPatch = "00 0000 0055 00AA 00FF 55FF AAFF FFFF FFAA FF55 FF00 AA00 5500 FF0000 00FF00 0000FF FFFF00>";

    /// <summary>The entries of the mesh shadings.</summary>
    private const string MeshEntries = "/ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [0 100 0 100 0 1 0 1 0 1] /Filter /ASCIIHexDecode";

    /// <summary>An axial shading extended only at its start paints before the axis and nothing after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialExtendsOnlyItsStart()
    {
        var image = RenderShading("/Extend [true false]");

        await RenderCheck.Near(image, Before, Middle, Rgb.Red255, Tolerance, nameof(AxialExtendsOnlyItsStart));
        await RenderCheck.Near(image, After, Middle, Rgb.White, Tolerance, nameof(AxialExtendsOnlyItsStart));
    }

    /// <summary>An axial shading extended only at its end paints after the axis and nothing before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialExtendsOnlyItsEnd()
    {
        var image = RenderShading("/Extend [false true]");

        await RenderCheck.Near(image, Before, Middle, Rgb.White, Tolerance, nameof(AxialExtendsOnlyItsEnd));
        await RenderCheck.Near(image, After, Middle, Rgb.Blue255, Tolerance, nameof(AxialExtendsOnlyItsEnd));
    }

    /// <summary>A radial shading extended only at its end paints outside its outer circle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadialExtendsOnlyItsEnd()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        pdf.Resources = $"/Shading << /S1 << /ShadingType 3 /ColorSpace /DeviceRGB /Coords [50 50 10 50 50 30] {RedToBlue} /Extend [false true] >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, Middle, Middle, Rgb.White, Tolerance, nameof(RadialExtendsOnlyItsEnd));
        await RenderCheck.Near(image, Before, Before, Rgb.Blue255, Tolerance, nameof(RadialExtendsOnlyItsEnd));
    }

    /// <summary>The sh operator ignores /Background, as PDFium and the specification do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingOperatorIgnoresTheBackground()
    {
        var image = RenderShading("/Extend [false false] /Background [0 1 0]");

        await RenderCheck.Near(image, Before, Middle, Rgb.White, Tolerance, nameof(ShadingOperatorIgnoresTheBackground));
    }

    /// <summary>A shading pattern fill paints /Background where the shading itself paints nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingPatternFillPaintsTheBackground()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern cs /P1 scn 0 0 100 100 re f" };
        pdf.Resources = $"/Pattern << /P1 << /PatternType 2 /Shading << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [25 0 75 0] {RedToBlue} /Background [0 1 0] >> >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, Before, Middle, Rgb.Green255, Tolerance, nameof(ShadingPatternFillPaintsTheBackground));
    }

    /// <summary>A mesh shading pattern used as the stroke colour paints the stroke.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshShadingPatternPaintsAStroke()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern CS /P1 SCN 10 w 10 50 m 90 50 l S" };
        var mesh = pdf.AddStream($"/ShadingType 4 {MeshEntries}", GreenTriangles);
        pdf.Resources = $"/Pattern << /P1 << /PatternType 2 /Shading {mesh} 0 R >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.Near(image, Middle, Middle, Rgb.Green255, Tolerance, nameof(MeshShadingPatternPaintsAStroke));
        await RenderCheck.Near(image, Middle, OffLine, Rgb.White, Tolerance, nameof(MeshShadingPatternPaintsAStroke));
    }

    /// <summary>A patch mesh is cut per power-of-two scale and reused within one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatchMeshesAreKeptPerScale()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        var patch = pdf.AddStream($"/ShadingType 6 {MeshEntries}", CoonsPatch);
        pdf.Resources = $"/Shading << /S1 {patch} 0 R >>";
        using var document = SkiaTestRegistration.Open(pdf.ToBytes(), null);
        var value = PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Shading)!.Get(document.Objects.Names.Intern("S1"));
        var shading = PdfShading.Parse(value, null)!;

        var small = shading.GetMesh(SmallScale);
        var large = shading.GetMesh(LargeScale);
        var nearLarge = shading.GetMesh(NearLargeScale);

        await Assert.That(small).IsNotNull();
        await Assert.That(ReferenceEquals(small, large)).IsFalse();
        await Assert.That(ReferenceEquals(large, nearLarge)).IsTrue();
    }

    /// <summary>A Coons patch paints its corner colours, cut finely enough that its middle blends them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CoonsPatchPaintsThePage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        var patch = pdf.AddStream($"/ShadingType 6 {MeshEntries}", CoonsPatch);
        pdf.Resources = $"/Shading << /S1 {patch} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await RenderCheck.NotNear(image, Middle, Middle, Rgb.White, Tolerance, nameof(CoonsPatchPaintsThePage));
    }

    /// <summary>Renders an axial red-to-blue shading from x 25 to 75 with the given entries.</summary>
    /// <param name="entries">The /Extend and other entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderShading(string entries)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PaintShading };
        pdf.Resources = $"/Shading << /S1 << /ShadingType 2 /ColorSpace /DeviceRGB /Coords [25 0 75 0] {RedToBlue} {entries} >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }
}

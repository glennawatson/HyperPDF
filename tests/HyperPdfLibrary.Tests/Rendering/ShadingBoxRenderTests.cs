// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Shadings;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Renders the shading /BBox clip and the /AntiAlias flag on axial, radial and mesh shadings, painted with <c>sh</c>, as a
/// pattern fill and as a pattern stroke (ISO 32000-2 8.7.4.3 Table 77, 8.7.4.5). PDFium clips <c>sh</c> to /BBox and
/// ignores /AntiAlias.
/// </summary>
public sealed class ShadingBoxRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A coordinate inside the box.</summary>
    private const int Inside = 50;

    /// <summary>A coordinate left of or below the box.</summary>
    private const int Outside = 10;

    /// <summary>A coordinate right of or above the box.</summary>
    private const int FarOutside = 90;

    /// <summary>The largest channel difference /AntiAlias dithering may make.</summary>
    private const int DitherLimit = 3;

    /// <summary>The device row of the stroked line in the stroke tests, with y flipped.</summary>
    private const int LineRow = 50;

    /// <summary>The box the tests clip to, as /BBox entries.</summary>
    private const string Box = "/BBox [20 20 80 80]";

    /// <summary>A solid red-to-red function, so every painted point is red.</summary>
    private const string Red = "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [1 0 0] /N 1 >>";

    /// <summary>A function between two close greys, which sits between 8-bit levels across the page.</summary>
    private const string FaintRamp = "/Function << /FunctionType 2 /Domain [0 1] /C0 [0.4 0.4 0.4] /C1 [0.45 0.45 0.45] /N 1 >>";

    /// <summary>The entries of the mesh shadings.</summary>
    private const string MeshEntries = "/ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [0 100 0 100 0 1 0 1 0 1] /Filter /ASCIIHexDecode";

    /// <summary>Two triangles covering the page in green: flag, x, y and RGB per vertex, eight bits each.</summary>
    private const string GreenTriangles = "00 00 00 00FF00 00 FF 00 00FF00 00 00 FF 00FF00 00 FF 00 00FF00 00 FF FF 00FF00 00 00 FF 00FF00>";

    /// <summary>A Coons patch over the page with slowly varying greys, so dithering has levels to spread.</summary>
    private const string FaintPatch = "00 0000 0055 00AA 00FF 55FF AAFF FFFF FFAA FF55 FF00 AA00 5500 666666 686868 6A6A6A 6C6C6C>";

    /// <summary>The content that paints the shading S1 with the operator.</summary>
    private const string Operator = "/S1 sh";

    /// <summary>The content that fills the page with a shading pattern.</summary>
    private const string PatternFill = "/Pattern cs /P1 scn 0 0 100 100 re f";

    /// <summary>The axial shading painted with <c>sh</c> shows only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialShadingOperatorClipsToItsBox()
    {
        var image = Render(Operator, $"/Shading << /S1 {Axial(Box)} >>");

        await CheckBox(image, nameof(AxialShadingOperatorClipsToItsBox));
    }

    /// <summary>The radial shading painted with <c>sh</c> shows only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadialShadingOperatorClipsToItsBox()
    {
        var image = Render(Operator, $"/Shading << /S1 {Radial(Box)} >>");

        await CheckBox(image, nameof(RadialShadingOperatorClipsToItsBox));
    }

    /// <summary>The mesh shading painted with <c>sh</c> shows only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshShadingOperatorClipsToItsBox()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = Operator };
        var mesh = pdf.AddStream($"/ShadingType 4 {Box} {MeshEntries}", GreenTriangles);
        pdf.Resources = $"/Shading << /S1 {mesh} 0 R >>";

        await CheckBox(Render(pdf), nameof(MeshShadingOperatorClipsToItsBox), Rgb.Green255);
    }

    /// <summary>A pattern fill with an axial shading shows the shading only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialShadingPatternFillClipsToItsBox()
    {
        var image = Render(PatternFill, $"/Pattern << /P1 << /PatternType 2 /Shading {Axial(Box)} >> >>");

        await CheckBox(image, nameof(AxialShadingPatternFillClipsToItsBox));
    }

    /// <summary>A pattern fill with a radial shading shows the shading only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadialShadingPatternFillClipsToItsBox()
    {
        var image = Render(PatternFill, $"/Pattern << /P1 << /PatternType 2 /Shading {Radial(Box)} >> >>");

        await CheckBox(image, nameof(RadialShadingPatternFillClipsToItsBox));
    }

    /// <summary>A pattern fill with a mesh shading shows the shading only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshShadingPatternFillClipsToItsBox()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PatternFill };
        var mesh = pdf.AddStream($"/ShadingType 4 {Box} {MeshEntries}", GreenTriangles);
        pdf.Resources = $"/Pattern << /P1 << /PatternType 2 /Shading {mesh} 0 R >> >>";

        await CheckBox(Render(pdf), nameof(MeshShadingPatternFillClipsToItsBox), Rgb.Green255);
    }

    /// <summary>The pattern's /Matrix moves the box with the shading, because /BBox is in pattern space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingBoxFollowsThePatternMatrix()
    {
        var image = Render(PatternFill, $"/Pattern << /P1 << /PatternType 2 /Matrix [1 0 0 1 20 0] /Shading {Axial(Box)} >> >>");

        await RenderCheck.Near(image, Inside, Inside, Rgb.Red255, Tolerance, nameof(ShadingBoxFollowsThePatternMatrix));
        await RenderCheck.Near(image, Outside + Outside, Inside, Rgb.White, Tolerance, nameof(ShadingBoxFollowsThePatternMatrix));
        await RenderCheck.Near(image, FarOutside, Inside, Rgb.Red255, Tolerance, nameof(ShadingBoxFollowsThePatternMatrix));
    }

    /// <summary>The background fills the whole shape and is not clipped by /BBox; the shading paints over it inside the box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackgroundIsNotClippedByTheBox()
    {
        var image = Render(PatternFill, $"/Pattern << /P1 << /PatternType 2 /Shading {Axial($"{Box} /Background [0 1 0]")} >> >>");

        await RenderCheck.Near(image, Outside, Inside, Rgb.Green255, Tolerance, nameof(BackgroundIsNotClippedByTheBox));
        await RenderCheck.Near(image, Inside, Inside, Rgb.Red255, Tolerance, nameof(BackgroundIsNotClippedByTheBox));
    }

    /// <summary>A mesh shading pattern paints its /Background where the mesh paints nothing, as an axial one does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshShadingPatternPaintsTheBackground()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = PatternFill };
        var mesh = pdf.AddStream($"/ShadingType 4 {Box} /Background [0 0 1] {MeshEntries}", GreenTriangles);
        pdf.Resources = $"/Pattern << /P1 << /PatternType 2 /Shading {mesh} 0 R >> >>";

        var image = Render(pdf);

        await RenderCheck.Near(image, Outside, Inside, Rgb.Blue255, Tolerance, nameof(MeshShadingPatternPaintsTheBackground));
        await RenderCheck.Near(image, Inside, Inside, Rgb.Green255, Tolerance, nameof(MeshShadingPatternPaintsTheBackground));
    }

    /// <summary>A stroke painted with a shading pattern shows the shading only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialShadingPatternStrokeClipsToItsBox()
    {
        var image = Render("/Pattern CS /P1 SCN 10 w 5 50 m 95 50 l S", $"/Pattern << /P1 << /PatternType 2 /Shading {Axial(Box)} >> >>");

        await RenderCheck.Near(image, Inside, LineRow, Rgb.Red255, Tolerance, nameof(AxialShadingPatternStrokeClipsToItsBox));
        await RenderCheck.Near(image, Outside, LineRow, Rgb.White, Tolerance, nameof(AxialShadingPatternStrokeClipsToItsBox));
        await RenderCheck.Near(image, FarOutside, LineRow, Rgb.White, Tolerance, nameof(AxialShadingPatternStrokeClipsToItsBox));
    }

    /// <summary>A stroke painted with a mesh shading pattern shows the mesh only inside /BBox.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshShadingPatternStrokeClipsToItsBox()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Pattern CS /P1 SCN 10 w 5 50 m 95 50 l S" };
        var mesh = pdf.AddStream($"/ShadingType 4 {Box} {MeshEntries}", GreenTriangles);
        pdf.Resources = $"/Pattern << /P1 << /PatternType 2 /Shading {mesh} 0 R >> >>";

        var image = Render(pdf);

        await RenderCheck.Near(image, Inside, LineRow, Rgb.Green255, Tolerance, nameof(MeshShadingPatternStrokeClipsToItsBox));
        await RenderCheck.Near(image, Outside, LineRow, Rgb.White, Tolerance, nameof(MeshShadingPatternStrokeClipsToItsBox));
    }

    /// <summary>/AntiAlias is read from the dictionary and defaults to false.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AntiAliasIsReadFromTheDictionary()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = Operator };
        pdf.Resources = $"/Shading << /S1 {Axial("/AntiAlias true")} /S2 {Axial(string.Empty)} >>";
        using var document = SkiaTestRegistration.Open(pdf.ToBytes(), null);
        var shadings = HyperPdfLibrary.Document.PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(HyperPdfLibrary.Objects.KnownName.Shading)!;

        var on = PdfShading.Parse(shadings.Get(document.Objects.Names.Intern("S1")), null)!;
        var off = PdfShading.Parse(shadings.Get(document.Objects.Names.Intern("S2")), null)!;

        await Assert.That(on.AntiAlias).IsTrue();
        await Assert.That(off.AntiAlias).IsFalse();
    }

    /// <summary>/AntiAlias on an axial shading dithers the colour ramp by at most a few levels and leaves the geometry alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AxialAntiAliasDithersTheRamp()
    {
        var plain = Render(Operator, $"/Shading << /S1 {Axial(Box, FaintRamp)} >>");
        var filtered = Render(Operator, $"/Shading << /S1 {Axial($"{Box} /AntiAlias true", FaintRamp)} >>");

        await CheckDithered(plain, filtered);
    }

    /// <summary>/AntiAlias on a radial shading dithers the colour ramp by at most a few levels and leaves the geometry alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadialAntiAliasDithersTheRamp()
    {
        var plain = Render(Operator, $"/Shading << /S1 {Radial($"{Box} /Extend [true true]", FaintRamp)} >>");
        var filtered = Render(Operator, $"/Shading << /S1 {Radial($"{Box} /Extend [true true] /AntiAlias true", FaintRamp)} >>");

        await CheckDithered(plain, filtered);
    }

    /// <summary>/AntiAlias on a patch mesh dithers the colours by at most a few levels and leaves the geometry alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeshAntiAliasDithersTheColours()
    {
        var plain = RenderPatch(string.Empty);
        var filtered = RenderPatch("/AntiAlias true");

        await CheckDithered(plain, filtered);
    }

    /// <summary>Checks the box clip on a page painted red (or another colour) inside [20, 80].</summary>
    /// <param name="image">The pixels.</param>
    /// <param name="name">The test name.</param>
    /// <returns>A task.</returns>
    private static Task CheckBox(RenderedImage image, string name) => CheckBox(image, name, Rgb.Red255);

    /// <summary>Checks the box clip.</summary>
    /// <param name="image">The pixels.</param>
    /// <param name="name">The test name.</param>
    /// <param name="inside">The colour expected inside the box.</param>
    /// <returns>A task.</returns>
    private static async Task CheckBox(RenderedImage image, string name, Rgb inside)
    {
        await RenderCheck.Near(image, Inside, Inside, inside, Tolerance, name);
        await RenderCheck.Near(image, Outside, Inside, Rgb.White, Tolerance, name);
        await RenderCheck.Near(image, FarOutside, Inside, Rgb.White, Tolerance, name);
        await RenderCheck.Near(image, Inside, Outside, Rgb.White, Tolerance, name);
        await RenderCheck.Near(image, Inside, FarOutside, Rgb.White, Tolerance, name);
    }

    /// <summary>Checks that two renders differ only by dithering: some pixels change, none by more than a few levels, and none outside the shading.</summary>
    /// <param name="plain">The render without /AntiAlias.</param>
    /// <param name="filtered">The render with it.</param>
    /// <returns>A task.</returns>
    private static async Task CheckDithered(RenderedImage plain, RenderedImage filtered)
    {
        var changed = 0;
        var largest = 0;
        for (var i = 0; i < plain.Pixels.Length; i++)
        {
            var difference = Math.Abs(plain.Pixels[i] - filtered.Pixels[i]);
            largest = Math.Max(largest, difference);
            changed += difference > 0 ? 1 : 0;
        }

        await Assert.That(changed).IsGreaterThan(0);
        await Assert.That(largest).IsLessThanOrEqualTo(DitherLimit);
        await RenderCheck.Near(filtered, Outside, Outside, Rgb.White, 0, nameof(CheckDithered));
    }

    /// <summary>Gets an axial shading dictionary, red at both ends unless a function is given.</summary>
    /// <param name="entries">Extra entries, such as /BBox.</param>
    /// <returns>The dictionary text.</returns>
    private static string Axial(string entries) => Axial(entries, Red);

    /// <summary>Gets an axial shading dictionary that extends both ways.</summary>
    /// <param name="entries">Extra entries, such as /BBox.</param>
    /// <param name="function">The /Function entry.</param>
    /// <returns>The dictionary text.</returns>
    private static string Axial(string entries, string function) =>
        $"<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [0 0 100 0] {function} /Extend [true true] {entries} >>";

    /// <summary>Gets a radial shading dictionary that extends both ways.</summary>
    /// <param name="entries">Extra entries, such as /BBox.</param>
    /// <returns>The dictionary text.</returns>
    private static string Radial(string entries) => Radial($"/Extend [true true] {entries}", Red);

    /// <summary>Gets a radial shading dictionary.</summary>
    /// <param name="entries">Extra entries, such as /BBox and /Extend.</param>
    /// <param name="function">The /Function entry.</param>
    /// <returns>The dictionary text.</returns>
    private static string Radial(string entries, string function) =>
        $"<< /ShadingType 3 /ColorSpace /DeviceRGB /Coords [50 50 0 50 50 100] {function} {entries} >>";

    /// <summary>Renders a page of content with resources.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="resources">The page resources.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, string resources) =>
        Render(new(Size, Size) { Content = content, Resources = resources });

    /// <summary>Renders a prepared document's first page.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(RenderTestPdf pdf)
    {
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }

    /// <summary>Renders the faint Coons patch with extra shading entries.</summary>
    /// <param name="entries">The extra entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderPatch(string entries)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = Operator };
        var patch = pdf.AddStream($"/ShadingType 6 {Box} {entries} {MeshEntries}", FaintPatch);
        pdf.Resources = $"/Shading << /S1 {patch} 0 R >>";
        return Render(pdf);
    }
}

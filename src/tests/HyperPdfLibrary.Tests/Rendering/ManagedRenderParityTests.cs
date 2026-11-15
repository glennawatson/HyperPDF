// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Render.Managed;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that the managed rasteriser draws pages the way the Skia backend does, within the small differences of two anti-aliasers.</summary>
public sealed class ManagedRenderParityTests
{
    /// <summary>The descriptor flags of the non-symbolic test font.</summary>
    private const int FontFlags = 32;

    /// <summary>The distance between two blend mode samples, in points.</summary>
    private const int BlendColumn = 13;

    /// <summary>The pixels of a point at the scale the pages are drawn at.</summary>
    private const int Scale = 2;

    /// <summary>The largest mean difference of a colour byte between the two backends, out of 255.</summary>
    private const double MeanTolerance = 1.5;

    /// <summary>The smallest share of pixels that must not be paper, so a blank page cannot pass.</summary>
    private const double MinimumInk = 0.01;

    /// <summary>Text, a filled rectangle, a stroked line with round ends, a dashed curve and a clipped fill are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAndVectorsMatchSkia()
    {
        const string content = """
            BT /F1 24 Tf 20 60 Td (Hello) Tj ET
            0.2 0.4 0.8 rg 20 10 60 30 re f
            0.8 0.1 0.1 RG 4 w 1 J 100 15 m 180 45 l S
            0 0.5 0 RG 2 w [6 3] 0 d 100 80 m 130 95 160 65 190 80 c S
            q 120 50 40 20 re W n 0.9 0.6 0 rg 110 40 m 170 40 l 140 80 l h f Q
            """;
        var outcome = Compare(FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = FontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = content }));

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>A gradient along a line, one between two circles and a shape filled with a shading pattern are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GradientsMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        var function = pdf.AddObject("<< /FunctionType 2 /Domain [0 1] /C0 [1 0 0] /C1 [0 0 1] /N 1 >>");
        var axial = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /ShadingType 2 /ColorSpace /DeviceRGB /Coords [10 0 90 0] /Function {function} 0 R /Extend [true true] >>"));
        var radial = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /ShadingType 3 /ColorSpace /DeviceRGB /Coords [150 50 5 150 50 40] /Function {function} 0 R >>"));
        var pattern = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /PatternType 2 /Shading {axial} 0 R >>"));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Shading << /S1 {axial} 0 R /S2 {radial} 0 R >> /Pattern << /P1 {pattern} 0 R >>");
        pdf.Content = "q 10 10 80 30 re W n /S1 sh Q q 110 10 80 80 re W n /S2 sh Q /Pattern cs /P1 scn 20 50 m 80 50 l 50 95 l h f";
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>A shape filled with a tiling pattern, and a group drawn half transparent, are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TilingPatternsAndGroupsMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        var cell = pdf.AddStream(
            "/Type /Pattern /PatternType 1 /PaintType 1 /TilingType 1 /BBox [0 0 10 10] /XStep 10 /YStep 10 /Resources << >>",
            "0.9 0.2 0.2 rg 0 0 5 5 re f 0.2 0.2 0.9 rg 5 5 5 5 re f");
        var group = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 80 80] /Group << /S /Transparency /I true >>",
            "0 0.6 0 rg 0 0 50 50 re f 0.6 0 0.6 rg 30 30 50 50 re f");
        var state = pdf.AddObject("<< /Type /ExtGState /ca 0.5 >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Pattern << /P1 {cell} 0 R >> /XObject << /G1 {group} 0 R >> /ExtGState << /GS1 {state} 0 R >>");
        pdf.Content = "/Pattern cs /P1 scn 10 10 80 80 re f 0.3 0.3 0.3 rg 105 20 90 20 re f q /GS1 gs 1 0 0 1 110 10 cm /G1 Do Q";
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>Shapes drawn with blend modes that work on each channel, and with ones that work on hue and brightness, are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BlendModesMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        string[] modes =
        [
            "Multiply", "Screen", "Overlay", "Darken", "Lighten", "ColorDodge", "ColorBurn", "HardLight", "SoftLight", "Difference", "Exclusion",
            "Hue", "Saturation", "Color", "Luminosity",
        ];
        var states = new StringBuilder();
        var content = new StringBuilder("0.8 0.5 0.2 rg 0 20 200 30 re f 0.2 0.5 0.8 rg 0 50 200 30 re f ");
        for (var i = 0; i < modes.Length; i++)
        {
            var state = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /ExtGState /BM /{modes[i]} >>"));
            _ = states.Append(CultureInfo.InvariantCulture, $"/B{i} {state} 0 R ");
            _ = content.Append(CultureInfo.InvariantCulture, $"q /B{i} gs 0.9 0.3 0.6 rg {(i * BlendColumn) + 1} 10 10 80 re f Q");
        }

        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << {states} >>");
        pdf.Content = content.ToString();
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>A colour image, a gray image and a stencil mask, each stretched and one rotated, are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagesMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        byte[] colors = [255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0, 0, 255, 255, 255, 0, 255, 40, 40, 40, 200, 200, 200, 255, 128, 0];
        byte[] grays = [0, 64, 128, 192, 255, 32, 96, 160, 224];
        byte[] mask = [0b1010_0000, 0b0100_0000, 0b1010_0000];
        var color = pdf.AddStream("/Type /XObject /Subtype /Image /Width 3 /Height 3 /ColorSpace /DeviceRGB /BitsPerComponent 8", colors);
        var gray = pdf.AddStream("/Type /XObject /Subtype /Image /Width 3 /Height 3 /ColorSpace /DeviceGray /BitsPerComponent 8", grays);
        var stencil = pdf.AddStream("/Type /XObject /Subtype /Image /Width 3 /Height 3 /ImageMask true /BitsPerComponent 1", mask);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /I1 {color} 0 R /I2 {gray} 0 R /I3 {stencil} 0 R >>");
        pdf.Content = "q 60 0 0 60 10 20 cm /I1 Do Q q 40 20 -20 40 100 20 cm /I2 Do Q 0.1 0.4 0.7 rg q 45 0 0 45 150 30 cm /I3 Do Q";
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>A shape faded by a luminosity soft mask, and one faded by an alpha soft mask, are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMasksMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        var mask = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 200 100] /Group << /S /Transparency /CS /DeviceGray >>",
            "0.2 g 0 0 60 100 re f 0.9 g 60 0 60 100 re f 0.5 g 120 0 80 50 re f");
        var bright = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /ExtGState /SMask << /Type /Mask /S /Luminosity /G {mask} 0 R >> >>"));
        var alpha = pdf.AddObject(string.Create(CultureInfo.InvariantCulture, $"<< /Type /ExtGState /SMask << /Type /Mask /S /Alpha /G {mask} 0 R >> >>"));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS1 {bright} 0 R /GS2 {alpha} 0 R >>");
        pdf.Content = "q /GS1 gs 0.8 0.1 0.1 rg 10 55 180 40 re f Q q /GS2 gs 0.1 0.1 0.8 rg 10 5 180 40 re f Q";
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>A shading worked out by a function of position, and a mesh of coloured triangles, are drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FunctionAndMeshShadingsMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        var function = pdf.AddStream(
            "/FunctionType 4 /Domain [0 1 0 1] /Range [0 1 0 1 0 1]",
            "{ 2 copy mul }");
        var sampled = pdf.AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /ShadingType 1 /ColorSpace /DeviceRGB /Domain [0 1 0 1] /Matrix [80 0 0 80 10 10] /Function {function} 0 R >>"));

        // Two triangles of a free-form mesh: a flag byte, two 8-bit coordinates and three 8-bit colour components for each corner.
        byte[] corners =
        [
            0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 255, 0, 0, 0, 255, 0, 0, 255,
            0, 255, 255, 255, 255, 0, 0, 255, 0, 0, 255, 0, 0, 0, 255, 0, 0, 255,
        ];
        var mesh = pdf.AddStream(
            "/ShadingType 4 /ColorSpace /DeviceRGB /BitsPerCoordinate 8 /BitsPerComponent 8 /BitsPerFlag 8 /Decode [110 190 10 90 0 1 0 1 0 1]",
            corners);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Shading << /S1 {sampled} 0 R /S2 {mesh} 0 R >>");
        pdf.Content = "q 10 10 80 80 re W n /S1 sh Q /S2 sh";
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>A half transparent group inside a knockout group replaces what the knockout group drew under it, and is drawn alike.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnockoutGroupsMatchSkia()
    {
        var pdf = new RenderTestPdf(FontTestDocument.PageWidth, FontTestDocument.PageHeight);
        var child = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 200 100] /Group << /S /Transparency /I true >>",
            "0.1 0.2 0.9 rg 60 20 80 60 re f");
        var state = pdf.AddObject("<< /Type /ExtGState /ca 0.5 >>");
        var parent = pdf.AddStream(
            string.Create(
                CultureInfo.InvariantCulture,
                $"/Type /XObject /Subtype /Form /BBox [0 0 200 100] /Group << /S /Transparency /I true /K true >> /Resources << /XObject << /C1 {child} 0 R >> /ExtGState << /GS1 {state} 0 R >> >>"),
            "0.9 0.2 0.1 rg 20 10 100 80 re f q /GS1 gs /C1 Do Q");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /P1 {parent} 0 R >>");
        pdf.Content = "0.3 0.8 0.3 rg 0 40 200 20 re f /P1 Do";
        var outcome = Compare(pdf.ToBytes());

        await Assert.That(outcome.Ink).IsGreaterThan(MinimumInk);
        await Assert.That(outcome.MeanDifference).IsLessThan(MeanTolerance);
    }

    /// <summary>Renders one page with both backends and measures how far apart the pixels are.</summary>
    /// <param name="bytes">The document.</param>
    /// <returns>The mean difference of a colour byte, and the share of pixels the managed backend inked.</returns>
    private static ParityOutcome Compare(byte[] bytes)
    {
        var skia = new PdfRenderBackendRegistration().UseSkia();
        var expected = Render(bytes, skia);
        var actual = Render(bytes, skia.UseManaged());
        long difference = 0;
        var inked = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            difference += Math.Abs(expected[i] - actual[i]);
            inked += actual[i] != byte.MaxValue ? 1 : 0;
        }

        return new((double)difference / expected.Length, (double)inked / expected.Length);
    }

    /// <summary>Renders the first page of a document.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="registration">The drawing services.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(byte[] pdf, PdfRenderBackendRegistration registration)
    {
        using var document = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = PdfFontLoader.Load, DrawingServices = new(registration) });
        using var renderer = new PdfPageRenderer(document);
        const int width = FontTestDocument.PageWidth * Scale;
        const int height = FontTestDocument.PageHeight * Scale;
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel));
        return pixels;
    }

    /// <summary>How two renderings of a page compare.</summary>
    /// <param name="MeanDifference">The mean difference of a colour byte, out of 255.</param>
    /// <param name="Ink">The share of bytes the managed backend drew that are not paper.</param>
    private readonly record struct ParityOutcome(double MeanDifference, double Ink);
}

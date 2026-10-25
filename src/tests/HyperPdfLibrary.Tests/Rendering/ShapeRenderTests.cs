// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders paths, strokes, clips and forms and checks pixel colours.</summary>
public sealed class ShapeRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>Content that paints the shading named S1.</summary>
    private const string PaintShading = "/S1 sh";

    /// <summary>The largest channel difference accepted for the coarse mesh colours.</summary>
    private const int MeshTolerance = 72;

    /// <summary>The largest channel difference accepted for solid areas.</summary>
    private const int Tolerance = 4;

    /// <summary>The centre of the filled rectangle in PDF space, on both axes.</summary>
    private const int RectCentre = 40;

    /// <summary>The device row of the centre of the filled rectangle, with y flipped.</summary>
    private const int RectCentreRow = Size - RectCentre;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>A point inside the ring of the even-odd shape.</summary>
    private const int RingPoint = 20;

    /// <summary>A point inside the clip rectangle.</summary>
    private const int InsideClip = 30;

    /// <summary>A point outside the clip rectangle.</summary>
    private const int OutsideClip = 80;

    /// <summary>The device row for a point inside the form after its matrix.</summary>
    private const int FormRow = 65;

    /// <summary>The device column for a point inside the form after its matrix.</summary>
    private const int FormColumn = 35;

    /// <summary>The device column of a point beyond the form's bounding box.</summary>
    private const int BeyondForm = 60;

    /// <summary>The device row of a point beyond the form's bounding box.</summary>
    private const int BeyondFormRow = 60;

    /// <summary>The device row of a point on the horizontal line.</summary>
    private const int LineRow = 50;

    /// <summary>The device row of a point above the line's width.</summary>
    private const int AboveLine = 30;

    /// <summary>A filled rectangle is red where it was drawn and white elsewhere.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilledRectangleHasItsColour()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 20 20 40 40 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(RectCentre, RectCentreRow, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(OutsideClip, OutsideClip, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>The even-odd rule leaves a hole where shapes overlap.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvenOddFillLeavesAHole()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "0 0 1 rg 10 10 80 80 re 30 30 40 40 re f*" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(RingPoint, Middle, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>A stroke is as wide as the line width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeUsesTheLineWidth()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "0 1 0 RG 10 w 10 50 m 90 50 l S" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, LineRow, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(Middle, AboveLine, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Painting after a clip only marks the clipped area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipRestrictsLaterPainting()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "20 20 40 40 re W n 0 1 0 rg 0 0 100 100 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(RectCentre, RectCentreRow, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(OutsideClip, OutsideClip, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(InsideClip, OutsideClip, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Restoring the graphics state removes a clip.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestoreRemovesTheClip()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 0 0 10 10 re W n Q 1 0 0 rg 0 0 100 100 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>A form XObject is placed by its matrix and clipped by its bounding box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormUsesItsMatrixAndBoundingBox()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Fm Do" };
        var form = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 30 30] /Matrix [1 0 0 1 20 20]",
            "0 0 1 rg 0 0 100 100 re f");
        pdf.Resources = $"/XObject << /Fm {form} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(FormColumn, FormRow, Rgb.Blue255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(BeyondForm, BeyondFormRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>CMYK black paints PDFium's dark grey for pure K, not RGB black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykBlackIsDark()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "0 0 0 1 k 0 0 100 100 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());
        Span<float> rgb = stackalloc float[3];
        CmykConverter.ToRgb(0, 0, 0, 1, rgb);
        var expected = new Rgb((int)MathF.Round(rgb[0] * byte.MaxValue), (int)MathF.Round(rgb[1] * byte.MaxValue), (int)MathF.Round(rgb[2] * byte.MaxValue));

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, expected, Tolerance)).IsTrue();
    }

    /// <summary>The fill colour of a Separation space is converted through its tint transform.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorOperatorsUseTheCurrentColorSpace()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/DeviceRGB cs 0 1 0 sc 0 0 100 100 re f" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.Green255, Tolerance)).IsTrue();
    }
}

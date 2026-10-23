// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders constant alpha, blend modes, groups and soft masks.</summary>
public sealed class TransparencyRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 6;

    /// <summary>A coordinate in the left half of the page.</summary>
    private const int Left = 25;

    /// <summary>A coordinate in the right half of the page.</summary>
    private const int Right = 75;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>Red over white at half alpha: the other channels reach half.</summary>
    private const int HalfChannel = 128;

    /// <summary>The device column and row of the second square in the group test.</summary>
    private const int OverlapPoint = 45;

    /// <summary>A coordinate outside both squares of the group test.</summary>
    private const int OutsideGroup = 90;

    /// <summary>Constant fill alpha mixes the colour with the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillAlphaMixesWithTheBackground()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs 1 0 0 rg 0 0 100 100 re f" };
        var state = pdf.AddObject("<< /Type /ExtGState /ca 0.5 >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, new(Rgb.Red255.Red, HalfChannel, HalfChannel), Tolerance)).IsTrue();
    }

    /// <summary>Stroke alpha is separate from fill alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeAlphaDoesNotChangeTheFill()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs 0 0 1 rg 0 0 100 100 re f" };
        var state = pdf.AddObject("<< /Type /ExtGState /CA 0.5 >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>The Multiply blend mode multiplies the colour with the backdrop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultiplyBlendsWithTheBackdrop()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 1 0 rg 0 0 100 100 re f /GS gs 0 1 1 rg 0 0 100 100 re f" };
        var state = pdf.AddObject("<< /Type /ExtGState /BM /Multiply >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, Rgb.Green255, Tolerance)).IsTrue();
    }

    /// <summary>A transparency group is composited as one object, so overlapping parts do not show through each other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupIsCompositedAsOneObject()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs /Fm Do" };
        var state = pdf.AddObject("<< /Type /ExtGState /ca 0.5 >>");
        var form = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceRGB >>",
            "1 0 0 rg 10 10 50 50 re f 30 30 50 50 re f");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >> /XObject << /Fm {form} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        var mixed = new Rgb(Rgb.Red255.Red, HalfChannel, HalfChannel);
        await Assert.That(image.IsNear(Left, Right, mixed, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(OverlapPoint, OverlapPoint, mixed, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(OutsideGroup, OutsideGroup, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>A luminosity soft mask shows content only where its group is bright.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LuminositySoftMaskHidesDarkAreas()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs 1 0 0 rg 0 0 100 100 re f" };
        var group = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceGray >>",
            "1 g 0 0 50 100 re f");
        var state = pdf.AddObject($"<< /Type /ExtGState /SMask << /Type /Mask /S /Luminosity /G {group} 0 R >> >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Left, Middle, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(Right, Middle, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>An alpha soft mask shows content only where its group paints.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlphaSoftMaskFollowsTheGroupsCoverage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/GS gs 0 0 1 rg 0 0 100 100 re f" };
        var group = pdf.AddStream(
            "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceGray >>",
            "0 g 50 0 50 100 re f");
        var state = pdf.AddObject($"<< /Type /ExtGState /SMask << /Type /Mask /S /Alpha /G {group} 0 R >> >>");
        pdf.Resources = $"/ExtGState << /GS {state} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Left, Middle, Rgb.White, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(Right, Middle, Rgb.Blue255, Tolerance)).IsTrue();
    }
}

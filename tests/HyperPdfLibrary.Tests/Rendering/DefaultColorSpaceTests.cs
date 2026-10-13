// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Checks that /DefaultGray, /DefaultRGB and /DefaultCMYK in the current resources replace DeviceGray, DeviceRGB and
/// DeviceCMYK for the colour operators (ISO 32000-2 8.6.5.6). The test defaults paint everything red, green and blue.
/// </summary>
[NotInParallel]
public sealed class DefaultColorSpaceTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 2;

    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The whole page as a path.</summary>
    private const string Page = " 0 0 100 100 re f";

    /// <summary><c>g</c> and <c>G</c> use DefaultGray, which paints red.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayOperatorsUseDefaultGray()
    {
        var fill = Render($"0.5 g{Page}", true);
        var stroke = Render("0.5 G 100 w 0 50 m 100 50 l S", true);

        await RenderCheck.Near(fill, Middle, Middle, Rgb.Red255, Tolerance, nameof(GrayOperatorsUseDefaultGray));
        await RenderCheck.Near(stroke, Middle, Middle, Rgb.Red255, Tolerance, nameof(GrayOperatorsUseDefaultGray));
    }

    /// <summary><c>rg</c> and <c>RG</c> use DefaultRGB, which paints green.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbOperatorsUseDefaultRgb()
    {
        var fill = Render($"1 0 0 rg{Page}", true);
        var stroke = Render("1 0 0 RG 100 w 0 50 m 100 50 l S", true);

        await RenderCheck.Near(fill, Middle, Middle, Rgb.Green255, Tolerance, nameof(RgbOperatorsUseDefaultRgb));
        await RenderCheck.Near(stroke, Middle, Middle, Rgb.Green255, Tolerance, nameof(RgbOperatorsUseDefaultRgb));
    }

    /// <summary><c>k</c> and <c>K</c> use DefaultCMYK, which paints blue.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykOperatorsUseDefaultCmyk()
    {
        var fill = Render($"0 0 0 1 k{Page}", true);
        var stroke = Render("0 0 0 1 K 100 w 0 50 m 100 50 l S", true);

        await RenderCheck.Near(fill, Middle, Middle, Rgb.Blue255, Tolerance, nameof(CmykOperatorsUseDefaultCmyk));
        await RenderCheck.Near(stroke, Middle, Middle, Rgb.Blue255, Tolerance, nameof(CmykOperatorsUseDefaultCmyk));
    }

    /// <summary><c>cs</c> and <c>CS</c> naming a device space use the default space too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedDeviceSpacesUseTheDefaults()
    {
        var gray = Render($"/DeviceGray cs 0.5 sc{Page}", true);
        var rgb = Render($"/DeviceRGB cs 1 0 0 sc{Page}", true);
        var cmyk = Render($"/DeviceCMYK cs 0 0 0 1 sc{Page}", true);

        await RenderCheck.Near(gray, Middle, Middle, Rgb.Red255, Tolerance, nameof(NamedDeviceSpacesUseTheDefaults));
        await RenderCheck.Near(rgb, Middle, Middle, Rgb.Green255, Tolerance, nameof(NamedDeviceSpacesUseTheDefaults));
        await RenderCheck.Near(cmyk, Middle, Middle, Rgb.Blue255, Tolerance, nameof(NamedDeviceSpacesUseTheDefaults));
    }

    /// <summary>Without the resources the device spaces convert as they always do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WithoutDefaultsDeviceSpacesAreUnchanged()
    {
        var gray = Render($"0 g{Page}", false);
        var rgb = Render($"1 0 0 rg{Page}", false);

        await RenderCheck.Near(gray, Middle, Middle, Rgb.Black, Tolerance, nameof(WithoutDefaultsDeviceSpacesAreUnchanged));
        await RenderCheck.Near(rgb, Middle, Middle, Rgb.Red255, Tolerance, nameof(WithoutDefaultsDeviceSpacesAreUnchanged));
    }

    /// <summary>A form with its own resources does not see the page's defaults; a form without resources inherits them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormsUseTheirOwnResources()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Own Do" };
        var own = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Resources << >>", "1 0 0 rg 0 0 100 100 re f");
        var inherited = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 100 100]", "1 0 0 rg 0 0 100 100 re f");
        AddDefaults(pdf);
        pdf.Resources += $" /XObject << /Own {own} 0 R /Inherited {inherited} 0 R >>";
        using var ownPage = new RenderTestPage(pdf.ToBytes());
        var ownImage = ownPage.RenderPage();
        pdf.Content = "/Inherited Do";
        using var inheritedPage = new RenderTestPage(pdf.ToBytes());
        var inheritedImage = inheritedPage.RenderPage();

        await RenderCheck.Near(ownImage, Middle, Middle, Rgb.Red255, Tolerance, nameof(FormsUseTheirOwnResources));
        await RenderCheck.Near(inheritedImage, Middle, Middle, Rgb.Green255, Tolerance, nameof(FormsUseTheirOwnResources));
    }

    /// <summary>A colour made in a default space keeps that space, so later components are read in it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheColourKeepsTheDefaultSpace()
    {
        var recorder = GraphicsStateRecorder.Run(DefaultsPdf("0.5 g 0 0 10 10 re f 1 0 0 rg 0 0 10 10 re f 0 0 0 1 k 0 0 10 10 re f"));

        await Assert.That(recorder.Paints[0].Fill.Space.Kind).IsEqualTo(PdfColorSpaceKind.DeviceN);
        await Assert.That(recorder.Paints[1].Fill.Space.Kind).IsEqualTo(PdfColorSpaceKind.DeviceN);
        await Assert.That(recorder.Paints[2].Fill.Space.Components).IsEqualTo(CmykComponents);
    }

    /// <summary>A default of another component count, or that is itself a device space, is not used.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnfitDefaultsAreIgnored()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = $"1 0 0 rg{Page}" };
        pdf.Resources = "/ColorSpace << /DefaultRGB /DeviceGray /DefaultGray [/DeviceN [/A /B] /DeviceRGB 1 0 R] >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        var image = page.RenderPage();

        await RenderCheck.Near(image, Middle, Middle, Rgb.Red255, Tolerance, nameof(UnfitDefaultsAreIgnored));
    }

    /// <summary>Renders content on a white page with or without the default spaces.</summary>
    /// <param name="content">The content.</param>
    /// <param name="defaults">Whether the page names the default spaces.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, bool defaults)
    {
        var pdf = defaults ? DefaultsPdf(content) : new RenderTestPdf(Size, Size) { Content = content };
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }

    /// <summary>Builds a page whose resources name the default spaces.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The document.</returns>
    private static RenderTestPdf DefaultsPdf(string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        AddDefaults(pdf);
        return pdf;
    }

    /// <summary>Names the default spaces in the resources: gray paints red, RGB green and CMYK blue.</summary>
    /// <param name="pdf">The document.</param>
    private static void AddDefaults(RenderTestPdf pdf)
    {
        var red = pdf.AddStream("/FunctionType 4 /Domain [0 1] /Range [0 1 0 1 0 1]", "{ pop 1 0 0 }");
        var green = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1 0 1] /Range [0 1 0 1 0 1]", "{ pop pop pop 0 1 0 }");
        var blue = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1 0 1 0 1] /Range [0 1 0 1 0 1]", "{ pop pop pop pop 0 0 1 }");
        pdf.Resources =
            $"/ColorSpace << /DefaultGray [/DeviceN [/G] /DeviceRGB {red} 0 R] /DefaultRGB [/DeviceN [/X /Y /Z] /DeviceRGB {green} 0 R] "
            + $"/DefaultCMYK [/DeviceN [/A /B /C /D] /DeviceRGB {blue} 0 R] >>";
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders the overprint preview for images: only opaque Separation and DeviceN images leave backdrop colorants alone.</summary>
public sealed class OverprintRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The render passes after both renderers are created.</summary>
    private const int InterleavedPasses = 2;

    /// <summary>The DeviceCMYK colour space name.</summary>
    private const string Cmyk = "/DeviceCMYK";

    /// <summary>One cyan DeviceCMYK pixel in hexadecimal.</summary>
    private const string CyanPixel = "FF000000>";

    /// <summary>A Separation space that tints from white to blue.</summary>
    private const string SpotSpace = "[/Separation /Spot /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 1] /N 1 >>]";

    /// <summary>The overprinting graphics state.</summary>
    private const string Overprint = "/OP true /op true /OPM 0";

    /// <summary>A DeviceCMYK image specifies all four process colorants, so it covers the backdrop even with overprint on (ISO 32000-2 8.6.7).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykImageCoversEvenWithOverprint()
    {
        var cyan = Cyan();
        var image = Render(Cmyk, CyanPixel, Overprint);

        await RenderCheck.Near(image, Middle, Middle, new(cyan.Red, cyan.Green, cyan.Blue), Tolerance, nameof(CmykImageCoversEvenWithOverprint));
    }

    /// <summary>Without the option, overprint is not previewed and a spot colour image covers the backdrop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverprintIsOffByDefault()
    {
        var image = Render(SpotSpace, "FF>", Overprint, false);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Blue255, Tolerance, nameof(OverprintIsOffByDefault));
    }

    /// <summary>Each renderer keeps its own overprint setting when another renderer uses the same document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConflictingRenderersKeepTheirOverprintSettings()
    {
        using var document = SkiaTestRegistration.Open(CreateImagePdf(SpotSpace, "FF>", Overprint), null);
        using var overprinting = new PdfPageRenderer(document, PdfRenderOptions.Default with { SimulateOverprint = true });
        using var covering = new PdfPageRenderer(document, PdfRenderOptions.Default with { SimulateOverprint = false });

        for (var i = 0; i < InterleavedPasses; i++)
        {
            await RenderCheck.Near(RenderConfigured(overprinting), Middle, Middle, Rgb.Black, Tolerance, nameof(ConflictingRenderersKeepTheirOverprintSettings));
            await RenderCheck.Near(RenderConfigured(covering), Middle, Middle, Rgb.Blue255, Tolerance, nameof(ConflictingRenderersKeepTheirOverprintSettings));
        }
    }

    /// <summary>A Separation image keeps the backdrop colorants it does not name, which the preview approximates with Multiply.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparationImageOverprints()
    {
        var image = Render(SpotSpace, "FF>", Overprint);

        await RenderCheck.Near(image, Middle, Middle, Rgb.Black, Tolerance, nameof(SeparationImageOverprints));
    }

    /// <summary>Without overprint the image covers the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykImageWithoutOverprintCovers()
    {
        var cyan = Cyan();
        var image = Render(Cmyk, CyanPixel, "/OP false");

        await RenderCheck.Near(image, Middle, Middle, new(cyan.Red, cyan.Green, cyan.Blue), Tolerance, nameof(CmykImageWithoutOverprintCovers));
    }

    /// <summary>A DeviceCMYK image in overprint mode 1 still covers the backdrop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverprintModeOneDoesNotDarken()
    {
        var cyan = Cyan();
        var image = Render(Cmyk, CyanPixel, "/OP true /OPM 1");

        await RenderCheck.Near(image, Middle, Middle, new(cyan.Red, cyan.Green, cyan.Blue), Tolerance, nameof(OverprintModeOneDoesNotDarken));
    }

    /// <summary>An RGB image never overprints.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbImageDoesNotOverprint()
    {
        var image = Render("/DeviceRGB", "00FFFF>", Overprint);

        await RenderCheck.Near(image, Middle, Middle, new(0, Rgb.Green255.Green, Rgb.Blue255.Blue), Tolerance, nameof(RgbImageDoesNotOverprint));
    }

    /// <summary>In overprint mode 0 a CMYK fill paints all four components, so it covers the backdrop even in the preview.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykFillDoesNotOverprint()
    {
        var cyan = Cyan();
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 1 0 rg 0 0 100 100 re f /OP gs 1 0 0 0 k 0 0 100 100 re f" };
        pdf.Resources = $"/ExtGState << /OP << /Type /ExtGState {Overprint} >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());
        using var configured = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { SimulateOverprint = true });

        var image = RenderConfigured(configured);

        await RenderCheck.Near(image, Middle, Middle, new(cyan.Red, cyan.Green, cyan.Blue), Tolerance, nameof(CmykFillDoesNotOverprint));
    }

    /// <summary>Gets cyan as the library converts DeviceCMYK.</summary>
    /// <returns>The colour.</returns>
    private static ColorState Cyan() => ColorState.Resolve(PdfColorSpace.DeviceCmyk, [1, 0, 0, 0]);

    /// <summary>Renders a one pixel image over a yellow page.</summary>
    /// <param name="colorSpace">The image's colour space.</param>
    /// <param name="hex">The image data in hexadecimal.</param>
    /// <param name="state">The ExtGState entries in force.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string colorSpace, string hex, string state) => Render(colorSpace, hex, state, true);

    /// <summary>Renders a one pixel image over a yellow page.</summary>
    /// <param name="colorSpace">The image's colour space.</param>
    /// <param name="hex">The image data in hexadecimal.</param>
    /// <param name="state">The ExtGState entries in force.</param>
    /// <param name="simulate">Whether the document's options turn overprint simulation on.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string colorSpace, string hex, string state, bool simulate)
    {
        using var page = new RenderTestPage(CreateImagePdf(colorSpace, hex, state));
        using var configured = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { SimulateOverprint = simulate });
        return RenderConfigured(configured);
    }

    /// <summary>Creates a one-pixel image over a yellow page.</summary>
    /// <param name="colorSpace">The image's colour space.</param>
    /// <param name="hex">The image data in hexadecimal.</param>
    /// <param name="state">The ExtGState entries in force.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateImagePdf(string colorSpace, string hex, string state)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 1 0 rg 0 0 100 100 re f /OP gs q 100 0 0 100 0 0 cm /Im Do Q" };
        var image = pdf.AddStream($"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace {colorSpace} /BitsPerComponent 8 /Filter /ASCIIHexDecode", hex);
        pdf.Resources = $"/XObject << /Im {image} 0 R >> /ExtGState << /OP << /Type /ExtGState {state} >> >>";
        return pdf.ToBytes();
    }

    /// <summary>Renders the page through the specified renderer.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>The page pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static RenderedImage RenderConfigured(PdfPageRenderer renderer)
    {
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        if (!renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel)))
        {
            throw new InvalidOperationException("The page could not be rendered.");
        }

        return new(pixels, Size, Size);
    }
}

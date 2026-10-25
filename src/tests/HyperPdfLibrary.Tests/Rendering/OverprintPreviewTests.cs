// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Renders the overprint preview (ISO 32000-2 11.7.4.3) for fills, strokes and text. An object that leaves some backdrop
/// colorants alone is painted with the Multiply blend; one that replaces them all covers the backdrop.
/// </summary>
[NotInParallel]
public sealed class OverprintPreviewTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 3;

    /// <summary>The device column in the middle of the glyph square.</summary>
    private const int GlyphColumn = 30;

    /// <summary>The device row in the middle of the glyph square.</summary>
    private const int GlyphRow = 70;

    /// <summary>The cyan the dense colour has.</summary>
    private const float Light = 0.2F;

    /// <summary>The magenta the dense colour has.</summary>
    private const float Medium = 0.4F;

    /// <summary>The yellow the dense colour has.</summary>
    private const float Strong = 0.6F;

    /// <summary>The black the dense colour has.</summary>
    private const float Dark = 0.1F;

    /// <summary>A device column inside the stroke of the rectangle but outside its fill.</summary>
    private const int OutsideEdge = 17;

    /// <summary>The largest channel value, to divide a product of two channels by.</summary>
    private const int Full = 255;

    /// <summary>Paints the page cyan.</summary>
    private const string CyanPage = "1 0 0 0 k 0 0 100 100 re f ";

    /// <summary>The overprinting graphics states.</summary>
    private const string States =
        "/ExtGState << /Mode1 << /OP true /op true /OPM 1 >> /Mode0 << /OP true /op true /OPM 0 >> /Off << /OP false /op false /OPM 1 >> "
        + "/Fill << /op true /OPM 1 >> /Stroke << /OP true /op false /OPM 1 >> >>";

    /// <summary>A Separation colour space whose tint 1 is blue.</summary>
    private const string Spot = "[/Separation /Spot /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 1] /N 1 >>]";

    /// <summary>The /All Separation colour space, whose tint 1 is blue.</summary>
    private const string AllInk = "[/Separation /All /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 1] /N 1 >>]";

    /// <summary>A magenta fill with overprint mode 1 keeps the cyan beneath it, as the Multiply preview shows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ModeOneKeepsUntouchedComponentsOfTheBackdrop()
    {
        var image = Render($"{CyanPage}/Mode1 gs 0 1 0 0 k 0 0 100 100 re f", true);

        await RenderCheck.Near(image, Middle, Middle, Multiply(Resolve(1, 0, 0, 0), Resolve(0, 1, 0, 0)), Tolerance, nameof(ModeOneKeepsUntouchedComponentsOfTheBackdrop));
    }

    /// <summary>Overprint mode 0 paints all four DeviceCMYK components, so the fill replaces the backdrop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ModeZeroReplacesTheBackdrop()
    {
        var image = Render($"{CyanPage}/Mode0 gs 0 1 0 0 k 0 0 100 100 re f", true);

        await RenderCheck.Near(image, Middle, Middle, Resolve(0, 1, 0, 0), Tolerance, nameof(ModeZeroReplacesTheBackdrop));
    }

    /// <summary>Without overprint, or with the preview off, a fill covers the backdrop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverprintOffCoversTheBackdrop()
    {
        var disabled = Render($"{CyanPage}/Off gs 0 1 0 0 k 0 0 100 100 re f", true);
        var noPreview = Render($"{CyanPage}/Mode1 gs 0 1 0 0 k 0 0 100 100 re f", false);

        await RenderCheck.Near(disabled, Middle, Middle, Resolve(0, 1, 0, 0), Tolerance, nameof(OverprintOffCoversTheBackdrop));
        await RenderCheck.Near(noPreview, Middle, Middle, Resolve(0, 1, 0, 0), Tolerance, nameof(OverprintOffCoversTheBackdrop));
    }

    /// <summary>A fill that gives every component a value replaces the backdrop even in mode 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ModeOneWithNoZeroComponentReplaces()
    {
        var image = Render($"{CyanPage}/Mode1 gs 0.2 0.4 0.6 0.1 k 0 0 100 100 re f", true);

        await RenderCheck.Near(image, Middle, Middle, Resolve(Light, Medium, Strong, Dark), Tolerance, nameof(ModeOneWithNoZeroComponentReplaces));
    }

    /// <summary>Strokes overprint with /OP, and a fill-only overprint leaves strokes covering.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokesOverprint()
    {
        var stroked = Render($"{CyanPage}/Stroke gs 0 1 0 0 K 40 w 0 50 m 100 50 l S", true);
        var fillOnly = Render($"{CyanPage}/Fill gs 0 1 0 0 K 40 w 0 50 m 100 50 l S", true);

        await RenderCheck.Near(stroked, Middle, Middle, Multiply(Resolve(1, 0, 0, 0), Resolve(0, 1, 0, 0)), Tolerance, nameof(StrokesOverprint));
        await RenderCheck.Near(fillOnly, Middle, Middle, Resolve(0, 1, 0, 0), Tolerance, nameof(StrokesOverprint));
    }

    /// <summary>Text overprints with the fill overprint setting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextOverprints()
    {
        var image = Render($"{CyanPage}/Mode1 gs 0 1 0 0 k BT /F1 40 Tf 10 10 Td (A) Tj ET", true);

        await RenderCheck.Near(image, GlyphColumn, GlyphRow, Multiply(Resolve(1, 0, 0, 0), Resolve(0, 1, 0, 0)), Tolerance, nameof(TextOverprints));
    }

    /// <summary>A fill and stroke painted together each follow their own overprint setting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillAndStrokeFollowTheirOwnSettings()
    {
        var image = Render($"{CyanPage}/Stroke gs 0 1 0 0 k 0 1 0 0 K 10 w 20 20 60 60 re B", true);

        await RenderCheck.Near(image, Middle, Middle, Resolve(0, 1, 0, 0), Tolerance, nameof(FillAndStrokeFollowTheirOwnSettings));
        await RenderCheck.Near(image, OutsideEdge, Middle, Multiply(Resolve(1, 0, 0, 0), Resolve(0, 1, 0, 0)), Tolerance, nameof(FillAndStrokeFollowTheirOwnSettings));
    }

    /// <summary>A Separation overprints in either mode: it paints its own colorant only, so a yellow backdrop shows through the blue tint.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparationOverprints()
    {
        var spot = Render("1 1 0 rg 0 0 100 100 re f /Mode0 gs /CS0 cs 1 sc 0 0 100 100 re f", true, $"/ColorSpace << /CS0 {Spot} >>");
        var all = Render("1 1 0 rg 0 0 100 100 re f /Mode0 gs /CS0 cs 1 sc 0 0 100 100 re f", true, $"/ColorSpace << /CS0 {AllInk} >>");

        await RenderCheck.Near(spot, Middle, Middle, Rgb.Black, Tolerance, nameof(SeparationOverprints));
        await RenderCheck.Near(all, Middle, Middle, Rgb.Blue255, Tolerance, nameof(SeparationOverprints));
    }

    /// <summary>A DeviceN space that names only some of the process colorants overprints.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartialDeviceNOverprints()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 1 0 rg 0 0 100 100 re f /Mode0 gs /CS0 cs 1 0 sc 0 0 100 100 re f" };
        var tint = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1] /Range [0 1 0 1 0 1]", "{ pop pop 0 0 1 }");
        var allProcess = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1 0 1 0 1] /Range [0 1 0 1 0 1]", "{ pop pop pop pop 0 0 1 }");
        pdf.Resources = $"{States} /ColorSpace << /CS0 [/DeviceN [/Cyan /Magenta] /DeviceRGB {tint} 0 R] /CS1 [/DeviceN [/Cyan /Magenta /Yellow /Black] /DeviceRGB {allProcess} 0 R] >>";
        var partial = Render(pdf, true);
        pdf.Content = "1 1 0 rg 0 0 100 100 re f /Mode0 gs /CS1 cs 1 0 0 0 sc 0 0 100 100 re f";
        var full = Render(pdf, true);

        await RenderCheck.Near(partial, Middle, Middle, Rgb.Black, Tolerance, nameof(PartialDeviceNOverprints));
        await RenderCheck.Near(full, Middle, Middle, Rgb.Blue255, Tolerance, nameof(PartialDeviceNOverprints));
    }

    /// <summary>A renderer that previews overprint and one that does not keep their own results for the same document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderersKeepTheirOwnPreviewSettings()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = $"{CyanPage}/Mode1 gs 0 1 0 0 k 0 0 100 100 re f", Resources = States };
        using var page = new RenderTestPage(pdf.ToBytes());
        using var previewing = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { SimulateOverprint = true });
        using var covering = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { SimulateOverprint = false });

        await RenderCheck.Near(Draw(previewing), Middle, Middle, Multiply(Resolve(1, 0, 0, 0), Resolve(0, 1, 0, 0)), Tolerance, nameof(RenderersKeepTheirOwnPreviewSettings));
        await RenderCheck.Near(Draw(covering), Middle, Middle, Resolve(0, 1, 0, 0), Tolerance, nameof(RenderersKeepTheirOwnPreviewSettings));
    }

    /// <summary>Multiplies two colours channel by channel, as the Multiply blend does over an opaque backdrop.</summary>
    /// <param name="backdrop">The backdrop.</param>
    /// <param name="source">The source.</param>
    /// <returns>The product.</returns>
    private static Rgb Multiply(Rgb backdrop, Rgb source) =>
        new(backdrop.Red * source.Red / Full, backdrop.Green * source.Green / Full, backdrop.Blue * source.Blue / Full);

    /// <summary>Converts a DeviceCMYK colour as the library does.</summary>
    /// <param name="c">Cyan.</param>
    /// <param name="m">Magenta.</param>
    /// <param name="y">Yellow.</param>
    /// <param name="k">Black.</param>
    /// <returns>The colour.</returns>
    private static Rgb Resolve(float c, float m, float y, float k)
    {
        var colour = ColorState.Resolve(PdfColorSpace.DeviceCmyk, [c, m, y, k]);
        return new(colour.Red, colour.Green, colour.Blue);
    }

    /// <summary>Renders content on a white page with the test states.</summary>
    /// <param name="content">The content.</param>
    /// <param name="simulate">Whether the renderer previews overprint.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, bool simulate) => Render(content, simulate, string.Empty);

    /// <summary>Renders content on a white page with the test states and more resources.</summary>
    /// <param name="content">The content.</param>
    /// <param name="simulate">Whether the renderer previews overprint.</param>
    /// <param name="resources">More resource entries.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, bool simulate, string resources)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        pdf.Resources = $"{States} {resources} /Font << /F1 {font} 0 R >>";
        return Render(pdf, simulate);
    }

    /// <summary>Renders a prepared document's first page.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="simulate">Whether the renderer previews overprint.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(RenderTestPdf pdf, bool simulate)
    {
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new SquareFont(dictionary));
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { SimulateOverprint = simulate });
        return Draw(renderer);
    }

    /// <summary>Draws the first page with a renderer.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>The pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static RenderedImage Draw(PdfPageRenderer renderer)
    {
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        if (!renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel)))
        {
            throw new InvalidOperationException("The page could not be rendered.");
        }

        return new(pixels, Size, Size);
    }
}

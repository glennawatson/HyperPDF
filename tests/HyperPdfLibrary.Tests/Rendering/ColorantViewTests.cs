// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Lists the colorants a page paints and renders it with some of them hidden, as the separation view does.</summary>
[NotInParallel]
public sealed class ColorantViewTests
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

    /// <summary>The render passes of each renderer.</summary>
    private const int Passes = 2;

    /// <summary>The name of the Separation colorant of the test pages.</summary>
    private const string SpotInk = "Spot One";

    /// <summary>The name of the Magenta process colorant.</summary>
    private const string MagentaInk = "Magenta";

    /// <summary>The name of the Black process colorant.</summary>
    private const string BlackInk = "Black";

    /// <summary>The page, as a path to fill.</summary>
    private const string Page = " 0 0 100 100 re f";

    /// <summary>Yellow over the whole page, then the spot colour.</summary>
    private const string YellowThenSpot = $"1 1 0 rg{Page} /Spot cs 1 sc{Page}";

    /// <summary>The colour spaces of the test pages: spots that paint blue, and a DeviceN space that paints its first tint as red and its second as blue.</summary>
    private const string Spaces =
        "/ColorSpace << /Spot [/Separation /Spot#20One /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 1] /N 1 >>] "
        + "/Every [/Separation /All /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 0] /N 1 >>] "
        + "/Nothing [/Separation /None /DeviceRGB << /FunctionType 2 /Domain [0 1] /C0 [1 1 1] /C1 [0 0 0] /N 1 >>] "
        + "/Pair [/DeviceN [/Orange /Teal] /DeviceRGB 1 0 R] >>";

    /// <summary>Hides the spot colorant.</summary>
    private static readonly ImmutableArray<string> HideSpot = [SpotInk];

    /// <summary>Hides Magenta.</summary>
    private static readonly ImmutableArray<string> HideMagenta = [MagentaInk];

    /// <summary>Hides Black.</summary>
    private static readonly ImmutableArray<string> HideBlack = [BlackInk];

    /// <summary>The colorants of a page come in Cyan, Magenta, Yellow, Black order, then the others as they are first painted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageColorantsAreListedInOrder()
    {
        using var page = Open(
            "0 0 0 1 k 0 0 10 10 re f /Spot cs 1 sc 0 0 10 10 re f 0 1 0 0 k 0 0 10 10 re f /Pair cs 1 1 sc 0 0 10 10 re f 0.5 g 0 0 10 10 re f /Every cs 1 sc 0 0 10 10 re f");

        var colorants = PdfDocumentColorants.GetPageColorants(page.Document, 0);

        await Assert.That(string.Join(',', colorants.Select(static c => c.Name))).IsEqualTo("Magenta,Black,Spot One,Orange,Teal,All");
        await Assert.That(colorants[0].Kind).IsEqualTo(PdfColorantKind.Process);
        await Assert.That(colorants[2].Kind).IsEqualTo(PdfColorantKind.Spot);
        await Assert.That(colorants[^1].Kind).IsEqualTo(PdfColorantKind.All);
    }

    /// <summary>A colour with no ink, /None, RGB and Lab-like colours list nothing; a tint of zero lists nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColoursWithoutInkListNothing()
    {
        using var page = Open("0 0 0 0 k 0 0 10 10 re f 1 g 0 0 10 10 re f 1 0 0 rg 0 0 10 10 re f /Spot cs 0 sc 0 0 10 10 re f /Nothing cs 1 sc 0 0 10 10 re f");

        var colorants = await PdfDocumentColorants.GetPageColorantsAsync(page.Document, 0, CancellationToken.None);

        await Assert.That(colorants.IsEmpty).IsTrue();
    }

    /// <summary>Strokes, text and images list their colorants, and an image lists every colorant of its colour space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokesTextAndImagesAreListed()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 0 K 0 0 m 10 10 l S BT /F1 10 Tf 0 0 1 0 k (A) Tj ET q 10 0 0 10 0 0 cm /Im Do Q" };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /Spot /BitsPerComponent 8 /Filter /ASCIIHexDecode", "FF>");
        var tint = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1] /Range [0 1 0 1 0 1]", "{ 0 exch }");
        pdf.Resources = $"{Spaces.Replace("1 0 R", $"{tint} 0 R", StringComparison.Ordinal)} /Font << /F1 {font} 0 R >> /XObject << /Im {image} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes(), static dictionary => new SquareFont(dictionary));

        var colorants = PdfDocumentColorants.GetPageColorants(page.Document, 0);

        await Assert.That(string.Join(',', colorants.Select(static c => c.Name))).IsEqualTo("Cyan,Yellow,Spot One");
    }

    /// <summary>A page that does not exist is an error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPagesThrow()
    {
        using var page = Open($"0 g{Page}");

        await Assert.That(() => PdfDocumentColorants.GetPageColorants(page.Document, 1)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Hiding a Separation colorant takes its objects off the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidingASpotRemovesItsObjects()
    {
        var shown = Render(YellowThenSpot, []);
        var hidden = Render(YellowThenSpot, HideSpot);

        await RenderCheck.Near(shown, Middle, Middle, Rgb.Blue255, Tolerance, nameof(HidingASpotRemovesItsObjects));
        await RenderCheck.Near(hidden, Middle, Middle, Rgb.Yellow, Tolerance, nameof(HidingASpotRemovesItsObjects));
    }

    /// <summary>Hiding a process colorant clears it from CMYK fills and strokes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidingAProcessColorantClearsItFromCmykColours()
    {
        var fill = Render($"1 1 0 0 k{Page}", HideMagenta);
        var onlyMagenta = Render($"0 1 0 0 k{Page}", HideMagenta);
        var stroke = Render("1 1 0 0 K 100 w 0 50 m 100 50 l S", HideMagenta);
        var cyan = HyperPdfLibrary.Graphics.ColorState.Resolve(PdfColorSpace.DeviceCmyk, [1, 0, 0, 0]);

        await RenderCheck.Near(fill, Middle, Middle, new(cyan.Red, cyan.Green, cyan.Blue), Tolerance, nameof(HidingAProcessColorantClearsItFromCmykColours));
        await RenderCheck.Near(onlyMagenta, Middle, Middle, Rgb.White, Tolerance, nameof(HidingAProcessColorantClearsItFromCmykColours));
        await RenderCheck.Near(stroke, Middle, Middle, new(cyan.Red, cyan.Green, cyan.Blue), Tolerance, nameof(HidingAProcessColorantClearsItFromCmykColours));
    }

    /// <summary>Hiding Black clears gray fills and glyphs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidingBlackClearsGrayColours()
    {
        var fill = Render($"0.5 g{Page}", HideBlack);
        var text = Render("0 g BT /F1 40 Tf 10 10 Td (A) Tj ET", HideBlack);

        await RenderCheck.Near(fill, Middle, Middle, Rgb.White, Tolerance, nameof(HidingBlackClearsGrayColours));
        await RenderCheck.Near(text, GlyphColumn, GlyphRow, Rgb.White, Tolerance, nameof(HidingBlackClearsGrayColours));
    }

    /// <summary>Hiding one colorant of a DeviceN space sets its tint to zero and paints the others.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidingADeviceNColorantZeroesItsTint()
    {
        var both = Render($"/Pair cs 1 1 sc{Page}", []);
        var withoutTeal = Render($"/Pair cs 1 1 sc{Page}", ["Teal"]);
        var withoutBoth = Render($"1 1 0 rg{Page} /Pair cs 1 1 sc{Page}", ["Teal", "Orange"]);

        await RenderCheck.Near(both, Middle, Middle, new(Rgb.Red255.Red, 0, Rgb.Blue255.Blue), Tolerance, nameof(HidingADeviceNColorantZeroesItsTint));
        await RenderCheck.Near(withoutTeal, Middle, Middle, Rgb.Red255, Tolerance, nameof(HidingADeviceNColorantZeroesItsTint));
        await RenderCheck.Near(withoutBoth, Middle, Middle, Rgb.Yellow, Tolerance, nameof(HidingADeviceNColorantZeroesItsTint));
    }

    /// <summary>Hiding a colorant leaves the page alone when the page does not paint it, and RGB colours are never hidden.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnrelatedColorantsLeaveThePageAlone()
    {
        var rgb = Render($"1 0 0 rg{Page}", ["Cyan", MagentaInk, "Yellow", BlackInk, SpotInk]);

        await RenderCheck.Near(rgb, Middle, Middle, Rgb.Red255, Tolerance, nameof(UnrelatedColorantsLeaveThePageAlone));
    }

    /// <summary>Renderers with different hidden sets keep their own pages when they share a document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderersKeepTheirOwnHiddenSets()
    {
        using var page = Open($"1 1 0 rg{Page} /Spot cs 1 sc{Page}");
        using var showing = new PdfPageRenderer(page.Document, PdfRenderOptions.Default);
        using var hiding = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { HiddenColorants = [SpotInk, SpotInk] });
        using var hidingAgain = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { HiddenColorants = HideSpot });

        for (var i = 0; i < Passes; i++)
        {
            await RenderCheck.Near(Draw(showing), Middle, Middle, Rgb.Blue255, Tolerance, nameof(RenderersKeepTheirOwnHiddenSets));
            await RenderCheck.Near(Draw(hiding), Middle, Middle, Rgb.Yellow, Tolerance, nameof(RenderersKeepTheirOwnHiddenSets));
            await RenderCheck.Near(Draw(hidingAgain), Middle, Middle, Rgb.Yellow, Tolerance, nameof(RenderersKeepTheirOwnHiddenSets));
        }
    }

    /// <summary>Opens a test page with the test colour spaces.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The page.</returns>
    private static RenderTestPage Open(string content) => new(Build(content).ToBytes(), static dictionary => new SquareFont(dictionary));

    /// <summary>Builds a test page with the test colour spaces and a font.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The document.</returns>
    private static RenderTestPdf Build(string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        var tint = pdf.AddStream("/FunctionType 4 /Domain [0 1 0 1] /Range [0 1 0 1 0 1]", "{ 0 exch }");
        pdf.Resources = $"{Spaces.Replace("1 0 R", $"{tint} 0 R", StringComparison.Ordinal)} /Font << /F1 {font} 0 R >>";
        return pdf;
    }

    /// <summary>Renders content with some colorants hidden.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="hidden">The hidden colorants.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string content, ImmutableArray<string> hidden)
    {
        using var page = Open(content);
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { HiddenColorants = hidden });
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

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that the bitmap glyphs of a Type 3 font are drawn from cached glyph masks.</summary>
[NotInParallel(nameof(RenderAllocationTests))]
public sealed class Type3GlyphMaskRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The renders made before measuring.</summary>
    private const int Warmup = 4;

    /// <summary>The most measured passes made; the smallest counts.</summary>
    private const int Passes = 10;

    /// <summary>The renders measured.</summary>
    private const int Measured = 8;

    /// <summary>The share of ink a mask render may differ from a direct render by.</summary>
    private const double InkTolerance = 0.2;

    /// <summary>The position of the green channel in a BGRA pixel.</summary>
    private const int GreenSlot = 1;

    /// <summary>The position of the red channel in a BGRA pixel.</summary>
    private const int RedSlot = 2;

    /// <summary>The red channel at or above which a pixel counts as red text.</summary>
    private const int RedText = 200;

    /// <summary>The settings that draw small text and glyph bitmaps from masks.</summary>
    private static readonly PdfRenderQuality Masks = PdfRenderQuality.Default with { GlyphMasks = true };

    /// <summary>Verifies that bitmap glyphs drawn from masks put about as much ink on the page as glyphs drawn directly, in the text's colour.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BitmapGlyphs_DrawAboutTheSameInkFromMasks()
    {
        var direct = Ink(RenderText(PdfRenderQuality.Default));
        var masks = RenderText(Masks);

        await Assert.That(direct).IsGreaterThan(0);
        await Assert.That(Math.Abs(Ink(masks) - direct) / direct).IsLessThan(InkTolerance);
        await Assert.That(HasRedText(masks)).IsTrue();
    }

    /// <summary>Verifies that a page of bitmap glyphs drawn from masks renders with no allocation once warm.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BitmapGlyphMasks_DoNotAllocate()
    {
        using var page = new RenderTestPage(Build(), static dictionary => new TestType3Font(dictionary));
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = Masks });

        await Assert.That(MeasureRender(renderer)).IsEqualTo(0L);
    }

    /// <summary>Makes a page of red bitmap glyphs: a Type 3 font whose one glyph is a stencil image mask.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg BT /F1 12 Tf 5 60 Td (AAAAAAAA) Tj 0 -14 Td (AAAAAAAA) Tj ET" };
        var glyph = pdf.AddStream(string.Empty, "1000 0 0 0 1000 1000 d1 q 1000 0 0 1000 0 0 cm /Im Do Q");
        var mask = pdf.AddStream("/Type /XObject /Subtype /Image /Width 8 /Height 8 /ImageMask true /BitsPerComponent 1 /Filter /ASCIIHexDecode", "FF818181818181FF>");
        var font = pdf.AddObject(
            $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /a {glyph} 0 R >> "
            + $"/Encoding << /Differences [65 /a] >> /FirstChar 65 /LastChar 65 /Widths [1000] /Resources << /XObject << /Im {mask} 0 R >> >> >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Renders the page with quality settings.</summary>
    /// <param name="quality">The quality settings.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderText(PdfRenderQuality quality)
    {
        using var page = new RenderTestPage(Build(), static dictionary => new TestType3Font(dictionary));
        using var renderer = new PdfPageRenderer(page.Document, PdfRenderOptions.Default with { Quality = quality });
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        _ = Render(renderer, pixels);
        return new(pixels, Size, Size);
    }

    /// <summary>Renders the page into pixels.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="pixels">The target pixels.</param>
    /// <returns><see langword="true"/> when it rendered.</returns>
    private static bool Render(PdfPageRenderer renderer, byte[] pixels) =>
        renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel));

    /// <summary>Renders repeatedly and returns the bytes allocated by the measured renders.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>The bytes allocated on this thread; the smallest pass counts.</returns>
    private static long MeasureRender(PdfPageRenderer renderer)
    {
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        for (var i = 0; i < Warmup; i++)
        {
            _ = Render(renderer, pixels);
        }

        var smallest = long.MaxValue;
        for (var pass = 0; pass < Passes && smallest > 0; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Measured; i++)
            {
                _ = Render(renderer, pixels);
            }

            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return smallest;
    }

    /// <summary>Adds up how far the page is from white, by the green channel.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns>The ink.</returns>
    private static double Ink(RenderedImage image)
    {
        double ink = 0;
        for (var i = GreenSlot; i < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            ink += byte.MaxValue - image.Pixels[i];
        }

        return ink;
    }

    /// <summary>Determines whether any pixel is the red of the text.</summary>
    /// <param name="image">The pixels.</param>
    /// <returns><see langword="true"/> when one is.</returns>
    private static bool HasRedText(RenderedImage image)
    {
        for (var i = 0; i + RedSlot < image.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            if (image.Pixels[i + RedSlot] >= RedText && image.Pixels[i + GreenSlot] == 0 && image.Pixels[i] == 0)
            {
                return true;
            }
        }

        return false;
    }
}

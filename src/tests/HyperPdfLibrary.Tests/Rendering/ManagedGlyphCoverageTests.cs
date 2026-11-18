// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Render.Managed;
using HyperPdfLibrary.Render.Managed.Raster;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Checks the coverage of glyphs the managed rasteriser keeps when glyph masks are on: text drawn from kept coverage looks like
/// text filled as shapes, stays inside a clip, comes out the same every time, and the cache starts again when it is full.
/// </summary>
public class ManagedGlyphCoverageTests
{
    /// <summary>The font flags of a nonsymbolic font.</summary>
    private const int FontFlags = 32;

    /// <summary>The pixels of a point.</summary>
    private const int Scale = 2;

    /// <summary>The whole a share is taken of.</summary>
    private const int Whole = 100;

    /// <summary>The least ink, as a share, that text from kept coverage may have next to text filled as shapes.</summary>
    private const int LowShare = 97;

    /// <summary>The most ink, as a share, that text from kept coverage may have next to text filled as shapes.</summary>
    private const int HighShare = 103;

    /// <summary>The widest and tallest coverage the test keeps, in pixels.</summary>
    private const int Side = 96;

    /// <summary>The glyphs kept, more than the cache has room for at that size.</summary>
    private const int Glyphs = 300;

    /// <summary>Words with letters that come again, at origins that fall between pixels.</summary>
    private const string Text = "BT /F1 18 Tf 10.3 60.4 Td (Hello Hello Hello) Tj ET";

    /// <summary>The same words inside a clip with slanted sides, which cuts through the letters.</summary>
    private const string ClippedText = "20 40 m 180 40 l 100 95 l h W n BT /F1 18 Tf 10.3 60.4 Td (Hello Hello Hello) Tj ET";

    /// <summary>Text drawn from kept coverage has the ink of text filled as shapes, to within the small shift of each glyph's origin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptGlyphsWeighAsMuchAsFilledOnes()
    {
        var filled = Darkness(Render(Text, PdfRenderQuality.Default));
        var kept = Darkness(Render(Text, PdfRenderQuality.Default with { GlyphMasks = true }));

        await Assert.That(filled).IsGreaterThan(0);
        await Assert.That(kept * Whole).IsBetween(filled * LowShare, filled * HighShare);
    }

    /// <summary>Kept coverage is thinned by a clip that is not a rectangle, as filled glyphs are.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptGlyphsStayInsideAClip()
    {
        var filled = Darkness(Render(ClippedText, PdfRenderQuality.Default));
        var kept = Darkness(Render(ClippedText, PdfRenderQuality.Default with { GlyphMasks = true }));
        var unclipped = Darkness(Render(Text, PdfRenderQuality.Default with { GlyphMasks = true }));

        await Assert.That(filled).IsGreaterThan(0);
        await Assert.That(kept).IsLessThan(unclipped);
        await Assert.That(kept * Whole).IsBetween(filled * LowShare, filled * HighShare);
    }

    /// <summary>A page drawn again, when every glyph comes from kept coverage, has the pixels it had the first time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeptGlyphsAreDrawnTheSameEveryTime()
    {
        var quality = PdfRenderQuality.Default with { GlyphMasks = true };
        var first = Render(Text, quality);
        var second = Render(Text, quality);

        await Assert.That(first.AsSpan().SequenceEqual(second)).IsTrue();
    }

    /// <summary>The cache forgets what it kept when its room runs out, and keeps what comes after.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CacheStartsAgainWhenFull()
    {
        var cache = new GlyphCoverageCache();
        for (var glyph = 0; glyph < Glyphs; glyph++)
        {
            var kept = cache.Add(KeyOf(glyph), 0, 0, Side, Side);
            cache.Row(kept, Side - 1).Fill(byte.MaxValue);
        }

        await Assert.That(cache.Count).IsLessThan(Glyphs);
        await Assert.That(cache.TryGet(KeyOf(0), out _)).IsFalse();
        await Assert.That(cache.TryGet(KeyOf(Glyphs - 1), out var last)).IsTrue();
        await Assert.That(last.Width).IsEqualTo(Side);
    }

    /// <summary>Names a glyph by a number, so each number is a different glyph of the same outline.</summary>
    /// <param name="glyph">The number.</param>
    /// <returns>The name.</returns>
    private static GlyphCoverageKey KeyOf(int glyph) => new(PdfPath.Empty, glyph, 0, 0, 0, 0, 0, null);

    /// <summary>Sums the ink of a page: how far every colour byte is from white.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <returns>The ink.</returns>
    private static long Darkness(byte[] pixels)
    {
        long ink = 0;
        for (var pixel = 0; pixel < pixels.Length; pixel += RenderedImage.BytesPerPixel)
        {
            ink += (byte.MaxValue - pixels[pixel]) + (byte.MaxValue - pixels[pixel + 1]) + (byte.MaxValue - pixels[pixel + 1 + 1]);
        }

        return ink;
    }

    /// <summary>Renders the first page of a document with the managed rasteriser.</summary>
    /// <param name="content">The page's content stream.</param>
    /// <param name="quality">The render quality.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(string content, PdfRenderQuality quality)
    {
        var pdf = FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = FontFlags, Entries = "/Encoding /WinAnsiEncoding", Content = content });
        var registration = new PdfRenderBackendRegistration().UseSkia().UseManaged();
        using var document = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = PdfFontLoader.Load, DrawingServices = new(registration) });
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { Quality = quality });
        const int width = FontTestDocument.PageWidth * Scale;
        const int height = FontTestDocument.PageHeight * Scale;
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel));
        return pixels;
    }
}

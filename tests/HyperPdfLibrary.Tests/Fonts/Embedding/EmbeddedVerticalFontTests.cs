// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Optimizing;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Embedding a font for vertical writing: <c>Identity-V</c>, <c>/DW2</c>, <c>/W2</c> and downward advance.</summary>
[NotInParallel]
public sealed class EmbeddedVerticalFontTests
{
    /// <summary>The bytes of a pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The divisor that halves a width into the position vector's x.</summary>
    private const int HalfDivisor = 2;

    /// <summary>The index of the origin height in a <c>/W2</c> entry.</summary>
    private const int OriginIndex = 2;

    /// <summary>A channel value below which a pixel counts as ink.</summary>
    private const int InkLimit = 128;

    /// <summary>The vertical options.</summary>
    private static readonly PdfEmbeddedFontOptions Vertical = new() { WritingMode = PdfWritingMode.Vertical };

    /// <summary>The font writes <c>Identity-V</c> with the default displacement and a <c>/W2</c> entry for a narrower glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesIdentityVWithVerticalWidths()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.CreateVertical(), 0), Vertical, "ii");
        EmbeddedFontPdf.Keep("truetype-vertical.pdf", pdf);

        using var inspector = new EmbeddedFontInspector(pdf);
        var defaults = inspector.Descendant.GetArray(KnownName.DW2)!;
        var widths = inspector.Descendant.GetArray(KnownName.W2)!;
        var entry = widths.GetArray(1)!;

        await Assert.That(inspector.Type0.IsName(KnownName.Encoding, KnownName.IdentityV)).IsTrue();
        await Assert.That(defaults.GetInt32(0)).IsEqualTo(NotdefOrigin());
        await Assert.That(defaults.GetInt32(1)).IsEqualTo(-TestFont.VerticalAdvanceOf('A'));
        await Assert.That(widths.GetInt32(0)).IsEqualTo(1);
        await Assert.That(entry.GetInt32(0)).IsEqualTo(-TestFont.VerticalAdvanceOf('i'));
        await Assert.That(entry.GetInt32(1)).IsEqualTo(TestFont.AdvanceOf('i') / HalfDivisor);
        await Assert.That(entry.GetInt32(OriginIndex)).IsEqualTo(Origin('i'));
    }

    /// <summary>A font with no vertical metrics advances one em and puts the origin at the ascent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontWithoutVerticalMetricsUsesAnEmAndTheAscent()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), Vertical, "ab");

        using var inspector = new EmbeddedFontInspector(pdf);
        var defaults = inspector.Descendant.GetArray(KnownName.DW2)!;

        await Assert.That(inspector.Type0.IsName(KnownName.Encoding, KnownName.IdentityV)).IsTrue();
        await Assert.That(defaults.GetInt32(0)).IsEqualTo(TestFont.Ascender);
        await Assert.That(defaults.GetInt32(1)).IsEqualTo(-TestFont.UnitsPerEm);
        await Assert.That(inspector.Descendant.GetArray(KnownName.W2)).IsNull();
    }

    /// <summary>Vertical text extracts as typed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalTextExtracts()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.CreateVertical(), 0), Vertical, "Hello 中");

        using var document = PdfDocumentReader.Open(pdf, null);
        var page = PdfDocumentText.GetTextPage(document, 0);

        await Assert.That(page.Text.Replace("\r", string.Empty).Replace("\n", string.Empty)).Contains("Hello");
    }

    /// <summary>Glyphs stack downward, so the ink is taller than it is wide; horizontal text is the other way round.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GlyphsAdvanceDownward()
    {
        var source = EmbeddedFontPdf.Open(TestFont.CreateVertical(), 0);
        var vertical = Ink(OptimizerTestKit.Render(EmbeddedFontPdf.Lines(source, Vertical, "HHHHHH")));
        var horizontal = Ink(OptimizerTestKit.Render(EmbeddedFontPdf.Lines(source, PdfEmbeddedFontOptions.Default, "HHHHHH")));

        await Assert.That(vertical.Height).IsGreaterThan(vertical.Width);
        await Assert.That(horizontal.Width).IsGreaterThan(horizontal.Height);
    }

    /// <summary>Gets the vertical origin the test font gives a character.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The origin height above the baseline.</returns>
    private static int Origin(char c) => TestFont.TopOf(c) + TestFont.TopSideBearing;

    /// <summary>Gets the vertical origin of the missing glyph, which is a rectangle 700 units high.</summary>
    /// <returns>The origin height.</returns>
    private static int NotdefOrigin() => 0x2BC + TestFont.TopSideBearing;

    /// <summary>Measures the dark pixels of a render.</summary>
    /// <param name="image">The render.</param>
    /// <returns>The size of the box around the ink.</returns>
    private static InkBox Ink(RenderedImage image)
    {
        var left = image.Width;
        var top = image.Height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.Pixels[((y * image.Width) + x) * PixelBytes] >= InkLimit)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return new(right - left + 1, bottom - top + 1);
    }

    /// <summary>The size of the box around some ink.</summary>
    /// <param name="Width">The width in pixels.</param>
    /// <param name="Height">The height in pixels.</param>
    private readonly record struct InkBox(int Width, int Height);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Render.Skia.Fonts;
using HyperPdfLibrary.Tests.Fonts;

namespace HyperPdf.Engine.Tests;

/// <summary>Renders text pages with PDFium and with HyperPDF's managed fonts and compares the pixels.</summary>
[NotInParallel(nameof(FontRenderParityTests))]
public sealed class FontRenderParityTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// The largest mean absolute difference per channel accepted for embedded fonts, which both engines draw from the
    /// same outlines. PDFium snaps glyph origins to whole pixels and rasterises glyphs itself, so edges differ slightly.
    /// </summary>
    private const double EmbeddedThreshold = 1.5;

    /// <summary>
    /// The largest mean difference accepted for a standard font. Both engines draw it from the same bundled Foxit
    /// faces, so only rasterisation differs (measured 0.085).
    /// </summary>
    private const double SubstituteThreshold = 0.5;

    /// <summary>The per-channel tolerance for CJK font rasterization differences.</summary>
    private const byte CjkChannelTolerance = 16;

    /// <summary>A dark pixel in a glyph's expected area.</summary>
    private const byte InkThreshold = 200;

    /// <summary>The horizontal text origin in the CJK fixtures.</summary>
    private const int CjkTextX = 10;

    /// <summary>The top of the first text line's ink area.</summary>
    private const int CjkFirstLineY = 20;

    /// <summary>The top of the second text line's ink area.</summary>
    private const int CjkSecondLineY = 50;

    /// <summary>The advance and search width of a CJK glyph at 24 points.</summary>
    private const int CjkGlyphWidth = 24;

    /// <summary>The search height of a CJK text line.</summary>
    private const int CjkLineHeight = 30;

    /// <summary>The number of glyphs in the Japanese fixture's first line.</summary>
    private const int JapaneseGlyphs = 4;

    /// <summary>The number of glyphs in each Chinese fixture line.</summary>
    private const int ChineseGlyphs = 2;

    /// <summary>The number of glyphs in the Korean and Identity-H fixtures.</summary>
    private const int ThreeGlyphs = 3;

    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The text page content: two lines of mixed glyphs, spacing and a TJ adjustment.</summary>
    private const string SimpleText = "BT /F1 24 Tf 10 60 Td (Hello AVW mi) Tj 0 -30 Td 3 Tw [(a b) -500 (cd)] TJ ET";

    /// <summary>The composite text page content: CIDs of A, B and m.</summary>
    private const string CompositeText = "BT /F1 24 Tf 10 60 Td <00220023004E> Tj 0 -30 Td <0022> Tj ET";

    /// <summary>An embedded TrueType font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedTrueTypeMatchesPdfium() => AssertParity(
        nameof(EmbeddedTrueTypeMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = Nonsymbolic, Entries = "/Encoding /WinAnsiEncoding", Content = SimpleText }),
        EmbeddedThreshold);

    /// <summary>An embedded Type 1 font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedType1MatchesPdfium()
    {
        var program = TestFontPrograms.Type1(out var length1);
        var spec = new FontSpec
        {
            Subtype = "Type1",
            Program = program,
            FileKey = "FontFile",
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1}"),
            Flags = Symbolic,
            Content = SimpleText,
        };
        return AssertParity(nameof(EmbeddedType1MatchesPdfium), FontTestDocument.Build(spec), EmbeddedThreshold);
    }

    /// <summary>An embedded CFF font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCffMatchesPdfium() => AssertParity(
        nameof(EmbeddedCffMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "Type1", Program = TestFontPrograms.Cff(), FileKey = "FontFile3", FileEntries = "/Subtype /Type1C", Flags = Nonsymbolic, Content = SimpleText }),
        EmbeddedThreshold);

    /// <summary>An Identity-H CIDFontType2 font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCompositeMatchesPdfium() => AssertParity(
        nameof(EmbeddedCompositeMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "Type0", Program = TestFont.Create(), Flags = Symbolic, CidEntries = "/W [34 [500 500]]", Content = CompositeText }),
        EmbeddedThreshold);

    /// <summary>A Helvetica page, drawn with the bundled Foxit face, stays close to PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task StandardFontIsCloseToPdfium() =>
        AssertParity(nameof(StandardFontIsCloseToPdfium), TestPdf.Create(1), SubstituteThreshold);

    /// <summary>Predefined CMaps paint every mapped glyph, including glyphs that PDFium omits with some host fonts.</summary>
    /// <param name="encoding">The predefined CMap.</param>
    /// <param name="ordering">The CID collection.</param>
    /// <param name="content">The encoded text.</param>
    /// <param name="sample">A character whose host font must be present.</param>
    /// <param name="firstLineGlyphs">The number of glyphs on the first line.</param>
    /// <param name="secondLineGlyphs">The number of glyphs on the second line.</param>
    /// <returns>A task.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">The host has no face for this script.</exception>
    [Test]
    [Arguments("90ms-RKSJ-H", "Japan1", CjkParityPdf.ShiftJisContent, 'あ', JapaneseGlyphs, 0)]
    [Arguments("GB-EUC-H", "GB1", CjkParityPdf.GbEucContent, '中', ChineseGlyphs, ChineseGlyphs)]
    [Arguments("ETen-B5-H", "CNS1", CjkParityPdf.TraditionalContent, '一', ChineseGlyphs, 0)]
    [Arguments("UniKS-UCS2-H", "Korea1", CjkParityPdf.UniKsContent, '한', ThreeGlyphs, 0)]
    [Arguments("Identity-H", "Japan1", CjkParityPdf.IdentityJapan1Content, 'あ', ThreeGlyphs, 0)]
    public async Task CjkTextDrawsAllMappedGlyphs(string encoding, string ordering, string content, char sample, int firstLineGlyphs, int secondLineGlyphs)
    {
        using var face = new SkiaFontProvider().MatchCharacter(sample);
        if (face is null || face.GetGlyph(sample) == 0)
        {
            throw new TUnit.Core.Exceptions.SkipTestException($"No system font has the {ordering} sample glyph.");
        }

        using var input = new HyperPdfTestDocument(CjkParityPdf.Build(encoding, ordering, content, string.Empty));
        await input.Document.PreparePageAsync(0, CancellationToken.None);
        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.None);
        var reference = PdfiumReferenceRendering.Render(input.Pdf, info);
        var actual = new byte[reference.Pixels.Length];
        await Assert.That(input.Document.Render(info, new(actual, reference.Width, reference.Height, reference.Width * BytesPerPixel))).IsTrue();

        var score = RasterScoring.Compare(
            new(reference.Width, reference.Height, actual),
            new(reference.Width, reference.Height, reference.Pixels),
            CjkChannelTolerance);
        TestContext.Current?.Output.WriteLine($"{encoding} using {face.FamilyName}: {score.ErrorPixels} of {score.CheckedPixels} pixels differ from PDFium beyond {CjkChannelTolerance}");
        await Assert.That(score.DimensionsMatch).IsTrue();
        for (var glyph = 0; glyph < firstLineGlyphs; glyph++)
        {
            await Assert.That(HasInk(actual, reference.Width, CjkTextX + (glyph * CjkGlyphWidth), CjkFirstLineY)).IsTrue();
        }

        for (var glyph = 0; glyph < secondLineGlyphs; glyph++)
        {
            await Assert.That(HasInk(actual, reference.Width, CjkTextX + (glyph * CjkGlyphWidth), CjkSecondLineY)).IsTrue();
        }
    }

    /// <summary>Checks that one expected glyph cell has visible text.</summary>
    /// <param name="pixels">The BGRA page.</param>
    /// <param name="width">The page width.</param>
    /// <param name="x">The glyph cell's left edge.</param>
    /// <param name="y">The text line's top edge.</param>
    /// <returns>Whether the cell contains ink.</returns>
    private static bool HasInk(ReadOnlySpan<byte> pixels, int width, int x, int y)
    {
        for (var row = y; row < y + CjkLineHeight; row++)
        {
            for (var column = x; column < x + CjkGlyphWidth; column++)
            {
                if (pixels[((row * width) + column) * BytesPerPixel] < InkThreshold)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Renders a page with both engines and asserts the images are close.</summary>
    /// <param name="name">The test name.</param>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="threshold">The largest mean difference accepted.</param>
    /// <returns>A task.</returns>
    private static async Task AssertParity(string name, byte[] pdf, double threshold)
    {
        using var input = new HyperPdfTestDocument(pdf);
        await input.Document.PreparePageAsync(0, CancellationToken.None);
        var difference = Compare(input, new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), out var images);
        TestContext.Current?.Output.WriteLine($"{name}: mean difference {difference:F3}");
        if (difference >= threshold)
        {
            images.Save(name);
        }

        await Assert.That(difference).IsLessThan(threshold);
    }

    /// <summary>Renders a page with both engines.</summary>
    /// <param name="input">The comparison PDF and HyperPDF document.</param>
    /// <param name="info">The page request.</param>
    /// <param name="images">The two renders.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    /// <exception cref="InvalidOperationException">An engine could not render the page.</exception>
    private static double Compare(HyperPdfTestDocument input, in PageRenderInfo info, out ParityImages images)
    {
        var reference = PdfiumReferenceRendering.Render(input.Pdf, info);
        var width = reference.Width;
        var height = reference.Height;
        var expected = reference.Pixels.ToArray();
        var actual = new byte[expected.Length];
        if (!input.Document.Render(info, new(actual, width, height, width * BytesPerPixel)))
        {
            throw new InvalidOperationException("The page could not be rendered.");
        }

        images = new(expected, actual, width, height);
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Tests.Fonts.Embedding;
using HyperPdfLibrary.Text;

namespace HyperPdf.Engine.Tests;

/// <summary>Fonts embedded through the library extract, search and draw the same in HyperPDF and in PDFium.</summary>
[NotInParallel]
public sealed class EmbeddedFontParityTests
{
    /// <summary>The licence flag that forbids subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The bytes in a rendered pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The largest mean difference per channel accepted between two renderers.</summary>
    private const double RenderThreshold = 1.5;

    /// <summary>The largest mean difference per channel accepted between a subset and the whole font in one renderer.</summary>
    private const double SubsetThreshold = 0.05;

    /// <summary>The advance of a test glyph, in ems.</summary>
    private const float Advance = 0.5F;

    /// <summary>The advance of a ligature glyph, in ems.</summary>
    private const float LigatureAdvance = 0.8F;

    /// <summary>A line with capitals, a space and a composite glyph.</summary>
    private const string Line = "Hello World é";

    /// <summary>The Hebrew letters alef and bet.</summary>
    private const string AlefBet = "אב";

    /// <summary>Text with capitals, a composite glyph, Greek and Cyrillic extracts and searches identically in both engines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAndSearchMatchPdfium()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), PdfEmbeddedFontOptions.Default, Line, "Greek αβ Cyrillic Ж");
        using var pair = new EnginePair(pdf);

        var expected = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0);
        var actual = PdfDocumentText.GetTextPage(pair.Library, 0);
        var matches = new List<PdfTextMatch>();
        actual.Find("world", PdfTextSearchOptions.None, matches);
        var reference = PdfiumReferenceText.Find(pair.Pdfium, 0, "world", PdfiumSearchFlags.None);

        await Assert.That(expected.Text).Contains(Line);
        await Assert.That(actual.Text).IsEqualTo(expected.Text);
        await Assert.That(reference).Count().IsEqualTo(1);
        await Assert.That(matches).Count().IsEqualTo(1);
        await Assert.That(matches[0].Start).IsEqualTo(reference[0].Start);
        await Assert.That(matches[0].Count).IsEqualTo(reference[0].Length);
    }

    /// <summary>A ligature and a reordered cluster extract as their characters in both engines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigatureAndReorderedClusterMatchPdfium()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source)
            .Script("ffi", false, new PdfShapedGlyph(TestFont.GlyphOf('W'), 0, LigatureAdvance, 0, 0))
            .Script("ka", false, new PdfShapedGlyph(TestFont.GlyphOf('a'), 0, Advance, 0, 0), new PdfShapedGlyph(TestFont.GlyphOf('k'), 0, Advance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, "ffi", "ka");
        using var pair = new EnginePair(pdf);

        var expected = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0).Text;
        var actual = PdfDocumentText.GetTextPage(pair.Library, 0).Text;

        await Assert.That(expected).Contains("ffi");
        await Assert.That(expected).Contains("ka");
        await Assert.That(actual).IsEqualTo(expected);
    }

    /// <summary>A right-to-left run extracts in reading order in both engines, so the logical text is found by search.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightToLeftRunMatchesPdfium()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script(AlefBet, true, new PdfShapedGlyph(TestFont.GlyphOf('ב'), 1, Advance, 0, 0), new PdfShapedGlyph(TestFont.GlyphOf('א'), 0, Advance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, AlefBet);
        using var pair = new EnginePair(pdf);

        var expected = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0).Text;
        var actual = PdfDocumentText.GetTextPage(pair.Library, 0).Text;
        TestContext.Current?.Output.WriteLine($"PDFium: {expected}; HyperPDF: {actual}");

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(expected).Contains(AlefBet);
    }

    /// <summary>An embedded subset draws like PDFium draws it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsetRenderMatchesPdfium()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), PdfEmbeddedFontOptions.Default, Line, "Greek αβ Cyrillic Ж");
        using var input = new HyperPdfTestDocument(pdf);
        await input.Document.PreparePageAsync(0, CancellationToken.None);
        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.None);
        var reference = PdfiumReferenceRendering.Render(pdf, info);
        var actual = new byte[reference.Pixels.Length];
        await Assert.That(input.Document.Render(info, new(actual, reference.Width, reference.Height, reference.Width * BytesPerPixel))).IsTrue();

        await Assert.That(MeanDifference(reference.Pixels.ToArray(), actual)).IsLessThan(RenderThreshold);
    }

    /// <summary>PDFium draws a subset exactly as it draws the whole font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfiumDrawsSubsetLikeWholeFont()
    {
        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.None);
        var subset = PdfiumReferenceRendering.Render(EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), PdfEmbeddedFontOptions.Default, Line), info);
        var whole = PdfiumReferenceRendering.Render(EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(NoSubsetting, null, false), 0), PdfEmbeddedFontOptions.Default, Line), info);

        await Assert.That(MeanDifference(subset.Pixels.ToArray(), whole.Pixels.ToArray())).IsLessThan(SubsetThreshold);
    }

    /// <summary>Gets the mean absolute difference per byte of two renders.</summary>
    /// <param name="expected">The first render.</param>
    /// <param name="actual">The second render, the same size.</param>
    /// <returns>The mean difference.</returns>
    private static double MeanDifference(byte[] expected, byte[] actual)
    {
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}

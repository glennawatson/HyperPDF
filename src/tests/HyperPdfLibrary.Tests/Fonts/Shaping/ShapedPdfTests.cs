// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets.Shaping;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Fonts.Embedding;
using HyperPdfLibrary.Tests.Optimizing;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Text shaped by the font's own tables, embedded in a document: it extracts as typed, with <c>/ActualText</c> where glyphs do not map one to one.</summary>
[NotInParallel]
public sealed class ShapedPdfTests
{
    /// <summary>The lookup type of a ligature substitution.</summary>
    private const int Ligature = 0x4;

    /// <summary>A ligature the font forms is shown with one glyph, wrapped in <c>/ActualText</c>, and extracts as the letters typed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigatureFromTheFontExtractsAsTyped()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("fix");
        _ = fixture.AddMapped('F', 0, ShapingFixture.Advance);
        fixture.UseGsub("liga", fixture.Gsub.AddLookup(Ligature, 0, GsubSubtables.Ligature(fixture.Id('f'), new LigatureRule(fixture.Id('F'), [fixture.Id('i')]))));

        var pdf = Embed(fixture, "fix");

        await Assert.That(Content(pdf)).Contains("/ActualText (fi)");
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("fix");
    }

    /// <summary>A reordered Indic syllable is wrapped in <c>/ActualText</c> and extracts in text order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IndicSyllableExtractsInTextOrder()
    {
        var pdf = Embed(IndicShapingTests.Fixture(true, true, true), "कि");

        await Assert.That(Content(pdf)).Contains("/ActualText");
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("कि");
    }

    /// <summary>Joined Arabic letters extract as the letters typed, in reading order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArabicWordExtractsAsTyped()
    {
        var pdf = Embed(ArabicShapingTests.Fixture(), "ببا");

        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("ببا");
    }

    /// <summary>The embedded subset keeps the glyphs the shaper chose, so the file is smaller than the font and still shows the text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsetKeepsOnlyTheShapedGlyphs()
    {
        var fixture = ArabicShapingTests.Fixture();
        var font = fixture.Build();
        var pdf = Embed(fixture, "با");

        using var inspector = new EmbeddedFontInspector(pdf);

        await Assert.That(inspector.TrueTypeProgram.Length).IsLessThan(font.Length);
        await Assert.That(EmbeddedFontInspector.GlyphCount(inspector.TrueTypeProgram)).IsEqualTo(0x3);
    }

    /// <summary>Embeds a line of text shaped by the font's tables.</summary>
    /// <param name="fixture">The font.</param>
    /// <param name="text">The text.</param>
    /// <returns>The document.</returns>
    private static byte[] Embed(ShapingFixture fixture, string text)
    {
        var source = EmbeddedFontPdf.Open(fixture.Build(), 0);
        var pdf = EmbeddedFontPdf.Lines(source, PdfEmbeddedFontOptions.Default, text);
        EmbeddedFontPdf.Keep($"shaped-{Guid.NewGuid():N}.pdf", pdf);
        return pdf;
    }

    /// <summary>Gets a document's page content as text.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The decoded content stream.</returns>
    private static string Content(byte[] pdf)
    {
        using var document = PdfDocumentReader.Open(pdf, null);
        var contents = PdfDocumentPages.GetPage(document, 0).Dictionary.GetStream(KnownName.Contents)!;
        return Encoding.Latin1.GetString(contents.DecodeToArray());
    }
}

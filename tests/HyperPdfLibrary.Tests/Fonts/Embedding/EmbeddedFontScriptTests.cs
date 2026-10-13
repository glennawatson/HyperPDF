// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Optimizing;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Shaped runs that do not map one glyph to one character: ligatures, reordering, right-to-left text and marks.</summary>
[NotInParallel]
public sealed class EmbeddedFontScriptTests
{
    /// <summary>The advance of a ligature glyph, in ems.</summary>
    private const float LigatureAdvance = 0.8F;

    /// <summary>The advance of a test glyph, in ems.</summary>
    private const float Advance = 0.5F;

    /// <summary>The sideways shift of a mark, in ems.</summary>
    private const float MarkShift = -0.3F;

    /// <summary>The upward shift of a mark, in ems.</summary>
    private const float MarkRise = 0.4F;

    /// <summary>The Hebrew letters alef and bet, which the test font has.</summary>
    private const string AlefBet = "אב";

    /// <summary>The combining acute accent.</summary>
    private const char CombiningAcute = (char)0x0301;

    /// <summary>The letter e followed by a combining acute accent.</summary>
    private static readonly string EAcute = $"e{CombiningAcute}";

    /// <summary>A ligature shows several characters with one glyph and extracts as those characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigatureExtractsAsItsCharacters()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script("ffi", false, Glyph('W', 0, LigatureAdvance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, "ffi", "office");
        EmbeddedFontPdf.Keep("ligature.pdf", pdf);

        using var inspector = new EmbeddedFontInspector(pdf);
        var text = OptimizerTestKit.Text(pdf)[0];

        await Assert.That(text).Contains("ffi");
        await Assert.That(inspector.ToUnicode).Contains("<006600660069>");
        await Assert.That(Content(pdf)).Contains("/ActualText (ffi)");
    }

    /// <summary>The ligature policy decides whether a one-glyph cluster is wrapped; the text extracts either way.</summary>
    /// <param name="policy">The policy.</param>
    /// <param name="wrapped">Whether the ligature is wrapped in marked content.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfActualTextPolicy.Never, false)]
    [Arguments(PdfActualTextPolicy.WhenNeeded, false)]
    [Arguments(PdfActualTextPolicy.Ligatures, true)]
    public async Task PolicyDecidesWhetherLigaturesAreWrapped(PdfActualTextPolicy policy, bool wrapped)
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script("ffi", false, Glyph('W', 0, LigatureAdvance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { ActualText = policy, Shaper = shaper }, "ffi");

        await Assert.That(Content(pdf).Contains("/ActualText", StringComparison.Ordinal)).IsEqualTo(wrapped);
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("ffi");
    }

    /// <summary>A right-to-left run, drawn in visual order, extracts in reading order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightToLeftRunExtractsInReadingOrder()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script(AlefBet, true, Glyph('ב', 1, Advance, 0, 0), Glyph('א', 0, Advance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, AlefBet);
        EmbeddedFontPdf.Keep("hebrew.pdf", pdf);

        using var document = PdfDocumentReader.Open(pdf, null);
        var page = PdfDocumentText.GetTextPage(document, 0);
        var matches = new List<PdfTextMatch>();
        page.Find(AlefBet, PdfTextSearchOptions.None, matches);

        await Assert.That(page.Text).Contains(AlefBet);
        await Assert.That(matches).Count().IsEqualTo(1);
    }

    /// <summary>Glyphs reordered inside one cluster, as an Indic syllable is, extract in logical order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderedClusterExtractsInLogicalOrder()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script("ka", false, Glyph('a', 0, Advance, 0, 0), Glyph('k', 0, Advance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, "ka", "x");

        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("ka");
        await Assert.That(Content(pdf)).Contains("/ActualText (ka)");
    }

    /// <summary>A mark placed by an offset is drawn with a text rise and an adjustment, and its base and mark extract together.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkOffsetsBecomeRiseAndAdjustment()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script(EAcute, false, Glyph('e', 0, Advance, 0, 0), Glyph('´', 0, 0, MarkShift, MarkRise));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, EAcute);

        var content = Content(pdf);

        await Assert.That(content).Contains("4.8 Ts");
        await Assert.That(content).Contains("/ActualText");
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains(EAcute);
    }

    /// <summary>A glyph that stands for different text in different places keeps its first text in /ToUnicode and is wrapped elsewhere.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GlyphWithTwoMeaningsIsWrappedTheSecondTime()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var shaper = new ScriptedShaper(source).Script("Z", false, Glyph('A', 0, Advance, 0, 0));
        var pdf = EmbeddedFontPdf.Lines(source, new PdfEmbeddedFontOptions { Shaper = shaper }, "A", "Z");

        var text = OptimizerTestKit.Text(pdf)[0];

        await Assert.That(text).Contains("A");
        await Assert.That(text).Contains("Z");
        await Assert.That(Content(pdf)).Contains("/ActualText (Z)");
    }

    /// <summary>Makes a shaped glyph of a test font character.</summary>
    /// <param name="c">The character whose glyph is used.</param>
    /// <param name="cluster">The cluster.</param>
    /// <param name="advance">The advance in ems.</param>
    /// <param name="offsetX">The sideways offset in ems.</param>
    /// <param name="offsetY">The upward offset in ems.</param>
    /// <returns>The glyph.</returns>
    private static PdfShapedGlyph Glyph(char c, int cluster, float advance, float offsetX, float offsetY) =>
        new(TestFont.GlyphOf(c), cluster, advance, offsetX, offsetY);

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

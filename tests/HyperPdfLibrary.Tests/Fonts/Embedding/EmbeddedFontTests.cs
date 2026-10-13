// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Optimizing;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Embedding a TrueType font as a Type 0 font: subsetting, text, licence flags and collections.</summary>
[NotInParallel]
public sealed class EmbeddedFontTests
{
    /// <summary>The licence flag for restricted embedding.</summary>
    private const int Restricted = 0x0002;

    /// <summary>The licence flag for bitmap embedding only.</summary>
    private const int BitmapOnly = 0x0200;

    /// <summary>The licence flag that forbids subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The licence flag for preview and print embedding.</summary>
    private const int PreviewAndPrint = 0x0004;

    /// <summary>The descriptor flag for a nonsymbolic font.</summary>
    private const int NonsymbolicFlag = 32;

    /// <summary>The size of a file of zeros that is not a font.</summary>
    private const int NotFontBytes = 64;

    /// <summary>The faces in the test collection.</summary>
    private const int CollectionFaces = 2;

    /// <summary>The glyphs of the composite test: the missing glyph, the composite, 'e' and the accent.</summary>
    private const int CompositeGlyphs = 4;

    /// <summary>The mean pixel difference two renders of the same text may have.</summary>
    private const double RenderTolerance = 0.01;

    /// <summary>A line of text with capitals, a composite glyph and a space.</summary>
    private const string Line = "Hello World é";

    /// <summary>Text shown with an embedded font extracts as typed and can be found.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextExtractsAndSearches()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), PdfEmbeddedFontOptions.Default, Line, "Greek αβ Cyrillic Ж");
        EmbeddedFontPdf.Keep("truetype-text.pdf", pdf);

        using var document = PdfDocumentReader.Open(pdf, null);
        var page = PdfDocumentText.GetTextPage(document, 0);
        var matches = new List<PdfTextMatch>();
        page.Find("world", PdfTextSearchOptions.None, matches);

        await Assert.That(page.Text).Contains(Line);
        await Assert.That(page.Text).Contains("Greek αβ Cyrillic Ж");
        await Assert.That(matches).Count().IsEqualTo(1);
        await Assert.That(page.GetText(matches[0].Start, matches[0].Count)).IsEqualTo("World");
    }

    /// <summary>The program is cut to the glyphs used, numbered from zero, and the font is tagged and described.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsetIsSmallerAndTagged()
    {
        var original = TestFont.Create();
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(original, 0), PdfEmbeddedFontOptions.Default, "Hello é");

        using var inspector = new EmbeddedFontInspector(pdf);
        var program = inspector.TrueTypeProgram;
        var distinct = "Hello é".Where(static c => c != ' ').Distinct().Count() + 1;

        await Assert.That(program.Length).IsLessThan(original.Length);
        await Assert.That(EmbeddedFontInspector.GlyphCount(program)).IsGreaterThanOrEqualTo(distinct);
        await Assert.That(EmbeddedFontInspector.GlyphCount(program)).IsLessThan(TestFont.Characters.Length);
        await Assert.That(inspector.BaseFont).Matches("^[A-Z]{6}\\+PVLTestSans.*$");
        await Assert.That(inspector.Descendant.IsName(KnownName.Subtype, KnownName.CIDFontType2)).IsTrue();
        await Assert.That(inspector.Type0.IsName(KnownName.Encoding, KnownName.IdentityH)).IsTrue();
        await Assert.That(inspector.Descendant.IsName(KnownName.CIDToGIDMap, KnownName.Identity)).IsTrue();
        await Assert.That(inspector.Descriptor.GetStream(KnownName.CIDSet)).IsNotNull();
        await Assert.That(inspector.Descriptor.GetInt32(KnownName.Flags) & NonsymbolicFlag).IsEqualTo(NonsymbolicFlag);
        await Assert.That(inspector.ToUnicode).Contains("beginbfchar");
    }

    /// <summary>The same document is identical every time, so the tag depends only on the font and glyphs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TagIsReproducible()
    {
        var source = EmbeddedFontPdf.Open(TestFont.Create(), 0);
        var first = EmbeddedFontPdf.Lines(source, PdfEmbeddedFontOptions.Default, Line);
        var second = EmbeddedFontPdf.Lines(source, PdfEmbeddedFontOptions.Default, Line);

        await Assert.That(second).IsEquivalentTo(first);
    }

    /// <summary>A subset draws the same pixels as the whole font embedded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubsetRendersLikeTheWholeFont()
    {
        var subset = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), PdfEmbeddedFontOptions.Default, Line);
        var whole = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(NoSubsetting, null, false), 0), PdfEmbeddedFontOptions.Default, Line);

        var difference = OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(subset), OptimizerTestKit.Render(whole));

        await Assert.That(difference).IsLessThan(RenderTolerance);
        await Assert.That(OptimizerTestKit.Text(subset)[0]).IsEqualTo(OptimizerTestKit.Text(whole)[0]);
    }

    /// <summary>A font that forbids subsetting is embedded whole, with a map from CIDs to its glyphs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoSubsettingEmbedsTheWholeFont()
    {
        var original = TestFont.Create(NoSubsetting, null, false);
        var source = EmbeddedFontPdf.Open(original, 0);
        var pdf = EmbeddedFontPdf.Lines(source, PdfEmbeddedFontOptions.Default, Line);

        using var inspector = new EmbeddedFontInspector(pdf);

        await Assert.That(source.CanSubset).IsFalse();
        await Assert.That(inspector.TrueTypeProgram.Length).IsEqualTo(original.Length);
        await Assert.That(inspector.BaseFont).DoesNotMatch("^[A-Z]{6}\\+.*$");
        await Assert.That(inspector.Descendant.GetStream(KnownName.CIDToGIDMap)).IsNotNull();
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains(Line);
    }

    /// <summary>Restricted and bitmap-only licences are refused with a plain reason; preview and print is accepted.</summary>
    /// <param name="flags">The licence flags.</param>
    /// <param name="refused">Whether the font is refused.</param>
    /// <param name="reason">Part of the reason.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Restricted, true, "restricted")]
    [Arguments(BitmapOnly, true, "bitmaps")]
    [Arguments(PreviewAndPrint, false, "")]
    [Arguments(0, false, "")]
    public async Task LicenceDecidesWhetherAFontIsRefused(int flags, bool refused, string reason)
    {
        var result = PdfFontSource.Open(TestFont.Create(flags, null, false));

        await Assert.That(result.IsEmbeddable).IsEqualTo(!refused);
        await Assert.That(result.Refusal ?? string.Empty).Contains(reason);
    }

    /// <summary>Data that is not a font, a missing collection face and a font without outlines are refused with a reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableFilesAreRefused()
    {
        var notFont = PdfFontSource.Open(new byte[NotFontBytes]);
        var missingFace = PdfFontSource.Open(TestFont.Create(), 1);

        await Assert.That(notFont.IsEmbeddable).IsFalse();
        await Assert.That(notFont.Refusal).Contains("not a TrueType or OpenType");
        await Assert.That(missingFace.IsEmbeddable).IsFalse();
        await Assert.That(missingFace.Refusal).Contains("only face 0");
    }

    /// <summary>Another face of a collection is pulled out as a font of its own and embedded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CollectionFaceIsEmbedded()
    {
        var collection = TestFontCollection.Create(TestFont.Create(), TestFont.Create(0, "PVL Second Sans", true));
        var second = EmbeddedFontPdf.Open(collection, 1);
        var missing = PdfFontSource.Open(collection, CollectionFaces);
        var pdf = EmbeddedFontPdf.Lines(second, PdfEmbeddedFontOptions.Default, Line);

        using var inspector = new EmbeddedFontInspector(pdf);

        await Assert.That(second.FaceCount).IsEqualTo(CollectionFaces);
        await Assert.That(second.FaceIndex).IsEqualTo(1);
        await Assert.That(second.FamilyName).IsEqualTo("PVL Second Sans");
        await Assert.That(second.IsBold).IsTrue();
        await Assert.That(missing.Refusal).Contains("has 2 faces");
        await Assert.That(inspector.BaseFont).Contains("PVLSecondSans");
        await Assert.That(EmbeddedFontInspector.GlyphCount(inspector.TrueTypeProgram)).IsGreaterThan(1);
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains(Line);
    }

    /// <summary>A composite glyph keeps its parts when subset, and a glyph the font lacks draws the missing glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompositeGlyphsKeepTheirParts()
    {
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestFont.Create(), 0), PdfEmbeddedFontOptions.Default, "é☃");

        using var inspector = new EmbeddedFontInspector(pdf);

        // .notdef, the composite, 'e' and the accent; the snowman has no glyph and is drawn with .notdef.
        await Assert.That(EmbeddedFontInspector.GlyphCount(inspector.TrueTypeProgram)).IsEqualTo(CompositeGlyphs);
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("é");
    }

    /// <summary>Fonts are finished together and then reject glyphs they have not used.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FinishedFontRejectsNewGlyphs()
    {
        var builder = new HyperPdfLibrary.Writing.PdfDocumentBuilder();
        var font = PdfEmbeddedFont.Create(builder, EmbeddedFontPdf.Open(TestFont.Create(), 0));
        var code = font.GetCode(TestFont.GlyphOf('A'));
        PdfEmbeddedFont.FinishAll(builder);

        await Assert.That(font.IsFinished).IsTrue();
        await Assert.That(font.GetCode(TestFont.GlyphOf('A'))).IsEqualTo(code);
        await Assert.That(() => font.GetCode(TestFont.GlyphOf('B'))).Throws<InvalidOperationException>();
    }
}

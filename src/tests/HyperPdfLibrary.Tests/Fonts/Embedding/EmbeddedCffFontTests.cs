// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Optimizing;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Embedding fonts with Compact Font Format outlines: subsetting, text, licences and the CFF the subsetter writes.</summary>
[NotInParallel]
public sealed class EmbeddedCffFontTests
{
    /// <summary>The licence flag that forbids subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The licence flag for restricted embedding.</summary>
    private const int Restricted = 0x0002;

    /// <summary>The mean pixel difference two renders of the same text may have.</summary>
    private const double RenderTolerance = 0.01;

    /// <summary>A font name as the planner writes it, with a subset tag.</summary>
    private const string FontName = "ABCDEF+Test";

    /// <summary>The place of the first shown glyph in a subset, after .notdef.</summary>
    private const int FirstShown = 1;

    /// <summary>The place of the second shown glyph in a subset.</summary>
    private const int SecondShown = 2;

    /// <summary>The length of a subroutine that is only a <c>return</c>.</summary>
    private const int BareReturn = 1;

    /// <summary>A line of text with capitals, a composite glyph and a space.</summary>
    private const string Line = "Hello World é";

    /// <summary>The glyphs of the accent test: .notdef, the accented glyph, its base and its accent.</summary>
    private const int AccentGlyphs = 4;

    /// <summary>Text shown with an embedded CFF font extracts as typed and can be found.</summary>
    /// <param name="cidKeyed">Whether the font is CID-keyed.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task TextExtractsAndSearches(bool cidKeyed)
    {
        var font = TestCffFont.Create(new TestCffOptions { CidKeyed = cidKeyed });
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(font, 0), PdfEmbeddedFontOptions.Default, Line, "Digits 0123");
        EmbeddedFontPdf.Keep(cidKeyed ? "cff-cid-text.pdf" : "cff-text.pdf", pdf);

        using var document = PdfDocumentReader.Open(pdf, null);
        var page = PdfDocumentText.GetTextPage(document, 0);
        var matches = new List<PdfTextMatch>();
        page.Find("world", PdfTextSearchOptions.None, matches);

        await Assert.That(page.Text).Contains(Line);
        await Assert.That(page.Text).Contains("Digits 0123");
        await Assert.That(matches).Count().IsEqualTo(1);
    }

    /// <summary>The subset is a bare CFF font in a CIDFontType0 font, smaller than the file, tagged and described.</summary>
    /// <param name="cidKeyed">Whether the font is CID-keyed.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SubsetIsSmallerAndTagged(bool cidKeyed)
    {
        var font = TestCffFont.Create(new TestCffOptions { CidKeyed = cidKeyed });
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(font, 0), PdfEmbeddedFontOptions.Default, "Hello é");

        using var inspector = new EmbeddedFontInspector(pdf);
        var program = inspector.CompactProgram;
        var parsed = CffProgram.TryParse(program, out var cff);

        await Assert.That(parsed).IsTrue();
        await Assert.That(program.Length).IsLessThan(font.Length);
        await Assert.That(cff!.IsCidKeyed).IsEqualTo(cidKeyed);
        await Assert.That(inspector.BaseFont).Matches("^[A-Z]{6}\\+PVLTestSansCFF.*$");
        await Assert.That(inspector.Descendant.IsName(KnownName.Subtype, KnownName.CIDFontType0)).IsTrue();
        await Assert.That(inspector.Type0.IsName(KnownName.Encoding, KnownName.IdentityH)).IsTrue();
        await Assert.That(inspector.Descendant.GetStream(KnownName.CIDToGIDMap)).IsNull();

        // The font shows an accented glyph, so a name-keyed font stays name-keyed and has no CID numbers to list in a /CIDSet.
        await Assert.That(inspector.Descriptor.GetStream(KnownName.CIDSet) is not null).IsEqualTo(cidKeyed);
        await Assert.That(inspector.ToUnicode).Contains("beginbfchar");
    }

    /// <summary>A name-keyed font without accented glyphs is written CID-keyed, so its glyph numbers are its CIDs and the /CIDSet lists them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NameKeyedFontBecomesCidKeyedWithoutAccents()
    {
        var font = TestCffFont.Create();
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(font, 0), PdfEmbeddedFontOptions.Default, "Hello World");

        using var inspector = new EmbeddedFontInspector(pdf);
        var parsed = CffProgram.TryParse(inspector.CompactProgram, out var cff);

        await Assert.That(parsed).IsTrue();
        await Assert.That(cff!.IsCidKeyed).IsTrue();
        await Assert.That(inspector.Descriptor.GetStream(KnownName.CIDSet)).IsNotNull();
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains("Hello World");
    }

    /// <summary>Every glyph of a subset draws and measures as the glyph it came from, including a glyph made from two others.</summary>
    /// <param name="cidKeyed">Whether the font is CID-keyed.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SubsetGlyphsMatchTheOriginals(bool cidKeyed)
    {
        var original = CffOf(TestCffFont.Create(new TestCffOptions { CidKeyed = cidKeyed }));
        int[] shown = [TestFont.GlyphOf('H'), TestFont.GlyphOf('e'), TestFont.GlyphOf('é'), TestFont.GlyphOf('A'), TestFont.GlyphOf('5'), TestFont.GlyphOf('z'), TestFont.GlyphOf(' ')];
        var subset = CffSubsetter.Renumber(original, shown, FontName);

        await Assert.That(subset).IsNotNull();
        _ = CffProgram.TryParse(original, out var before);
        _ = CffProgram.TryParse(subset!.Data, out var after);
        await Assert.That(after!.GlyphCount).IsEqualTo(subset.Original.Length);
        for (var i = 0; i < subset.Original.Length; i++)
        {
            await Assert.That(OutlineRecorder.Record(after, i)).IsEqualTo(OutlineRecorder.Record(before!, subset.Original[i]));
            await Assert.That(after.GetAdvanceWidth(i)).IsEqualTo(before!.GetAdvanceWidth(subset.Original[i]));
        }
    }

    /// <summary>A glyph built by the accented-character form of endchar brings its base and accent along.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AccentedGlyphBringsItsParts()
    {
        var original = CffOf(TestCffFont.Create());
        var subset = CffSubsetter.Renumber(original, [TestFont.EAcuteGlyph], FontName);

        await Assert.That(subset!.Original.Length).IsEqualTo(AccentGlyphs);
        await Assert.That(subset.Original).Contains(TestFont.EGlyph);
        await Assert.That(subset.Original).Contains(TestFont.AccentGlyph);
    }

    /// <summary>Subroutines that no kept glyph calls shrink to a bare return, and called ones are kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusedSubroutinesAreCutDown()
    {
        var original = CffOf(TestCffFont.Create());
        var lower = CffSubsetter.Renumber(original, [TestFont.GlyphOf('e')], FontName);
        var upper = CffSubsetter.Renumber(original, [TestFont.GlyphOf('A')], FontName);
        _ = CffFaceReader.TryRead(lower!.Data, out var lowerFace);
        _ = CffFaceReader.TryRead(upper!.Data, out var upperFace);

        // Local subroutine 0 draws the lower case triangle; 1 is called by nothing. Global subroutine 0 draws the capital square.
        await Assert.That(lowerFace!.Dicts[0].Subrs.Get(lowerFace.Data.Span, 0).Length).IsGreaterThan(BareReturn);
        await Assert.That(lowerFace.Dicts[0].Subrs.Get(lowerFace.Data.Span, 1).Length).IsEqualTo(BareReturn);
        await Assert.That(lowerFace.GlobalSubrs.Get(lowerFace.Data.Span, 0).Length).IsEqualTo(BareReturn);
        await Assert.That(upperFace!.GlobalSubrs.Get(upperFace.Data.Span, 0).Length).IsGreaterThan(BareReturn);
        await Assert.That(upperFace.Dicts[0].Subrs.Get(upperFace.Data.Span, 0).Length).IsEqualTo(BareReturn);
    }

    /// <summary>A CID-keyed subset keeps only the Font DICTs its glyphs use and sends each glyph to the right one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CidKeyedSubsetKeepsUsedFontDicts()
    {
        var original = CffOf(TestCffFont.Create(new TestCffOptions { CidKeyed = true }));
        var first = CffSubsetter.Renumber(original, [TestFont.GlyphOf('A')], FontName);
        var both = CffSubsetter.Renumber(original, [TestFont.GlyphOf('A'), TestFont.GlyphOf('z')], FontName);
        _ = CffFaceReader.TryRead(first!.Data, out var firstFace);
        _ = CffFaceReader.TryRead(both!.Data, out var bothFace);

        await Assert.That(firstFace!.IsCid).IsTrue();
        await Assert.That(firstFace.Dicts.Length).IsEqualTo(1);
        await Assert.That(bothFace!.Dicts.Length).IsEqualTo(SecondShown);
        await Assert.That(bothFace.FdSelect[FirstShown]).IsEqualTo((byte)0);
        await Assert.That(bothFace.FdSelect[SecondShown]).IsEqualTo((byte)1);
    }

    /// <summary>Keeping glyph numbers blanks the glyphs nobody shows and leaves the others unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepingNumbersBlanksUnusedGlyphs()
    {
        var original = CffOf(TestCffFont.Create());
        var kept = CffSubsetter.KeepNumbers(original, [TestFont.GlyphOf('A')], FontName);

        _ = CffProgram.TryParse(original, out var before);
        _ = CffProgram.TryParse(kept!, out var after);

        await Assert.That(kept!.Length).IsLessThan(original.Length);
        await Assert.That(after!.GlyphCount).IsEqualTo(before!.GlyphCount);
        await Assert.That(OutlineRecorder.Record(after, TestFont.GlyphOf('A'))).IsEqualTo(OutlineRecorder.Record(before, TestFont.GlyphOf('A')));
        await Assert.That(OutlineRecorder.Record(after, TestFont.GlyphOf('B'))).IsEmpty();
        await Assert.That(after.GetGlyphByName("A"u8)).IsEqualTo(before.GetGlyphByName("A"u8));
    }

    /// <summary>A font that forbids subsetting is embedded whole as an OpenType file, shown by glyph number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoSubsettingEmbedsTheWholeOpenTypeFile()
    {
        var font = TestCffFont.Create(new TestCffOptions { EmbeddingFlags = NoSubsetting });
        var pdf = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(font, 0), PdfEmbeddedFontOptions.Default, Line);
        EmbeddedFontPdf.Keep("cff-whole.pdf", pdf);

        using var inspector = new EmbeddedFontInspector(pdf);

        await Assert.That(inspector.CompactProgram.Length).IsEqualTo(font.Length);
        await Assert.That(inspector.BaseFont).DoesNotMatch("^[A-Z]{6}\\+.*$");
        await Assert.That(OptimizerTestKit.Text(pdf)[0]).Contains(Line);
    }

    /// <summary>A subset draws the same pixels as the whole font embedded, whether the subset stays name-keyed for an accented glyph or becomes CID-keyed.</summary>
    /// <param name="line">The text shown.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Line)]
    [Arguments("Hello World")]
    public async Task SubsetRendersLikeTheWholeFont(string line)
    {
        var subset = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestCffFont.Create(), 0), PdfEmbeddedFontOptions.Default, line);
        var whole = EmbeddedFontPdf.Lines(EmbeddedFontPdf.Open(TestCffFont.Create(new TestCffOptions { EmbeddingFlags = NoSubsetting }), 0), PdfEmbeddedFontOptions.Default, line);

        var difference = OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(subset), OptimizerTestKit.Render(whole));

        await Assert.That(difference).IsLessThan(RenderTolerance);
    }

    /// <summary>CFF2 and variable fonts and fonts with a restricted licence are refused with a plain reason; plain CFF fonts are accepted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableCffFontsAreRefused()
    {
        var cff2 = PdfFontSource.Open(TestCffFont.Create(new TestCffOptions { ExtraTables = ["CFF2"] }));
        var variable = PdfFontSource.Open(TestCffFont.Create(new TestCffOptions { ExtraTables = ["fvar"] }));
        var restricted = PdfFontSource.Open(TestCffFont.Create(new TestCffOptions { EmbeddingFlags = Restricted }));
        var plain = PdfFontSource.Open(TestCffFont.Create());

        await Assert.That(cff2.Refusal).Contains("CFF2");
        await Assert.That(variable.Refusal).Contains("variable font");
        await Assert.That(restricted.Refusal).Contains("restricted");
        await Assert.That(plain.IsEmbeddable).IsTrue();
        await Assert.That(plain.Source!.Outlines).IsEqualTo(PdfFontOutlines.CompactFontFormat);
    }

    /// <summary>Reads the CFF table of a font file.</summary>
    /// <param name="font">The font file.</param>
    /// <returns>The table.</returns>
    private static byte[] CffOf(byte[] font)
    {
        _ = SfntFont.TryRead(font, 0, out var face, out _);
        return face!.Table(SfntTags.Cff).ToArray();
    }
}

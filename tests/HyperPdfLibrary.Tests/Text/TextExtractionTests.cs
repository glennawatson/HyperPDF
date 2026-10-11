// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the characters a text page extracts and the ones it generates, against PDFium's rules.</summary>
[NotInParallel]
public sealed class TextExtractionTests
{
    /// <summary>The left of the first glyph's outline: the text x of 10 plus the 0.5 point bearing.</summary>
    private const float FirstGlyphLeft = 10.5F;

    /// <summary>The baseline of the first line.</summary>
    private const float Baseline = 100;

    /// <summary>The top of the glyph outlines on the first line.</summary>
    private const float GlyphTop = 107;

    /// <summary>The bottom of the loose box: the baseline less the 0.2 em descent.</summary>
    private const float LooseBottom = 98;

    /// <summary>The top of the loose box: the baseline plus the 0.8 em ascent.</summary>
    private const float LooseTop = 108;

    /// <summary>The font size used by the content.</summary>
    private const float FontSize = 10;

    /// <summary>The invisible text render mode.</summary>
    private const int InvisibleMode = 3;

    /// <summary>The Hebrew letter alef, code 0xA0 of the test font.</summary>
    private const char Alef = (char)0x05D0;

    /// <summary>The Hebrew letter bet, code 0xA1 of the test font.</summary>
    private const char Bet = (char)0x05D1;

    /// <summary>The Hebrew letter gimel, code 0xA2 of the test font.</summary>
    private const char Gimel = (char)0x05D2;

    /// <summary>The text of the single-word tests.</summary>
    private const string Hello = "Hello";

    /// <summary>The content that shows "Hello" on the first line.</summary>
    private const string HelloContent = "BT /F1 10 Tf 10 100 Td (Hello) Tj ET";

    /// <summary>The text of the two-word tests.</summary>
    private const string HelloWorld = "Hello World";

    /// <summary>The length of "Hello".</summary>
    private const int HelloLength = 5;

    /// <summary>The index of the first character of the second line, after "Hello" and CR LF.</summary>
    private const int SecondLineFirst = 7;

    /// <summary>The index of the last character of "ffi".</summary>
    private const int LastLigaturePiece = 2;

    /// <summary>The characters of "A", an unmapped glyph and "B".</summary>
    private const int UnmappedCharCount = 3;

    /// <summary>The character index of "B" after the unmapped glyph.</summary>
    private const int CharAfterUnmapped = 2;

    /// <summary>The length of "Hello World" and of "Hello", a line break and "Line2".</summary>
    private const int TwoWordsLength = 11;

    /// <summary>The length of "Hello", CR, LF and "Line2".</summary>
    private const int TwoLinesLength = 12;

    /// <summary>The index of the joining hyphen in "hyphen-ated".</summary>
    private const int HyphenIndex = 6;

    /// <summary>The character PDFium gives a joining hyphen.</summary>
    private const char JoiningHyphen = (char)2;

    /// <summary>The page text PDFium gives a joining hyphen.</summary>
    private const char JoiningHyphenText = (char)0xFFFE;

    /// <summary>The y of the second line after a 20 point move down.</summary>
    private const float SecondBaseline = 80;

    /// <summary>The viewer x of the first glyph on a page turned a quarter: its user y.</summary>
    private const float RotatedLeft = 100;

    /// <summary>The y of the second glyph of upward text, one advance above the first.</summary>
    private const float UpwardSecondY = 105;

    /// <summary>The y of the second glyph of vertical text, one em below the first.</summary>
    private const float VerticalSecondOrigin = 181.2F;

    /// <summary>The y of the first glyph of vertical text: the pen less the 0.88 em vertical origin.</summary>
    private const float VerticalFirstOrigin = 191.2F;

    /// <summary>The tolerance for coordinates.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>Alef, bet and gimel drawn left to right, in logical order.</summary>
    private static readonly string LogicalHebrew = new([Gimel, Bet, Alef]);

    /// <summary>"abc " then alef and bet drawn left to right, in logical order.</summary>
    private static readonly string MixedLine = new(['a', 'b', 'c', ' ', Bet, Alef]);

    /// <summary>"abc " then alef, in right-to-left line order.</summary>
    private static readonly string RightToLeftLine = new([Alef, ' ', 'a', 'b', 'c']);

    /// <summary>A single show operator gives its glyphs in order, with outline and loose boxes in user space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsGlyphsInOrderWithBoxes()
    {
        var page = TextTestDocument.Extract(HelloContent);
        var first = page.GetChar(0);

        await Assert.That(page.Text).IsEqualTo(Hello);
        await Assert.That(page.CharCount).IsEqualTo(HelloLength);
        await Assert.That(first.Box.Left).IsEqualTo(FirstGlyphLeft).Within(Tolerance);
        await Assert.That(first.Box.Bottom).IsEqualTo(Baseline).Within(Tolerance);
        await Assert.That(first.Box.Top).IsEqualTo(GlyphTop).Within(Tolerance);
        await Assert.That(first.LooseBox.Bottom).IsEqualTo(LooseBottom).Within(Tolerance);
        await Assert.That(first.LooseBox.Top).IsEqualTo(LooseTop).Within(Tolerance);
        await Assert.That(first.FontSize).IsEqualTo(FontSize);
        await Assert.That(page.GetChar(1).Box.Left).IsGreaterThan(first.Box.Right);
    }

    /// <summary>A gap between two show operators on one line becomes a generated space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GapBetweenRunsGeneratesSpace()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (Hello) Tj 30 0 Td (World) Tj ET");

        await Assert.That(page.Text).IsEqualTo(HelloWorld);
        await Assert.That(page.CharCount).IsEqualTo(TwoWordsLength);
        await Assert.That(page.GetChar(HelloLength).IsGenerated).IsTrue();
    }

    /// <summary>A move to a new line generates a CR LF pair.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewLineGeneratesLineBreak()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (Hello) Tj 0 -20 Td (Line2) Tj ET");

        await Assert.That(page.Text).IsEqualTo("Hello\r\nLine2");
        await Assert.That(page.CharCount).IsEqualTo(TwoLinesLength);
        await Assert.That(page.GetChar(HelloLength).IsGenerated).IsTrue();
        await Assert.That(page.GetChar(HelloLength + 1).Unicode).IsEqualTo('\n');
        await Assert.That(page.GetChar(SecondLineFirst).Origin.Y).IsEqualTo(SecondBaseline).Within(Tolerance);
    }

    /// <summary>A wide TJ adjustment generates a space; a small one does not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KerningGapsGenerateSpaces()
    {
        var wide = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td [(Hello) -600 (World)] TJ ET");
        var narrow = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td [(Hel) 50 (lo)] TJ ET");

        await Assert.That(wide.Text).IsEqualTo(HelloWorld);
        await Assert.That(narrow.Text).IsEqualTo(Hello);
    }

    /// <summary>A hyphen ending a line joins the word to the next line, as PDFium marks it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineEndHyphenJoinsWord()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (hyphen-) Tj 0 -20 Td (ated) Tj ET");
        var hyphen = page.GetChar(HyphenIndex);

        await Assert.That(page.Text).IsEqualTo($"hyphen{JoiningHyphenText}ated");
        await Assert.That(hyphen.Kind).IsEqualTo(PdfTextCharKind.Hyphen);
        await Assert.That(hyphen.Unicode).IsEqualTo(JoiningHyphen);
    }

    /// <summary>Text drawn twice with a small offset, as fake bold is, is extracted once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingDuplicateRunIsDropped()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (Bold) Tj ET BT /F1 10 Tf 10.3 100 Td (Bold) Tj ET");

        await Assert.That(page.Text).IsEqualTo("Bold");
    }

    /// <summary>Marked content /ActualText replaces the glyphs it covers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ActualTextReplacesGlyphs()
    {
        var pdf = TextTestDocument.Create("/Span /P1 BDC BT /F1 10 Tf 10 100 Td (\\001) Tj ET EMC");
        pdf.Resources += " /Properties << /P1 << /ActualText (fi) >> >>";
        var page = TextTestDocument.Extract(pdf);

        await Assert.That(page.Text).IsEqualTo("fi");
        await Assert.That(page.GetChar(0).Kind).IsEqualTo(PdfTextCharKind.ActualText);
    }

    /// <summary>A code whose Unicode text has several characters gives one character each; a ligature character is split.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LigaturesExpand()
    {
        var multiple = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (\\001) Tj ET");
        var single = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (\\003) Tj ET");

        await Assert.That(multiple.Text).IsEqualTo("ffi");
        await Assert.That(multiple.GetChar(LastLigaturePiece).Kind).IsEqualTo(PdfTextCharKind.Normal);
        await Assert.That(single.Text).IsEqualTo("fi");
        await Assert.That(single.GetChar(0).Kind).IsEqualTo(PdfTextCharKind.Piece);
    }

    /// <summary>A glyph without Unicode keeps its character slot but adds no text, so indexes map as PDFium maps them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnmappedGlyphKeepsIndexWithoutText()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (A\\002B) Tj ET");

        await Assert.That(page.CharCount).IsEqualTo(UnmappedCharCount);
        await Assert.That(page.Text).IsEqualTo("AB");
        await Assert.That(page.GetChar(1).Kind).IsEqualTo(PdfTextCharKind.NotUnicode);
        await Assert.That(page.TextIndexFromCharIndex(CharAfterUnmapped)).IsEqualTo(1);
        await Assert.That(page.CharIndexFromTextIndex(1)).IsEqualTo(CharAfterUnmapped);
        await Assert.That(page.GetText(0, UnmappedCharCount)).IsEqualTo("AB");
    }

    /// <summary>Invisible text, such as an OCR layer, is extracted with its render mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvisibleTextIsExtracted()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 3 Tr 10 100 Td (Hidden) Tj ET");

        await Assert.That(page.Text).IsEqualTo("Hidden");
        await Assert.That(page.GetChar(0).RenderMode).IsEqualTo(InvisibleMode);
    }

    /// <summary>Boxes stay in user space on a rotated page and map to viewer space through the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotatedPageKeepsUserSpaceBoxes()
    {
        var pdf = TextTestDocument.Create(HelloContent);
        pdf.PageEntries = "/Rotate 90";
        var page = TextTestDocument.Extract(pdf);
        var viewer = page.Page.ToViewerRectangle(page.GetChar(0).Box);

        await Assert.That(page.Text).IsEqualTo(Hello);
        await Assert.That(page.GetChar(0).Box.Left).IsEqualTo(FirstGlyphLeft).Within(Tolerance);
        await Assert.That(viewer.Left).IsEqualTo(RotatedLeft).Within(Tolerance);
        await Assert.That(viewer.Bottom).IsEqualTo(FirstGlyphLeft).Within(Tolerance);
    }

    /// <summary>Text set upward by its text matrix advances up the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotatedTextAdvancesAlongItsBaseline()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 0 1 -1 0 100 100 Tm (Up) Tj ET");

        await Assert.That(page.Text).IsEqualTo("Up");
        await Assert.That(page.GetChar(1).Origin.Y).IsEqualTo(UpwardSecondY).Within(Tolerance);
    }

    /// <summary>Vertical writing advances down the page from the vertical origin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalTextAdvancesDown()
    {
        var page = TextTestDocument.Extract("BT /F2 10 Tf 100 200 Td (AB) Tj ET");

        await Assert.That(page.Text).IsEqualTo("AB");
        await Assert.That(page.GetChar(0).Origin.Y).IsEqualTo(VerticalFirstOrigin).Within(Tolerance);
        await Assert.That(page.GetChar(1).Origin.Y).IsEqualTo(VerticalSecondOrigin).Within(Tolerance);
    }

    /// <summary>Hebrew drawn left to right is extracted in logical, right-to-left order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightToLeftTextIsInLogicalOrder()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (\\240\\241\\242) Tj ET");

        await Assert.That(page.Text).IsEqualTo(LogicalHebrew);
        await Assert.That(page.GetChar(0).Kind).IsEqualTo(PdfTextCharKind.Piece);
    }

    /// <summary>A right-to-left word after left-to-right text keeps the line order and reverses only itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedDirectionReversesOnlyRightToLeftWords()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (abc \\240\\241) Tj ET");

        await Assert.That(page.Text).IsEqualTo(MixedLine);
    }

    /// <summary>The document's right-to-left viewer preference puts a line's runs in right-to-left order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightToLeftPreferenceOrdersTheLine()
    {
        var pdf = TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (abc \\240) Tj ET");
        pdf.CatalogEntries = "/ViewerPreferences << /Direction /R2L >>";
        var page = TextTestDocument.Extract(pdf);

        await Assert.That(page.Text).IsEqualTo(RightToLeftLine);
    }

    /// <summary>Runs on one line are put in left-to-right order, whatever order the content draws them in.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunsOnALineAreSortedByPosition()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 40 100 Td (World) Tj ET BT /F1 10 Tf 10 100 Td (Hello) Tj ET");

        await Assert.That(page.Text).IsEqualTo(HelloWorld);
    }

    /// <summary>A bold font marks its characters bold.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BoldFontIsReported()
    {
        var page = TextTestDocument.Extract("BT /F3 10 Tf 10 100 Td (B) Tj ET");

        await Assert.That(page.GetChar(0).IsBold).IsTrue();
    }

    /// <summary>A page without text has no characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyPageHasNoText()
    {
        var page = TextTestDocument.Extract("0 0 10 10 re f");

        await Assert.That(page.CharCount).IsEqualTo(0);
        await Assert.That(page.GetText(0, 1)).IsEqualTo(string.Empty);
    }

    /// <summary>Sequential extractions reuse the document's cleaned build state.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The first extraction did not return its state.</exception>
    [Test]
    public async Task ReusesDocumentStateAndClearsBuffers()
    {
        using var document = TextTestDocument.Open(TextTestDocument.Create(HelloContent).ToBytes());
        var first = PdfDocumentText.ExtractText(document, 0);
        var state = document.State.TextBuild ?? throw new InvalidOperationException("The first extraction did not return its state.");
        var retainedItems = state.Chars.Count + state.Temp.Count + state.Text.Count + state.TempText.Count
            + state.Line.Count + state.Segments.Count + state.SpaceCodes.Count + state.Runs.Count
            + state.Glyphs.Count + state.Unicode.Count;

        var second = PdfDocumentText.ExtractText(document, 0);
        var reusedAfterSecond = ReferenceEquals(state, document.State.TextBuild);
        var clearAfterSecond = state.Chars.Count + state.Temp.Count + state.Runs.Count == 0;

        await Assert.That(first.Text).IsEqualTo(Hello);
        await Assert.That(second.Text).IsEqualTo(Hello);
        await Assert.That(reusedAfterSecond && retainedItems == 0 && clearAfterSecond).IsTrue();
    }
}

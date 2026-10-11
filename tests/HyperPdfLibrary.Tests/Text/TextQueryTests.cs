// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks hit testing, rectangles, search and web links on extracted text.</summary>
[NotInParallel]
public sealed class TextQueryTests
{
    /// <summary>Two lines of text, the second starting with the first word again.</summary>
    private const string TwoLines = "BT /F1 10 Tf 10 100 Td (Hello World) Tj 0 -20 Td (hello again) Tj ET";

    /// <summary>The text index where the second line starts, after "Hello World" and CR LF.</summary>
    private const int SecondLineStart = 13;

    /// <summary>The word searched for.</summary>
    private const string Word = "hello";

    /// <summary>The length of "hello".</summary>
    private const int WordLength = 5;

    /// <summary>The rectangles of two lines.</summary>
    private const int LineCount = 2;

    /// <summary>The matches of "aa" in "aaaa" when each match starts after the last.</summary>
    private const int SeparateMatches = 2;

    /// <summary>The index of "World".</summary>
    private const int WorldStart = 6;

    /// <summary>The length from "World" to the end of "hello" on the next line.</summary>
    private const int AcrossLinesLength = 12;

    /// <summary>The x inside the third glyph of a line starting at 10.</summary>
    private const float ThirdGlyphX = 22;

    /// <summary>The x in the gap between the first two glyphs.</summary>
    private const float GapX = 15;

    /// <summary>The y inside the glyphs of the first line.</summary>
    private const float GlyphY = 103;

    /// <summary>A hit-test tolerance that reaches across the gap between glyphs.</summary>
    private const float HitTolerance = 2;

    /// <summary>A point far from any text.</summary>
    private const float FarAway = 250;

    /// <summary>The index of the third glyph.</summary>
    private const int ThirdGlyph = 2;

    /// <summary>The start of the web address in "Visit www.example.com today".</summary>
    private const int WebStart = 6;

    /// <summary>The length of "www.example.com".</summary>
    private const int WebLength = 15;

    /// <summary>The start of the email address in "mail me@example.org now".</summary>
    private const int MailStart = 5;

    /// <summary>The length of "me@example.org".</summary>
    private const int MailLength = 14;

    /// <summary>The matches of "aa" in "aaaa" when matches may overlap.</summary>
    private const int OverlappingMatches = 3;

    /// <summary>The queries measured for allocation.</summary>
    private const int Repeats = 8;

    /// <summary>The capacity given to output lists, so adding results does not grow them.</summary>
    private const int OutputCapacity = 64;

    /// <summary>The right edge of "Hello World": ten 5 point advances and a 2.5 point space from 10, less the 0.5 point bearing.</summary>
    private const float LineRight = 62;

    /// <summary>The tolerance for coordinates.</summary>
    private const float Tolerance = 0.001F;

    /// <summary>A point in a glyph hits it; a point between glyphs hits nothing without a tolerance and the nearest one with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitTestingFindsGlyphs()
    {
        var page = TextTestDocument.Extract(TwoLines);

        await Assert.That(page.GetIndexAtPosition(new(ThirdGlyphX, GlyphY), 0, 0)).IsEqualTo(ThirdGlyph);
        await Assert.That(page.GetIndexAtPosition(new(GapX, GlyphY), 0, 0)).IsEqualTo(-1);
        await Assert.That(page.GetIndexAtPosition(new(GapX, GlyphY), HitTolerance, HitTolerance)).IsEqualTo(0);
        await Assert.That(page.GetIndexAtPosition(new(FarAway, FarAway), HitTolerance, HitTolerance)).IsEqualTo(-1);
    }

    /// <summary>Rectangles join the glyph boxes of each run and leave out spaces and line breaks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RectanglesCoverEachRun()
    {
        var page = TextTestDocument.Extract(TwoLines);
        var rects = new List<PdfRectangle>();
        page.GetRects(0, page.CharCount, rects);

        await Assert.That(rects.Count).IsEqualTo(LineCount);
        await Assert.That(rects[0].Right).IsEqualTo(LineRight).Within(Tolerance);
    }

    /// <summary>Callback queries return the same rectangles and matches without an intermediate list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CallbackQueriesMatchListQueriesWithoutAllocating()
    {
        var page = TextTestDocument.Extract(TwoLines);
        var rects = new List<PdfRectangle>(OutputCapacity);
        var matches = new List<PdfTextMatch>(OutputCapacity);
        page.GetRects(0, page.CharCount, rects);
        page.Find(Word, PdfTextSearchOptions.None, matches);

        var visitedRects = new List<PdfRectangle>(OutputCapacity);
        var visitedMatches = new List<PdfTextMatch>(OutputCapacity);
        CallbackQuery(page, visitedRects, visitedMatches);
        await Assert.That(visitedRects).IsEquivalentTo(rects);
        await Assert.That(visitedMatches).IsEquivalentTo(matches);

        CallbackQuery(page, visitedRects, visitedMatches);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Repeats; i++)
        {
            CallbackQuery(page, visitedRects, visitedMatches);
        }

        await Assert.That(GC.GetAllocatedBytesForCurrentThread() - before).IsEqualTo(0L);
    }

    /// <summary>Search ignores case by default, can match case and whole words, and crosses line breaks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchFollowsOptions()
    {
        var page = TextTestDocument.Extract(TwoLines);

        await Assert.That(Find(page, Word, PdfTextSearchOptions.None)).IsEquivalentTo([new PdfTextMatch(0, WordLength), new PdfTextMatch(SecondLineStart, WordLength)]);
        await Assert.That(Find(page, Word, PdfTextSearchOptions.MatchCase)).IsEquivalentTo([new PdfTextMatch(SecondLineStart, WordLength)]);
        await Assert.That(Find(page, "hell", PdfTextSearchOptions.WholeWord)).IsEmpty();
        await Assert.That(Find(page, "world hello", PdfTextSearchOptions.None)).IsEquivalentTo([new PdfTextMatch(WorldStart, AcrossLinesLength)]);
    }

    /// <summary>Consecutive search finds overlapping matches.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConsecutiveSearchOverlaps()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (aaaa) Tj ET");

        await Assert.That(Find(page, "aa", PdfTextSearchOptions.None).Count).IsEqualTo(SeparateMatches);
        await Assert.That(Find(page, "aa", PdfTextSearchOptions.Consecutive).Count).IsEqualTo(OverlappingMatches);
    }

    /// <summary>Web and email addresses written as text become links.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WebLinksAreFound()
    {
        var web = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (Visit www.example.com today) Tj ET").GetWebLinks();
        var mail = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (mail me@example.org now) Tj ET").GetWebLinks();

        await Assert.That(web).IsEquivalentTo([new PdfWebLink("http://www.example.com", WebStart, WebLength)]);
        await Assert.That(mail).IsEquivalentTo([new PdfWebLink("mailto:me@example.org", MailStart, MailLength)]);
    }

    /// <summary>The document keeps text pages, so asking twice gives the same page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextPagesAreCached()
    {
        var document = TextTestDocument.Open(TextTestDocument.Create(TwoLines).ToBytes());

        await Assert.That(HyperPdfLibrary.Document.PdfDocumentText.GetTextPage(document, 0)).IsSameReferenceAs(HyperPdfLibrary.Document.PdfDocumentText.GetTextPage(document, 0));
    }

    /// <summary>Counting, hit testing, rectangles, text spans and search allocate nothing once warm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task QueriesDoNotAllocate()
    {
        var page = TextTestDocument.Extract(TwoLines);
        var rects = new List<PdfRectangle>(OutputCapacity);
        var matches = new List<PdfTextMatch>(OutputCapacity);
        _ = Query(page, rects, matches);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0;
        for (var i = 0; i < Repeats; i++)
        {
            total += Query(page, rects, matches);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(total).IsGreaterThan(0);
        await Assert.That(allocated).IsEqualTo(0L);
    }

    /// <summary>Runs every query once.</summary>
    /// <param name="page">The page.</param>
    /// <param name="rects">The rectangle list, cleared first.</param>
    /// <param name="matches">The match list, cleared first.</param>
    /// <returns>A value that depends on every result.</returns>
    private static int Query(PdfTextPage page, List<PdfRectangle> rects, List<PdfTextMatch> matches)
    {
        rects.Clear();
        matches.Clear();
        page.GetRects(0, page.CharCount, rects);
        page.Find(Word, PdfTextSearchOptions.None, matches);
        var hit = page.GetIndexAtPosition(new(ThirdGlyphX, GlyphY), HitTolerance, HitTolerance);
        var text = page.GetTextSpan(0, WordLength);
        return page.CharCount + hit + text.Length + rects.Count + matches.Count + page.TextIndexFromCharIndex(ThirdGlyph);
    }

    /// <summary>Runs both callback queries with caller-owned result lists.</summary>
    /// <param name="page">The page.</param>
    /// <param name="rects">The rectangle list.</param>
    /// <param name="matches">The match list.</param>
    private static void CallbackQuery(PdfTextPage page, List<PdfRectangle> rects, List<PdfTextMatch> matches)
    {
        rects.Clear();
        matches.Clear();
        page.GetRects(0, page.CharCount, rects, static (output, rect) => output.Add(rect));
        page.Find(Word, PdfTextSearchOptions.None, matches, static (output, start, count) => output.Add(new(start, count)));
    }

    /// <summary>Finds every match of a query.</summary>
    /// <param name="page">The page.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <returns>The matches.</returns>
    private static List<PdfTextMatch> Find(PdfTextPage page, string query, PdfTextSearchOptions options)
    {
        var matches = new List<PdfTextMatch>();
        page.Find(query, options, matches);
        return matches;
    }
}

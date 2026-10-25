// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>
/// Tests for glyph-aware font fallback across <see cref="PdfCanvasFont"/>, <see cref="PdfPageCanvas"/>,
/// <see cref="PdfTextWrapper"/>, <see cref="PdfTextFlow"/>, tables, rich text runs, and bundled default backup fonts.
/// </summary>
public sealed class FontFallbackTests
{
    /// <summary>Standard test font size in points.</summary>
    private const float TestFontSize = 12F;

    /// <summary>Page width in points for canvas and flow tests.</summary>
    private const float TestPageWidth = 400F;

    /// <summary>Page height in points for canvas and flow tests.</summary>
    private const float TestPageHeight = 300F;

    /// <summary>Page margin in points for flow tests.</summary>
    private const float TestMargin = 36F;

    /// <summary>Baseline X coordinate for canvas draw tests.</summary>
    private const float TestDrawX = 36F;

    /// <summary>Baseline Y coordinate for canvas draw tests.</summary>
    private const float TestDrawY = 200F;

    /// <summary>Narrow wrapping width in points to force multi-line wrapping.</summary>
    private const float NarrowWrapWidth = 70F;

    /// <summary>Tolerance for floating-point width comparisons.</summary>
    private const float WidthTolerance = 0.01F;

    /// <summary>Expected count of bundled default fallback fonts (Symbol, Dingbats, Emoji).</summary>
    private const int ExpectedDefaultFallbackCount = 3;

    /// <summary>Expected run count when splitting text with three alternating font runs.</summary>
    private const int ExpectedThreeRuns = 3;

    /// <summary>Expected run count when splitting text with five alternating font runs.</summary>
    private const int ExpectedFiveRuns = 5;

    /// <summary>Index of the second element in a list.</summary>
    private const int SecondIndex = 1;

    /// <summary>Index of the third element in a list.</summary>
    private const int ThirdIndex = 2;

    /// <summary>Index of the fourth element in a list.</summary>
    private const int FourthIndex = 3;

    /// <summary>Unicode code point for Latin capital letter A (<c>U+0041</c>).</summary>
    private const int CodePointLatinA = 0x0041;

    /// <summary>Unicode code point for N-ary summation (<c>U+2211</c>).</summary>
    private const int CodePointSummation = 0x2211;

    /// <summary>Unicode code point for party popper emoji (<c>U+1F389</c>).</summary>
    private const int CodePointPartyPopper = 0x1F389;

    /// <summary>Verifies that <c>HasGlyph</c> on font sources, embedded fonts, and canvas fonts accurately reports glyph coverage.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HasGlyphReportsCoverageAcrossBundledAndBuiltInFonts()
    {
        var sansSource = OpenBundled(PdfBundledFontFamily.Sans);
        var mathSource = OpenBundled(PdfBundledFontFamily.Symbol);
        var emojiSource = OpenBundled(PdfBundledFontFamily.Emoji);

        await Assert.That(sansSource.HasGlyph(CodePointLatinA)).IsTrue();
        await Assert.That(sansSource.HasGlyph(CodePointPartyPopper)).IsFalse();
        await Assert.That(mathSource.HasGlyph(CodePointSummation)).IsTrue();
        await Assert.That(emojiSource.HasGlyph(new Rune(CodePointPartyPopper))).IsTrue();

        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var sans = creator.EmbedFont(sansSource);
        var builtIn = creator.AddBuiltInFont(AppearanceFont.Helvetica);

        await Assert.That(sans.HasGlyph(new Rune(CodePointLatinA))).IsTrue();
        await Assert.That(sans.HasGlyph(new Rune(CodePointPartyPopper))).IsFalse();
        await Assert.That(builtIn.HasGlyph(CodePointLatinA)).IsTrue();
        await Assert.That(builtIn.HasGlyph(CodePointSummation)).IsFalse();
        await Assert.That(builtIn.HasGlyph(CodePointPartyPopper)).IsFalse();
    }

    /// <summary>Verifies that default fallback helpers return the bundled symbol, dingbats, and monochrome emoji fonts in order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DefaultFallbacksOpenAndEmbedSymbolDingbatsAndEmojiInOrder()
    {
        var sources = PdfBundledFonts.OpenDefaultFallbacks();
        await Assert.That(sources.Count).IsEqualTo(ExpectedDefaultFallbackCount);
        await Assert.That(sources[0].PostScriptName).Contains("Math");
        await Assert.That(sources[SecondIndex].PostScriptName).Contains("Symbols");
        await Assert.That(sources[ThirdIndex].PostScriptName).Contains("Emoji");

        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var embedded = creator.EmbedDefaultFallbacks();
        await Assert.That(embedded.Count).IsEqualTo(ExpectedDefaultFallbackCount);
        await Assert.That(embedded[ThirdIndex].HasGlyph(CodePointPartyPopper)).IsTrue();
    }

    /// <summary>Verifies that no fallback occurs when the primary font contains all glyphs in a string.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrimaryFontWithAllGlyphsDoesNotTriggerFallback()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var sans = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Sans));
        var math = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Symbol));
        var font = sans.WithFallbacks(math);

        var runs = font.SplitRuns("Hello World 123");
        await Assert.That(runs.Count).IsEqualTo(1);
        await Assert.That(runs[0].Font).IsSameReferenceAs(font);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].Length).IsEqualTo("Hello World 123".Length);
    }

    /// <summary>Verifies that an ordered fallback chain selects the first fallback font containing each missing glyph and coalesces adjacent clusters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OrderedFallbackListSelectsFirstMatchingFontAndMeasuresConsistently()
    {
        var creator = new PdfDocumentCreator(new PdfDocumentCreatorOptions { UseDefaultFallbackFonts = false });
        var sans = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Sans));
        var math = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Symbol));
        var emoji = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Emoji));
        var font = sans.WithFallbacks(math, emoji);

        const string text = "A∀B🎉C";
        var runs = font.SplitRuns(text);
        await Assert.That(runs.Count).IsEqualTo(ExpectedFiveRuns);
        await Assert.That(runs[0].Font).IsSameReferenceAs(font);
        await Assert.That(runs[SecondIndex].Font).IsSameReferenceAs(math);
        await Assert.That(runs[ThirdIndex].Font).IsSameReferenceAs(font);
        await Assert.That(runs[FourthIndex].Font).IsSameReferenceAs(emoji);

        var expectedWidth = sans.Measure("A", TestFontSize)
            + math.Measure("∀", TestFontSize)
            + sans.Measure("B", TestFontSize)
            + emoji.Measure("🎉", TestFontSize)
            + sans.Measure("C", TestFontSize);
        var actualWidth = font.Measure(text, TestFontSize);
        await Assert.That(Math.Abs(actualWidth - expectedWidth)).IsLessThanOrEqualTo(WidthTolerance);
    }

    /// <summary>Verifies that surrogate pairs, variation selectors, skin-tone modifiers, and ZWJ sequences remain intact and extract cleanly from a generated PDF.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SurrogatePairsVariationSelectorsModifiersAndZwjSequencesStayTogetherAndExtract(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var sans = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Sans));

        const string text = "Hi 👍\uFE0F 👍🏻 👩‍🔬 ∑ ✓ Bye";
        var runs = sans.SplitRuns(text);
        await Assert.That(runs.Count).IsGreaterThanOrEqualTo(ExpectedThreeRuns);

        using (var page = creator.BeginPage(TestPageWidth, TestPageHeight))
        {
            page.DrawText(sans, TestFontSize, TestDrawX, TestDrawY, text);
            _ = page.Finish();
        }

        var extracted = await ExtractFirstPageTextAsync(creator.ToArray(), cancellationToken);

        await Assert.That(extracted).Contains("Hi");
        await Assert.That(extracted).Contains("👍");
        await Assert.That(extracted).Contains("∑");
        await Assert.That(extracted).Contains("✓");
        await Assert.That(extracted).Contains("Bye");
    }

    /// <summary>Verifies that <see cref="PdfCanvasFont.WithoutDefaultFallbacks"/> disables automatic default backup font resolution.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task WithoutDefaultFallbacksDisablesAutomaticBackupResolution()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var sansWithDefaults = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Sans));
        var sansWithoutDefaults = sansWithDefaults.WithoutDefaultFallbacks();

        var withDefaultRuns = sansWithDefaults.SplitRuns("A🎉B");
        await Assert.That(withDefaultRuns.Count).IsEqualTo(ExpectedThreeRuns);
        await Assert.That(withDefaultRuns[SecondIndex].Font.Name).Contains("Emoji");

        var withoutDefaultRuns = sansWithoutDefaults.SplitRuns("A🎉B");
        await Assert.That(withoutDefaultRuns.Count).IsEqualTo(1);
        await Assert.That(withoutDefaultRuns[0].Font).IsSameReferenceAs(sansWithoutDefaults);
    }

    /// <summary>Verifies that <see cref="PdfTextWrapper"/> measures fallback runs identically to <see cref="PdfCanvasFont.Measure"/> and never splits emoji clusters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TextWrapperMeasuresFallbackRunsAndNeverSplitsEmojiClusters()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var sans = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Sans));

        const string text = "🎉👍🏻👩‍🔬🚀🎉👍🏻👩‍🔬🚀";
        var lines = new List<PdfTextLine>();
        PdfTextWrapper.Wrap(sans, TestFontSize, text, NarrowWrapWidth, lines);

        await Assert.That(lines.Count).IsGreaterThan(1);
        var reconstructed = new StringBuilder();
        foreach (var line in lines)
        {
            var slice = text.Substring(line.Start, line.Length);
            var validUtf16 = IsValidUtf16(slice);
            var expectedLineWidth = sans.Measure(slice, TestFontSize);
            await Assert.That(validUtf16).IsTrue();
            await Assert.That(Math.Abs(line.Width - expectedLineWidth)).IsLessThanOrEqualTo(WidthTolerance);
            _ = reconstructed.Append(slice);
        }

        await Assert.That(reconstructed.ToString()).IsEqualTo(text);
    }

    /// <summary>Verifies that fallback fonts configured at <see cref="PdfFlowOptions"/> level are inherited and overridden across tables, cells, text runs, and code blocks.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FlowOptionsTableCellTextRunAndCodeBlockInheritAndOverrideFallbackFonts(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new PdfDocumentCreatorOptions { UseDefaultFallbackFonts = false });
        var sans = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Sans));
        var mono = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Fixed));
        var math = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Symbol));
        var dingbats = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Dingbats));
        var emoji = creator.EmbedFont(OpenBundled(PdfBundledFontFamily.Emoji));

        var flowOptions = new PdfFlowOptions(sans) { PageWidth = TestPageWidth, PageHeight = TestPageHeight, Margin = TestMargin, FallbackFonts = [math] };
        var table = BuildFallbackTable(math, dingbats, emoji);
        var codeBlock = new PdfFlowCodeBlock("val = ∀ + 🎉;") { Font = mono, FallbackFonts = [math, emoji] };

        _ = PdfTextFlow.Add(creator, [new PdfFlowParagraph("Body math ∀"), table, codeBlock], flowOptions, cancellationToken);
        var extracted = await ExtractFirstPageTextAsync(creator.ToArray(), cancellationToken);

        await Assert.That(extracted).Contains("Body math ∀");
        await Assert.That(extracted).Contains("Header ∀ ✓");
        await Assert.That(extracted).Contains("Cell emoji 🎉");
        await Assert.That(extracted).Contains("Run math ∀");
        await Assert.That(extracted).Contains("Run emoji 🎉");
        await Assert.That(extracted).Contains("val = ∀ + 🎉;");
    }

    /// <summary>Opens a regular bundled font source for test assertions.</summary>
    /// <param name="family">The bundled font family.</param>
    /// <returns>The opened font source.</returns>
    private static PdfFontSource OpenBundled(PdfBundledFontFamily family) =>
        PdfBundledFonts.Open(family, bold: false, italic: false).Source!;

    /// <summary>Builds a table testing table-level, cell-level, and run-level fallback font overrides.</summary>
    /// <param name="math">The math fallback font.</param>
    /// <param name="dingbats">The dingbats fallback font.</param>
    /// <param name="emoji">The emoji fallback font.</param>
    /// <returns>The configured <see cref="PdfFlowTable"/>.</returns>
    private static PdfFlowTable BuildFallbackTable(PdfCanvasFont math, PdfCanvasFont dingbats, PdfCanvasFont emoji) =>
        new(
        [
            new PdfTableRow([new PdfTableCell("Header ∀ ✓")]),
            new PdfTableRow([new PdfTableCell("Cell emoji 🎉") { FallbackFonts = [emoji] }]),
            new PdfTableRow([new PdfTableCell([new PdfTextRun("Run math ∀ "), new PdfTextRun("Run emoji 🎉") { FallbackFonts = [emoji] }])]),
        ]) { FallbackFonts = [math, dingbats] };

    /// <summary>Checks that a UTF-16 character span contains no unpaired surrogates.</summary>
    /// <param name="span">The character span.</param>
    /// <returns><see langword="true"/> when every code point decodes as a valid <see cref="Rune"/>.</returns>
    private static bool IsValidUtf16(ReadOnlySpan<char> span)
    {
        for (int offset = 0, consumed; offset < span.Length; offset += consumed)
        {
            if (Rune.DecodeFromUtf16(span.Slice(offset), out _, out consumed) != System.Buffers.OperationStatus.Done)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Opens a PDF byte array and extracts the text of its first page.</summary>
    /// <param name="pdf">The PDF file bytes.</param>
    /// <param name="cancellationToken">Cancels the extraction.</param>
    /// <returns>The extracted text of page 0.</returns>
    private static async Task<string> ExtractFirstPageTextAsync(byte[] pdf, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(pdf, null, cancellationToken);
        var page = await PdfDocumentText.GetTextPageAsync(document, 0, cancellationToken);
        return page.Text;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.TextLayer;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.TextLayer;

/// <summary>Tests for <see cref="PdfTextLayer"/> and its fonts.</summary>
public sealed partial class PdfTextLayerTests
{
    /// <summary>How close numbers in the content must be.</summary>
    private const double Tolerance = 0.01;

    /// <summary>How close font metrics must be.</summary>
    private const float MetricTolerance = 0.001F;

    /// <summary>The width of the test word's box.</summary>
    private const float BoxWidth = 60;

    /// <summary>The height of the test word's box.</summary>
    private const float BoxHeight = 20;

    /// <summary>The word's left edge.</summary>
    private const float Left = 100;

    /// <summary>The word's top edge.</summary>
    private const float Top = 50;

    /// <summary>A box edge that leaves the box too narrow to place.</summary>
    private const float TinyWidth = 0.1F;

    /// <summary>Helvetica's ascent in ems.</summary>
    private const double Ascent = 0.718;

    /// <summary>Helvetica's descent in ems.</summary>
    private const double Descent = 0.207;

    /// <summary>The advance of 'A' in Helvetica, in thousandths of an em.</summary>
    private const float AdvanceA = 667;

    /// <summary>The number of letters in the test word.</summary>
    private const int LetterCount = 3;

    /// <summary>Thousandths of an em.</summary>
    private const double Thousand = 1000;

    /// <summary>Items in a wrapped content array: open, existing content, close, text layer.</summary>
    private const int WrappedItems = 4;

    /// <summary>The bytes of three two-byte codes less one: two characters.</summary>
    private const int TwoCodeBytes = 4;

    /// <summary>The index of the stream that restores the graphics state.</summary>
    private const int CloseIndex = 2;

    /// <summary>The first text layer font name.</summary>
    private const string FirstFontName = "OcrF1";

    /// <summary>The second text layer font name.</summary>
    private const string SecondFontName = "OcrF2";

    /// <summary>Content the page already draws.</summary>
    private const string PageContent = "0.5 g 0 0 10 10 re f";

    /// <summary>The percent that means no scaling.</summary>
    private const double Percent = 100;

    /// <summary>The width of the page used in the tests.</summary>
    private const int PageWidth = 300;

    /// <summary>The height of the page used in the tests.</summary>
    private const int PageHeight = 200;

    /// <summary>The ascent of the coded test font.</summary>
    private const float CodedAscent = 0.9F;

    /// <summary>The descent of the coded test font.</summary>
    private const float CodedDescent = 0.2F;

    /// <summary>The code of 'A' in the coded test font.</summary>
    private const ushort CodeA = 0x0102;

    /// <summary>The code of beta in the coded test font.</summary>
    private const ushort CodeBeta = 0x0003;

    /// <summary>The width of 'A' in the coded test font.</summary>
    private const float WidthA = 500;

    /// <summary>The width of beta in the coded test font.</summary>
    private const float WidthBeta = 600;

    /// <summary>The capture group of the text matrix's first element.</summary>
    private const int MatrixA = 1;

    /// <summary>The capture group of the text matrix's second element.</summary>
    private const int MatrixB = 2;

    /// <summary>The capture group of the text matrix's third element.</summary>
    private const int MatrixC = 3;

    /// <summary>The capture group of the text matrix's fourth element.</summary>
    private const int MatrixD = 4;

    /// <summary>The capture group of the text matrix's horizontal position.</summary>
    private const int MatrixE = 5;

    /// <summary>The capture group of the text matrix's vertical position.</summary>
    private const int MatrixF = 6;

    /// <summary>The capture group of a font size.</summary>
    private const int SizeGroup = 2;

    /// <summary>The capture group of a text scale.</summary>
    private const int ScaleGroup = 1;

    /// <summary>The bytes the widest code of a character takes.</summary>
    private const int CodeWidth = 2;

    /// <summary>A word drawn at the test box.</summary>
    private static readonly PdfTextLayerWord Triple = new("AAA", Left, Top, Left + BoxWidth, Top + BoxHeight, 0);

    /// <summary>The extent of 'A' then beta in the coded test font.</summary>
    private static readonly PdfTextExtent Both = new(WidthA + WidthBeta, 0, WidthA + WidthBeta);

    /// <summary>The codes of 'A' then beta.</summary>
    private static readonly byte[] ExpectedCodes = [0x01, 0x02, 0x00, 0x03];

    /// <summary>A word fills its box: the size makes ascent plus descent as tall as the box, and Tz makes the run as wide.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WordFillsItsBox()
    {
        const double expectedSize = BoxHeight / (Ascent + Descent);
        var ink = AppearanceFontMetrics.MeasureInk(AppearanceFont.Helvetica, "AAA"u8, (float)Thousand);
        var expectedScale = BoxWidth / ((ink.Right - ink.Left) * expectedSize / Thousand);
        var expectedLeft = ExpectedOrigin(0);
        using var document = Open(string.Empty);
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        var written = PdfTextLayer.Append(document.Objects, page, [Triple], [font]);
        var content = LayerContent(document);
        var size = Number(FontPattern().Match(content), SizeGroup);
        var scale = Number(ScalePattern().Match(content), ScaleGroup);
        var matrix = MatrixPattern().Match(content);

        // The baseline sits one descent above the box bottom: user y = page height - (bottom - descent * size).
        const double baseline = PageHeight - (Top + BoxHeight - (Descent * expectedSize));

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(content).Contains("3 Tr");
        await Assert.That(content).Contains("(AAA) Tj");
        await Assert.That(size).IsEqualTo(expectedSize).Within(Tolerance);
        await Assert.That(scale).IsEqualTo(expectedScale * Percent).Within(Tolerance);
        await Assert.That(Number(matrix, MatrixE)).IsEqualTo(expectedLeft).Within(Tolerance);
        await Assert.That(Number(matrix, MatrixF)).IsEqualTo(baseline).Within(Tolerance);
    }

    /// <summary>The existing content is wrapped in a save and restore, and the text layer is a new stream after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrapsExistingContent()
    {
        using var document = Open(PageContent);
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        _ = PdfTextLayer.Append(document.Objects, page, [Triple], [font]);
        var contents = PdfPageAnnotations.GetPageDictionary(document.Objects, page).GetArray(KnownName.Contents)!;

        await Assert.That(contents.Count).IsEqualTo(WrappedItems);
        await Assert.That(Decode(contents, 0)).IsEqualTo("q\n");
        await Assert.That(Decode(contents, 1)).IsEqualTo(PageContent);
        await Assert.That(Decode(contents, CloseIndex)).IsEqualTo("\nQ\n");
        await Assert.That(Decode(contents, WrappedItems - 1)).StartsWith("q");
    }

    /// <summary>A name already in the page's fonts is kept, and the new font gets a name that is free.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosesAFreshFontName()
    {
        using var document = Open(string.Empty, "/Font << /OcrF1 << /Type /Font /Subtype /Type1 /BaseFont /Courier >> >>");
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        _ = PdfTextLayer.Append(document.Objects, page, [Triple], [font]);
        var fonts = PdfPageAnnotations.GetPageDictionary(document.Objects, page).GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;
        var names = document.Objects.Names;

        await Assert.That(fonts.ContainsKey(names.Intern(FirstFontName))).IsTrue();
        await Assert.That(fonts.ContainsKey(names.Intern(SecondFontName))).IsTrue();
        await Assert.That(names.GetString(fonts.GetDictionary(names.Intern(FirstFontName))!.GetName(KnownName.BaseFont))).IsEqualTo("Courier");
        await Assert.That(LayerContent(document)).Contains($"/{SecondFontName} ");
    }

    /// <summary>On a rotated page the text turns with the page, so it reads along the viewer's horizontal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsPageRotation()
    {
        using var document = Open(string.Empty, string.Empty, "/Rotate 90");
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        _ = PdfTextLayer.Append(document.Objects, page, [Triple], [font]);
        var matrix = MatrixPattern().Match(LayerContent(document));
        var transform = page.UserTransform;

        await Assert.That(Number(matrix, MatrixA)).IsEqualTo(transform.M11).Within(Tolerance);
        await Assert.That(Number(matrix, MatrixB)).IsEqualTo(transform.M12).Within(Tolerance);
        await Assert.That(Number(matrix, MatrixC)).IsEqualTo(-transform.M21).Within(Tolerance);
        await Assert.That(Number(matrix, MatrixD)).IsEqualTo(-transform.M22).Within(Tolerance);
        await Assert.That(Math.Abs(transform.M11) < Tolerance).IsTrue();
    }

    /// <summary>A crop box moves the origin: the text lands where the viewer maps the box to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsCropBox()
    {
        const int cropLeft = 20;
        const int cropBottom = 30;
        using var document = Open(string.Empty, string.Empty, string.Create(CultureInfo.InvariantCulture, $"/CropBox [{cropLeft} {cropBottom} {PageWidth} {PageHeight}]"));
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        _ = PdfTextLayer.Append(document.Objects, page, [Triple], [font]);
        var matrix = MatrixPattern().Match(LayerContent(document));

        await Assert.That(Number(matrix, MatrixE)).IsEqualTo(ExpectedOrigin(cropLeft)).Within(Tolerance);
    }

    /// <summary>Empty words, tiny boxes and unknown fonts are skipped, and a layer with nothing to write leaves the page alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsWordsItCannotPlace()
    {
        using var document = Open(string.Empty);
        var page = PdfDocumentPages.GetPage(document, 0);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        PdfTextLayerWord[] words =
        [
            Triple with { Text = string.Empty },
            Triple with { Right = Left + TinyWidth },
            Triple with { Font = 1 },
            Triple with { Text = "中" },
        ];
        var before = PdfPageAnnotations.GetPageDictionary(document.Objects, page);
        var written = PdfTextLayer.Append(document.Objects, page, words, [font]);

        await Assert.That(written).IsEqualTo(0);
        await Assert.That(PdfPageAnnotations.GetPageDictionary(document.Objects, page)).IsSameReferenceAs(before);
        await Assert.That(PdfTextLayer.IsPlaceable(Triple)).IsTrue();
        await Assert.That(PdfTextLayer.IsPlaceable(Triple with { Bottom = float.NaN })).IsFalse();
    }

    /// <summary>The built in font encodes WinAnsi text and measures it; it reports what it cannot show.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StandardFontEncodesAndMeasures()
    {
        using var document = Open(string.Empty);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        var codes = new byte[LetterCount * CodeWidth];
        var count = font.Encode("AAA", codes, out var extent);

        await Assert.That(count).IsEqualTo(LetterCount);
        await Assert.That(codes[0]).IsEqualTo((byte)'A');
        await Assert.That(extent.Advance).IsEqualTo(LetterCount * AdvanceA).Within(MetricTolerance);
        await Assert.That(extent.InkLeft).IsGreaterThanOrEqualTo(0);
        await Assert.That(extent.InkRight).IsLessThan(extent.Advance);
        await Assert.That(font.Encode("中", codes, out _)).IsEqualTo(0);
        await Assert.That(PdfStandardTextLayerFont.Covers("Hello é €")).IsTrue();
        await Assert.That(PdfStandardTextLayerFont.Covers("中")).IsFalse();
        await Assert.That(font.Ascent).IsEqualTo((float)Ascent).Within(MetricTolerance);
        await Assert.That(font.Descent).IsEqualTo((float)Descent).Within(MetricTolerance);
    }

    /// <summary>A coded font writes two-byte codes, adds up their widths and leaves out characters it has no code for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodedFontWritesTwoByteCodes()
    {
        var widths = new float[CodeA + 1];
        widths[CodeA] = WidthA;
        widths[CodeBeta] = WidthBeta;
        var font = new PdfCodedTextLayerFont(default, new() { ['A'] = CodeA, ['β'] = CodeBeta }, widths, CodedAscent, CodedDescent);
        var codes = new byte[LetterCount * CodeWidth];
        var count = font.Encode("A?β", codes, out var extent);

        await Assert.That(count).IsEqualTo(TwoCodeBytes);
        await Assert.That(codes[..count]).IsEquivalentTo(ExpectedCodes);
        await Assert.That(extent).IsEqualTo(Both);
        await Assert.That(font.Encode("?", codes, out _)).IsEqualTo(0);
        await Assert.That(font.Ascent).IsEqualTo(CodedAscent);
        await Assert.That(font.Descent).IsEqualTo(CodedDescent);
    }

    /// <summary>A saved and reopened document keeps the text layer and its content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SurvivesSaving()
    {
        using var document = Open(PageContent);
        var font = new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica);
        _ = PdfTextLayer.Append(document.Objects, PdfDocumentPages.GetPage(document, 0), [Triple], [font]);
        using var reopened = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        var contents = PdfDocumentPages.GetPage(reopened, 0).Dictionary.GetArray(KnownName.Contents)!;
        var fonts = PdfDocumentPages.GetPage(reopened, 0).Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;

        await Assert.That(contents.Count).IsEqualTo(WrappedItems);
        await Assert.That(Decode(contents, WrappedItems - 1)).Contains("(AAA) Tj");
        await Assert.That(fonts.ContainsKey(reopened.Objects.Names.Intern(FirstFontName))).IsTrue();
    }

    /// <summary>Gets the pattern of a text scale.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"(-?[\d.]+) Tz")]
    private static partial Regex ScalePattern();

    /// <summary>Gets the pattern of a text matrix.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"(-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) (-?[\d.]+) Tm")]
    private static partial Regex MatrixPattern();

    /// <summary>Gets the pattern of a font selection.</summary>
    /// <returns>The regular expression.</returns>
    [GeneratedRegex(@"/(\w+) (-?[\d.]+) Tf")]
    private static partial Regex FontPattern();

    /// <summary>Gets where the test word's text origin lands horizontally: the ink's left edge is put on the box's left edge.</summary>
    /// <param name="offset">How far the page's origin sits left of the viewer's.</param>
    /// <returns>The user space x.</returns>
    private static double ExpectedOrigin(double offset)
    {
        var ink = AppearanceFontMetrics.MeasureInk(AppearanceFont.Helvetica, "AAA"u8, (float)Thousand);
        const double size = BoxHeight / (Ascent + Descent);
        var scale = BoxWidth / ((ink.Right - ink.Left) * size / Thousand);
        return Left - (scale * ink.Left * size / Thousand) + offset;
    }

    /// <summary>Reads a number captured by a match.</summary>
    /// <param name="match">The match.</param>
    /// <param name="group">The group.</param>
    /// <returns>The number.</returns>
    private static double Number(Match match, int group) => double.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    /// <summary>Opens a one page document whose page draws some content.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string content) => Open(content, string.Empty);

    /// <summary>Opens a one page document whose page draws some content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="resources">Extra resources entries.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string content, string resources) => Open(content, resources, string.Empty);

    /// <summary>Opens a one page document whose page draws some content.</summary>
    /// <param name="content">The page content.</param>
    /// <param name="resources">Extra resources entries.</param>
    /// <param name="pageEntries">Extra page dictionary entries.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string content, string resources, string pageEntries)
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Contents 4 0 R /Resources << {resources} >> {pageEntries} >>"),
            MiniPdf.Stream(string.Empty, content));
        return PdfDocumentReader.Open(bytes, null);
    }

    /// <summary>Gets the text layer's decoded content: the last stream of the page's contents.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The content.</returns>
    private static string LayerContent(PdfDocument document)
    {
        var contents = PdfPageAnnotations.GetPageDictionary(document.Objects, PdfDocumentPages.GetPage(document, 0)).GetArray(KnownName.Contents)!;
        return Decode(contents, contents.Count - 1);
    }

    /// <summary>Decodes one stream of a contents array.</summary>
    /// <param name="contents">The array.</param>
    /// <param name="index">The index.</param>
    /// <returns>The text.</returns>
    private static string Decode(PdfArray contents, int index) => Encoding.Latin1.GetString(contents.Get(index).AsStream()!.DecodeToArray());
}

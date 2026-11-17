// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdf.TestAssets.Shaping;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading a page of body text into page objects, writing it again with the original bytes kept, writing every
/// object again from the model, and writing it after one text object lost a glyph and another moved.
/// </summary>
public class HyperPdfPageObjectBenchmarks
{
    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 48;

    /// <summary>The line spacing.</summary>
    private const int Leading = 14;

    /// <summary>The left margin and the baseline of the first line.</summary>
    private const int Margin = 72;

    /// <summary>The page height.</summary>
    private const int PageHeight = 792;

    /// <summary>The words each line is made from.</summary>
    private const string LineText = "The quick brown fox jumps over the lazy dog while reading a long accessible document";

    /// <summary>A text in Cyrillic, which the built-in fonts lack.</summary>
    private const string CyrillicText = "Быстрая коричневая лиса прыгает через ленивую собаку, пока читает длинный доступный документ";

    /// <summary>A text in Arabic, which reads right to left.</summary>
    private const string ArabicText = "الثعلب البني السريع يقفز فوق الكلب الكسول بينما يقرأ مستندا طويلا يسهل الوصول إليه";

    /// <summary>Arabic words made of beh, alef and lam, which join, for the font that has only those letters.</summary>
    private const string JoinedLetters = "ببب ببا بلب لبب ابل بلا ببلب لبلب ببابل بلبلب ببل";

    /// <summary>The width the lines of the justified paragraph are stretched to.</summary>
    private const float JustifiedWidth = 300F;

    /// <summary>The size of the text of the justified paragraph.</summary>
    private const float JustifiedSize = 12F;

    /// <summary>The distance between the baselines of the justified paragraph.</summary>
    private const float JustifiedLeading = 14F;

    /// <summary>The advance of a letter of the Arabic font, in font units.</summary>
    private const int LetterAdvance = 500;

    /// <summary>The advance of the tatweel of the Arabic font, in font units.</summary>
    private const int TatweelAdvance = 250;

    /// <summary>The code of the Arabic letter beh.</summary>
    private const int BehCode = 0x0628;

    /// <summary>The code of the Arabic letter alef.</summary>
    private const int AlefCode = 0x0627;

    /// <summary>The code of the Arabic letter lam.</summary>
    private const int LamCode = 0x0644;

    /// <summary>The code of the tatweel.</summary>
    private const int TatweelCode = 0x0640;

    /// <summary>The code of the space.</summary>
    private const int SpaceCode = 0x0020;

    /// <summary>The lines the reflow benchmark treats as one paragraph.</summary>
    private const int ParagraphLines = 6;

    /// <summary>The lines the restyle benchmark changes.</summary>
    private const int RestyledLines = 8;

    /// <summary>How solid the restyled lines are.</summary>
    private const float Half = 0.5F;

    /// <summary>The gradient the restyled lines are filled with.</summary>
    private static readonly PdfGradient Gradient = PdfGradient.Linear(PdfPaint.FromRgb(0, 0, 1), PdfPaint.FromRgb(1, 0, 0), 0F);

    /// <summary>The lines of the justified paragraph, the last of which is short.</summary>
    private static readonly string[] JustifiedText = ["The quick brown fox jumps over", "the lazy dog while reading a", "long accessible document and", "end"];

    /// <summary>The lines of the paragraph the reflow benchmark rewraps, kept between runs so filling it makes no garbage.</summary>
    private readonly List<PdfTextObject> _paragraph = [with(ParagraphLines)];

    /// <summary>The font that has the letters of <see cref="JoinedLetters"/> and the tatweel.</summary>
    private readonly PdfFontSource _arabicFont = CreateArabicFont();

    /// <summary>The document.</summary>
    private PdfDocument? _document;

    /// <summary>The document whose page holds a justified paragraph.</summary>
    private PdfDocument? _justified;

    /// <summary>The page's content, read once for the write benchmarks.</summary>
    private PdfPageContent? _content;

    /// <summary>Opens the document and reads the page.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(CreatePage(), null);
        _content = PdfDocumentPageContent.GetPageContent(_document, 0);
        _justified = PdfDocumentReader.Open(CreateJustifiedPage(), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document?.Dispose();
        _justified?.Dispose();
    }

    /// <summary>Reads the page into objects.</summary>
    /// <returns>The object count, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public int Parse() => PdfDocumentPageContent.GetPageContent(_document!, 0).Objects.Count;

    /// <summary>Writes the content again, copying the bytes of objects nobody changed.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int RegeneratePreserve() => PdfPageContentWriter.Regenerate(_content!, PdfRegenerateMode.Preserve).Length;

    /// <summary>Writes every object again from the model.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int RegenerateRewrite() => PdfPageContentWriter.Regenerate(_content!, PdfRegenerateMode.Rewrite).Length;

    /// <summary>Reads the page, removes a glyph, moves another object and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int EditAndRegenerate()
    {
        var content = PdfDocumentPageContent.GetPageContent(_document!, 0);
        _ = ((PdfTextObject)content.Objects[0]).RemoveGlyph(1);
        content.Objects[1].Translate(0, 1);
        return PdfPageContentWriter.Regenerate(content).Length;
    }

    /// <summary>Reads the page, makes eight lines half solid, multiplied and filled with a gradient, and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int RestyleAndRegenerate()
    {
        var content = PdfDocumentPageContent.GetPageContent(_document!, 0);
        for (var i = 0; i < RestyledLines; i++)
        {
            var line = content.Objects[i];
            line.SetOpacity(Half);
            line.SetBlendMode(PdfBlendMode.Multiply);
            line.SetGradientFill(Gradient);
        }

        return PdfPageContentWriter.Regenerate(content).Length;
    }

    /// <summary>Reads the page, rewraps its first six lines as one paragraph with a longer text, and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int ReflowParagraph() => Reflow($"{LineText} {LineText} {LineText}");

    /// <summary>Reads the page, rewraps its first six lines with a longer text in Cyrillic, which is embedded and measured as the font sets it, and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int ReflowCyrillicParagraph() => Reflow($"{CyrillicText} {CyrillicText} {CyrillicText}");

    /// <summary>Reads the page, rewraps its first six lines with a longer text in Arabic, which reads right to left and lines up on the right, and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int ReflowArabicParagraph() => Reflow($"{ArabicText} {ArabicText} {ArabicText}");

    /// <summary>Reads the page of a justified paragraph, rewraps its four lines with a longer text in Arabic, which fills each line out with tatweels, and writes the content.</summary>
    /// <returns>The length written.</returns>
    [Benchmark]
    public int ReflowJustifiedArabicParagraph()
    {
        var content = PdfDocumentPageContent.GetPageContent(_justified!, 0);
        _paragraph.Clear();
        for (var i = 0; i < JustifiedText.Length; i++)
        {
            _paragraph.Add((PdfTextObject)content.Objects[i]);
        }

        PdfPageContentTextReplacement.ReplaceParagraph(content, _paragraph, $"{JoinedLetters} {JoinedLetters} {JoinedLetters}", AppearanceFont.Helvetica, 0F, _arabicFont);
        return PdfPageContentWriter.Regenerate(content).Length;
    }

    /// <summary>Makes the font that has the letters of <see cref="JoinedLetters"/>, a space and the tatweel.</summary>
    /// <returns>The font.</returns>
    private static PdfFontSource CreateArabicFont()
    {
        var builder = new ShapingFontBuilder();
        foreach (var code in new[] { BehCode, AlefCode, LamCode, SpaceCode })
        {
            _ = builder.AddGlyph(code, LetterAdvance);
        }

        _ = builder.AddGlyph(TatweelCode, TatweelAdvance);
        return PdfFontSource.OpenOrThrow(builder.Build());
    }

    /// <summary>Builds a page of a justified paragraph: all lines but the last are stretched to one width, and the last ends at the right edge.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateJustifiedPage()
    {
        var content = new StringBuilder("BT /F1 12 Tf\n");
        for (var line = 0; line < JustifiedText.Length; line++)
        {
            var width = AppearanceFontMetrics.MeasureAdvance(AppearanceFont.Helvetica, Encoding.Latin1.GetBytes(JustifiedText[line]), JustifiedSize);
            var last = line == JustifiedText.Length - 1;
            var spacing = last ? 0F : (JustifiedWidth - width) / JustifiedText[line].Count(static c => c == ' ');
            var left = Margin + (last ? JustifiedWidth - width : 0F);
            _ = content.Append(CultureInfo.InvariantCulture, $"{spacing} Tw 1 0 0 1 {left} {PageHeight - Margin - (line * JustifiedLeading)} Tm ({JustifiedText[line]}) Tj\n");
        }

        _ = content.Append("ET");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>");
    }

    /// <summary>Builds a page of body text.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePage()
    {
        var content = new StringBuilder("BT /F1 10 Tf\n");
        for (var line = 0; line < Lines; line++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"1 0 0 1 {Margin} {PageHeight - Margin - (line * Leading)} Tm ({LineText}) Tj\n");
        }

        _ = content.Append("ET");
        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources 5 0 R /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>");
    }

    /// <summary>Rewraps the first six lines of the page with a text and writes the content.</summary>
    /// <param name="text">The new text.</param>
    /// <returns>The length written.</returns>
    private int Reflow(string text)
    {
        var content = PdfDocumentPageContent.GetPageContent(_document!, 0);
        _paragraph.Clear();
        for (var i = 0; i < ParagraphLines; i++)
        {
            _paragraph.Add((PdfTextObject)content.Objects[i]);
        }

        PdfPageContentTextReplacement.ReplaceParagraph(content, _paragraph, text, AppearanceFont.Helvetica, 0F, null);
        return PdfPageContentWriter.Regenerate(content).Length;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Loads and draws Type 3 fonts, whose glyphs are content streams.</summary>
[NotInParallel]
public sealed class Type3FontTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The code of the square glyph.</summary>
    private const int CodeSquare = 'A';

    /// <summary>The width of the square in text space: 1000 glyph units times the 0.001 matrix.</summary>
    private const float SquareWidth = 1;

    /// <summary>The tolerance of width comparisons.</summary>
    private const float Tolerance = 0.0001F;

    /// <summary>The largest channel difference accepted.</summary>
    private const int ColourTolerance = 8;

    /// <summary>A device column inside the first square.</summary>
    private const int InsideX = 20;

    /// <summary>A device column inside the second square, one advance of 20 points later.</summary>
    private const int SecondX = 40;

    /// <summary>A device row inside the squares.</summary>
    private const int InsideY = 80;

    /// <summary>A device row above the squares.</summary>
    private const int AboveY = 50;

    /// <summary>The glyph procedure: a square filling most of the em.</summary>
    private const string SquareProcedure = "1000 0 0 0 1000 1000 d1 100 100 800 800 re f";

    /// <summary>A Type 3 font scales /Widths by the font matrix and finds glyph procedures through /Differences.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type3FontLoadsProceduresAndWidths()
    {
        using var page = new RenderTestPage(CreatePage("BT /F1 20 Tf 10 10 Td (AA) Tj ET"));
        var font = LoadFont(page);

        await Assert.That(font).IsTypeOf<Type3Font>();
        await Assert.That(font.IsType3).IsTrue();
        await Assert.That(font.GetWidth(CodeSquare)).IsEqualTo(SquareWidth).Within(Tolerance);
        await Assert.That(((PdfType3Font)font).GetCharProc(CodeSquare)).IsNotNull();
        await Assert.That(((PdfType3Font)font).GetCharProc('B')).IsNull();
        await Assert.That(font.GetOutline(CodeSquare)).IsNull();
        await Assert.That(FontProbe.Text(font, CodeSquare)).IsEqualTo("A");
    }

    /// <summary>Type 3 glyphs draw their procedures at the text position and advance by their widths.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type3GlyphsDraw()
    {
        using var page = new RenderTestPage(CreatePage("BT /F1 20 Tf 10 10 Td (AA) Tj ET"));
        var image = page.RenderPage();

        await Assert.That(image.IsNear(InsideX, InsideY, Rgb.Black, ColourTolerance)).IsTrue();
        await Assert.That(image.IsNear(SecondX, InsideY, Rgb.Black, ColourTolerance)).IsTrue();
        await Assert.That(image.IsNear(InsideX, AboveY, Rgb.White, ColourTolerance)).IsTrue();
    }

    /// <summary>Creates a page whose /F1 is a Type 3 font with one square glyph named A.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePage(string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        var procedure = pdf.AddStream(string.Empty, SquareProcedure);
        var font = pdf.AddObject(
            $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 1000 1000] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /square {procedure} 0 R >> "
            + "/Encoding << /Type /Encoding /Differences [65 /square] >> /FirstChar 65 /LastChar 65 /Widths [1000] "
            + "/ToUnicode " + pdf.AddStream(string.Empty, "begincmap 1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfchar <41> <0041> endbfchar endcmap") + " 0 R >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        return pdf.ToBytes();
    }

    /// <summary>Loads the page's /F1.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The font.</returns>
    /// <exception cref="InvalidOperationException">The font did not load.</exception>
    private static PdfFont LoadFont(RenderTestPage page)
    {
        var fonts = HyperPdfLibrary.Document.PdfDocumentPages.GetPage(page.Document, 0).Resources?.GetDictionary(KnownName.Font) ?? throw new InvalidOperationException("No fonts.");
        var dictionary = fonts.Get(fonts.GetKeyAt(0)).AsDictionary() ?? throw new InvalidOperationException("No font.");
        return PdfFontLoader.Load(dictionary) ?? throw new InvalidOperationException("The font did not load.");
    }
}

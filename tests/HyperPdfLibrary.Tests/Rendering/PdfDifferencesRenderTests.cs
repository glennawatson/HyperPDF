// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Regression checks for cases of the PDF Association differences collection (pdf-association/pdf-differences) that HyperPDF got
/// wrong: Type 3 stroke colour, degenerate subpaths and inline image key precedence.
/// </summary>
public sealed class PdfDifferencesRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 200;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 12;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 100;

    /// <summary>The left edge of the Type 3 square, where its stroke is.</summary>
    private const int SquareEdge = 50;

    /// <summary>The gray the one pixel inline image is made of.</summary>
    private const int Gray = 65;

    /// <summary>A one pixel gray inline image whose full keys disagree with its abbreviated keys.</summary>
    private const string InlineImage = "q 100 0 0 100 50 50 cm BI /Width 99 /Height 99 /W 1 /H 1 /BitsPerComponent 4 /BPC 8 /ColorSpace /DeviceRGB /CS /G ID A\nEI Q";

    /// <summary>The channel of an unpainted page.</summary>
    private const int White = 255;

    /// <summary>A Type 3 glyph that gives only a shape (d1) and strokes a square keeps the stroking colour of the text, not its fill colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type3ShapeGlyphStrokesWithTheStrokeColour()
    {
        var pdf = new RenderTestPdf(Size, Size);
        var glyph = pdf.AddStream(string.Empty, "100 0 0 0 100 100 d1 20 w 0 0 100 100 re S");
        var font = pdf.AddObject(
            $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 100 100] /FontMatrix [0.01 0 0 0.01 0 0] /CharProcs << /a {glyph} 0 R >> "
            + "/Encoding << /Type /Encoding /Differences [97 /a] >> /FirstChar 97 /LastChar 97 /Widths [100] >>");
        pdf.Resources = $"/Font << /F1 {font} 0 R >>";
        pdf.Content = "1 0 0 RG 0 0 1 rg BT /F1 100 Tf 50 50 Td (a) Tj ET";
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(SquareEdge, Middle, new(White, 0, 0), Tolerance)).IsTrue();
    }

    /// <summary>A degenerate closed subpath paints a dot only with round caps; butt and square caps paint nothing.</summary>
    /// <param name="cap">The line cap style.</param>
    /// <param name="paints">Whether a dot is expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, false)]
    [Arguments(1, true)]
    [Arguments(2, false)]
    public async Task DegenerateSubpathsPaintOnlyWithRoundCaps(int cap, bool paints)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = $"30 w {cap} J 100 100 m h S" };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, paints ? new(0, 0, 0) : new(White, White, White), Tolerance)).IsTrue();
    }

    /// <summary>When an inline image has both an abbreviated and a full key, the abbreviated key wins, so a full Width and Height that disagree change nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AbbreviatedInlineImageKeysTakePrecedence()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = InlineImage };
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, new(Gray, Gray, Gray), Tolerance)).IsTrue();
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the underlines, strikeouts and highlights found from the lines and boxes a page draws near its text.</summary>
public sealed class TextDecorationTests
{
    /// <summary>Three letters at x 10 to 25, with a baseline at y 100 and a 10 point font.</summary>
    private const string Letters = "BT /F1 10 Tf 10 100 Td (abc) Tj ET";

    /// <summary>The index of the third letter.</summary>
    private const int Third = 2;

    /// <summary>Verifies that a thin line under the letters is an underline of every letter it runs along.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ThinLineUnderTheBaseline_IsAnUnderline()
    {
        var page = TextTestDocument.Extract($"{Letters} 0.5 w 10 98 m 25 98 l S");

        await Assert.That(page.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.Underline);
        await Assert.That(page.GetCharStyle(1).Decorations).IsEqualTo(PdfTextDecorations.Underline);
        await Assert.That(page.GetCharStyle(Third).Decorations).IsEqualTo(PdfTextDecorations.Underline);
    }

    /// <summary>Verifies that a thin filled bar under the letters is an underline too, as word processors draw it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ThinFilledBarUnderTheBaseline_IsAnUnderline()
    {
        var page = TextTestDocument.Extract($"{Letters} 10 97.5 15 0.6 re f");

        await Assert.That(page.GetCharStyle(1).Decorations).IsEqualTo(PdfTextDecorations.Underline);
    }

    /// <summary>Verifies that a line through the middle of the letters is a strikeout.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ThinLineThroughTheBody_IsAStrikeout()
    {
        var page = TextTestDocument.Extract($"{Letters} 0.5 w 10 103 m 25 103 l S");

        await Assert.That(page.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.Strikeout);
        await Assert.That(page.GetCharStyle(Third).Decorations).IsEqualTo(PdfTextDecorations.Strikeout);
    }

    /// <summary>Verifies that a coloured box behind the letters is a highlight, and a white box is not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ColouredBoxBehindTheText_IsAHighlight()
    {
        var highlighted = TextTestDocument.Extract($"1 1 0 rg 10 98 15 12 re f 0 g {Letters}");
        var paper = TextTestDocument.Extract($"1 1 1 rg 10 98 15 12 re f 0 g {Letters}");

        await Assert.That(highlighted.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.Highlight);
        await Assert.That(highlighted.GetCharStyle(Third).Decorations).IsEqualTo(PdfTextDecorations.Highlight);
        await Assert.That(paper.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.None);
    }

    /// <summary>Verifies that a line that covers only some of the letters decorates only those letters.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LineOverPartOfTheText_DecoratesOnlyThatPart()
    {
        var page = TextTestDocument.Extract($"{Letters} 0.5 w 10 98 m 20 98 l S");

        await Assert.That(page.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.Underline);
        await Assert.That(page.GetCharStyle(1).Decorations).IsEqualTo(PdfTextDecorations.Underline);
        await Assert.That(page.GetCharStyle(Third).Decorations).IsEqualTo(PdfTextDecorations.None);
    }

    /// <summary>Verifies that lines and boxes far from the text, and a drawing with many points, decorate nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ShapesAwayFromTheText_DecorateNothing()
    {
        var far = TextTestDocument.Extract($"{Letters} 0.5 w 10 60 m 25 60 l S 1 1 0 rg 10 20 15 12 re f");
        var drawing = TextTestDocument.Extract($"{Letters} 0.5 w 10 98 m 12 98 l 14 99 l 16 98 l 18 99 l 20 98 l 22 99 l 25 98 l S");

        await Assert.That(far.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.None);
        await Assert.That(drawing.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.None);
    }

    /// <summary>Verifies that text with nothing drawn near it has no decorations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlainText_HasNoDecorations()
    {
        var page = TextTestDocument.Extract(Letters);

        await Assert.That(page.GetCharStyle(0).Decorations).IsEqualTo(PdfTextDecorations.None);
    }
}

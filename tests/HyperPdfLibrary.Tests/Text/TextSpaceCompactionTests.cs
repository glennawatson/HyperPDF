// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks repeated spaces in extracted text and their character metadata.</summary>
[NotInParallel]
public sealed class TextSpaceCompactionTests
{
    /// <summary>The middle character in a five-character compacted line.</summary>
    private const int MiddleChar = 2;

    /// <summary>The character after the middle space.</summary>
    private const int AfterMiddleChar = 3;

    /// <summary>The last character in a five-character compacted line.</summary>
    private const int LastChar = 4;

    /// <summary>Repeated glyph spaces collapse, including at both ends of a line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CollapsesGlyphSpacesWithoutLosingFollowingCharacters()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (  A   B  ) Tj ET");

        await Assert.That(page.Text).IsEqualTo(" A B ");
        await Assert.That(page.CharCount).IsEqualTo(page.Text.Length);
        await Assert.That(page.GetChar(1).Unicode).IsEqualTo('A');
        await Assert.That(page.GetChar(AfterMiddleChar).Unicode).IsEqualTo('B');
        await Assert.That(page.GetChar(0).Kind).IsEqualTo(PdfTextCharKind.Normal);
        await Assert.That(page.GetChar(0).Box.Left).IsLessThan(page.GetChar(1).Box.Left);
        await Assert.That(page.GetChar(MiddleChar).Box.Left).IsGreaterThan(page.GetChar(1).Box.Left);
        await Assert.That(page.GetChar(1).Box.Left).IsLessThan(page.GetChar(AfterMiddleChar).Box.Left);
    }

    /// <summary>ActualText spaces collapse while the surviving characters keep their source kind and positions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CollapsesActualTextSpacesAndKeepsCharacterMetadata()
    {
        var pdf = TextTestDocument.Create("/Span /P1 BDC BT /F1 10 Tf 10 100 Td (XY) Tj ET EMC");
        pdf.Resources += " /Properties << /P1 << /ActualText ( A   B ) >> >>";
        var page = TextTestDocument.Extract(pdf);

        await Assert.That(page.Text).IsEqualTo(" A B ");
        await Assert.That(page.CharCount).IsEqualTo(page.Text.Length);
        await Assert.That(page.GetChar(0).Kind).IsEqualTo(PdfTextCharKind.ActualText);
        await Assert.That(page.GetChar(MiddleChar).Kind).IsEqualTo(PdfTextCharKind.ActualText);
        await Assert.That(page.GetChar(AfterMiddleChar).Unicode).IsEqualTo('B');
        await Assert.That(page.GetChar(0).Box.Left).IsLessThan(page.GetChar(1).Box.Left);
        await Assert.That(page.GetChar(MiddleChar).Box.Left).IsGreaterThan(page.GetChar(1).Box.Left);
        await Assert.That(page.GetChar(AfterMiddleChar).Box.Left).IsGreaterThan(page.GetChar(1).Box.Left);
    }

    /// <summary>An all-space line leaves one space and a page without text remains empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlesAllSpaceAndEmptyPages()
    {
        var spaces = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (      ) Tj ET");
        var empty = TextTestDocument.Extract("0 0 10 10 re f");

        await Assert.That(spaces.Text).IsEqualTo(" ");
        await Assert.That(spaces.CharCount).IsEqualTo(1);
        await Assert.That(spaces.GetChar(0).Kind).IsEqualTo(PdfTextCharKind.Normal);
        await Assert.That(empty.Text).IsEqualTo(string.Empty);
        await Assert.That(empty.CharCount).IsEqualTo(0);
    }

    /// <summary>Spaces at glyph and ActualText boundaries collapse without changing the following source kind.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactsMixedGlyphAndActualTextRuns()
    {
        var pdf = TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (A  ) Tj /Span /P1 BDC (XY) Tj EMC (  B) Tj ET");
        pdf.Resources += " /Properties << /P1 << /ActualText (  C  ) >> >>";
        var page = TextTestDocument.Extract(pdf);

        await Assert.That(page.Text).IsEqualTo("A C B");
        await Assert.That(page.GetChar(MiddleChar).Kind).IsEqualTo(PdfTextCharKind.ActualText);
        await Assert.That(page.GetChar(MiddleChar).Unicode).IsEqualTo('C');
        await Assert.That(page.GetChar(LastChar).Kind).IsEqualTo(PdfTextCharKind.Normal);
        await Assert.That(page.GetChar(AfterMiddleChar).Box.Left).IsLessThan(page.GetChar(LastChar).Box.Left);
    }

    /// <summary>The next extraction starts with clean buffers after a compacted line.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The first extraction did not return its state.</exception>
    [Test]
    public async Task ReusesCleanBuildStateAfterCompaction()
    {
        using var document = TextTestDocument.Open(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (A       B) Tj ET").ToBytes());
        var first = PdfDocumentText.ExtractText(document, 0);
        var state = document.State.TextBuild ?? throw new InvalidOperationException("The first extraction did not return its state.");
        var second = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (C) Tj ET");

        await Assert.That(first.Text).IsEqualTo("A B");
        await Assert.That(second.Text).IsEqualTo("C");
        await Assert.That(ReferenceEquals(state, document.State.TextBuild)).IsTrue();
        await Assert.That(state.Temp.Count).IsEqualTo(0);
        await Assert.That(state.TempText.Count).IsEqualTo(0);
    }
}

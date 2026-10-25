// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Layout;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the grouping of blobs of ink into lines of text.</summary>
public sealed class TextLineFinderTests
{
    /// <summary>The number of lines the first two tests expect.</summary>
    private const int TwoLines = 2;

    /// <summary>The height of a test letter.</summary>
    private const int LetterHeight = 10;

    /// <summary>The width of a test letter.</summary>
    private const int LetterWidth = 6;

    /// <summary>The distance from the start of one letter to the start of the next.</summary>
    private const int LetterPitch = 8;

    /// <summary>The number of letters in a test line.</summary>
    private const int LettersPerLine = 5;

    /// <summary>The row of the first test line.</summary>
    private const int FirstRow = 20;

    /// <summary>The row of the second test line.</summary>
    private const int SecondRow = 50;

    /// <summary>The column the left column starts at.</summary>
    private const int LeftColumn = 10;

    /// <summary>The column the right column starts at, far enough from the left that the lines do not join.</summary>
    private const int RightColumn = 300;

    /// <summary>The height of a picture, far above a letter.</summary>
    private const int PictureHeight = 200;

    /// <summary>The width of a speck.</summary>
    private const int SpeckSize = 2;

    /// <summary>Letters in two rows are two lines, with the box around each row's letters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowsOfLettersAreLines()
    {
        var blobs = new List<ComponentBox>();
        AddLine(blobs, LeftColumn, FirstRow);
        AddLine(blobs, LeftColumn, SecondRow);
        var lines = new List<TextLine>();

        TextLineFinder.Find(blobs, lines, new());

        await Assert.That(lines.Count).IsEqualTo(TwoLines);
        await Assert.That(lines[0].Left).IsEqualTo(LeftColumn);
        await Assert.That(lines[0].Top).IsEqualTo(FirstRow);
        await Assert.That(lines[0].Right).IsEqualTo(LeftColumn + ((LettersPerLine - 1) * LetterPitch) + LetterWidth - 1);
        await Assert.That(lines[0].Bottom).IsEqualTo(FirstRow + LetterHeight - 1);
        await Assert.That(lines[1].Top).IsEqualTo(SecondRow);
    }

    /// <summary>Rows side by side with a wide gap between are separate lines, so columns stay apart.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColumnsStayApart()
    {
        var blobs = new List<ComponentBox>();
        AddLine(blobs, LeftColumn, FirstRow);
        AddLine(blobs, RightColumn, FirstRow);
        var lines = new List<TextLine>();

        TextLineFinder.Find(blobs, lines, new());

        await Assert.That(lines.Count).IsEqualTo(TwoLines);
        await Assert.That(lines[0].Left).IsEqualTo(LeftColumn);
        await Assert.That(lines[1].Left).IsEqualTo(RightColumn);
    }

    /// <summary>Pictures far taller than a letter and lone specks are not lines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PicturesAndSpecksAreLeftOut()
    {
        var blobs = new List<ComponentBox>();
        AddLine(blobs, LeftColumn, FirstRow);
        blobs.Add(new(LeftColumn, SecondRow, LeftColumn + LetterWidth, SecondRow + PictureHeight, PictureHeight));
        blobs.Add(new(RightColumn, SecondRow, RightColumn + SpeckSize - 1, SecondRow + SpeckSize - 1, SpeckSize * SpeckSize));
        var lines = new List<TextLine>();

        TextLineFinder.Find(blobs, lines, new());

        await Assert.That(lines.Count).IsEqualTo(1);
    }

    /// <summary>No blobs, no lines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingGivesNoLines()
    {
        var lines = new List<TextLine>();

        TextLineFinder.Find([], lines, new());

        await Assert.That(lines.Count).IsEqualTo(0);
    }

    /// <summary>Adds the blobs of one row of letters.</summary>
    /// <param name="blobs">The list added to.</param>
    /// <param name="left">The column of the first letter.</param>
    /// <param name="top">The row of the letters' tops.</param>
    private static void AddLine(List<ComponentBox> blobs, int left, int top)
    {
        for (var i = 0; i < LettersPerLine; i++)
        {
            var x = left + (i * LetterPitch);
            blobs.Add(new(x, top, x + LetterWidth - 1, top + LetterHeight - 1, LetterWidth * LetterHeight));
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Checks which lines of a page are found as one paragraph.</summary>
public sealed class ParagraphFinderTests
{
    /// <summary>The height of a line of text, in points.</summary>
    private const float LineHeight = 12F;

    /// <summary>The distance from the top of one line to the top of the next.</summary>
    private const float Step = 14F;

    /// <summary>The width of a line.</summary>
    private const float Width = 150F;

    /// <summary>The left edge of the paragraph.</summary>
    private const float Left = 20F;

    /// <summary>The first row and the number of the object on it.</summary>
    private const int First = 0;

    /// <summary>The second row and the number of the object on it.</summary>
    private const int Second = 1;

    /// <summary>The third row and the number of the object on it.</summary>
    private const int Third = 2;

    /// <summary>The row of a line that is far under the paragraph.</summary>
    private const int FarRow = 6;

    /// <summary>The number of the object that is far under the paragraph.</summary>
    private const int FarIndex = 3;

    /// <summary>The number of the object that is taller than the paragraph's lines.</summary>
    private const int TallIndex = 4;

    /// <summary>The number of the object that starts elsewhere.</summary>
    private const int ElsewhereIndex = 5;

    /// <summary>The number of the object that is a drawing.</summary>
    private const int DrawingIndex = 6;

    /// <summary>The number of the only line of the lone paragraph.</summary>
    private const int LoneIndex = 7;

    /// <summary>How far in the lines under a list marker start.</summary>
    private const float Hanging = 14F;

    /// <summary>How far right the line that starts elsewhere is.</summary>
    private const float Indent = 80F;

    /// <summary>The width of a bullet drawn as a text of its own.</summary>
    private const float MarkerWidth = 6F;

    /// <summary>The middle the centred lines are set around.</summary>
    private const float Middle = 100F;

    /// <summary>The right edge the right aligned lines end at.</summary>
    private const float RightEdge = 170F;

    /// <summary>Where the middle of the first turned line is, across and down the page.</summary>
    private const float TurnedCentre = 200F;

    /// <summary>The number of degrees in a half turn.</summary>
    private const float HalfTurnDegrees = 180F;

    /// <summary>The number of a line that is not part of the turned paragraph.</summary>
    private const int Far = 9;

    /// <summary>How long the turned lines are.</summary>
    private const float TurnedLength = 100F;

    /// <summary>One half.</summary>
    private const float Half = 0.5F;

    /// <summary>Words that read left to right.</summary>
    private const string PlainWords = "plain words";

    /// <summary>Words that read right to left.</summary>
    private const string RightToLeftWords = "مرحبا بالعالم";

    /// <summary>The widths of the three lines of the centred and right aligned paragraphs.</summary>
    private static readonly float[] LineWidths = [150F, 100F, 120F];

    /// <summary>Starting from any line of three that follow one another, all three are found, from the top down.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinesThatFollowOneAnotherFormAParagraph()
    {
        PageObjectInfo[] page = [Line(First, First, "one"), Line(Second, Second, "two"), Line(Third, Third, "three")];

        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[Third])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[First])[First]).IsEqualTo(First);
    }

    /// <summary>A line far below, a line of another size, a line that starts elsewhere and a drawing are not part of the paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherLinesAreLeftOut()
    {
        PageObjectInfo[] page =
        [
            Line(First, First, "one"),
            Line(Second, Second, "two"),
            Line(FarIndex, FarRow, "far below"),
            new(TallIndex, PageObjectKind.Text, new(Left, (Third * Step) + 1, Width, LineHeight * Third), "bigger"),
            new(ElsewhereIndex, PageObjectKind.Text, new(Left + Indent, Third * Step, Width, LineHeight), "indented elsewhere"),
            new(DrawingIndex, PageObjectKind.Drawing, new(Left, Third * Step, Width, LineHeight), string.Empty),
        ];

        await Assert.That(ParagraphFinder.Find(page, page[First])).IsEquivalentTo([First, Second]);
    }

    /// <summary>A list item is its marker line and the lines under its words, from either end, and the next item is left out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AListItemIsItsMarkerLineAndTheLinesUnderItsWords()
    {
        PageObjectInfo[] page =
        [
            Line(First, First, "• First item has words"),
            new(Second, PageObjectKind.Text, new(Left + Hanging, Step, Width, LineHeight), "that wrap on"),
            new(Third, PageObjectKind.Text, new(Left + Hanging, Step * Third, Width, LineHeight), "to a third line"),
            Line(FarIndex, FarRow, "• Second item"),
        ];

        await Assert.That(ParagraphFinder.Find(page, page[First])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[Third])).IsEquivalentTo([First, Second, Third]);
    }

    /// <summary>A bullet drawn as a text of its own beside the first words is not part of the paragraph, and it ends the item before and starts this one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AMarkerOfItsOwnStartsAListItem()
    {
        const int Marker = 10;
        const int NextMarker = 11;
        PageObjectInfo[] page =
        [
            new(Marker, PageObjectKind.Text, new(Left, 0, MarkerWidth, LineHeight), "•"),
            new(First, PageObjectKind.Text, new(Left + Hanging, 0, Width, LineHeight), "First item has words"),
            new(Second, PageObjectKind.Text, new(Left + Hanging, Step, Width, LineHeight), "that wrap on"),
            new(Third, PageObjectKind.Text, new(Left + Hanging, Step * Third, Width, LineHeight), "to a third line"),
            new(NextMarker, PageObjectKind.Text, new(Left, Step * FarRow, MarkerWidth, LineHeight), "•"),
            new(FarIndex, PageObjectKind.Text, new(Left + Hanging, Step * FarRow, Width, LineHeight), "Second item"),
        ];

        await Assert.That(ParagraphFinder.Find(page, page[1])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[Third])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[^1])).IsEquivalentTo([FarIndex]);
        await Assert.That(ParagraphFinder.Find(page, page[0])).IsEquivalentTo([Marker]);
    }

    /// <summary>Centred lines, which start in different places, are found as one paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CentredLinesFormAParagraph()
    {
        PageObjectInfo[] page = [Centred(First), Centred(Second), Centred(Third)];

        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[Third])).IsEquivalentTo([First, Second, Third]);
    }

    /// <summary>Right aligned lines, which start in different places, are found as one paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightAlignedLinesFormAParagraph()
    {
        PageObjectInfo[] page = [AtRight(First), AtRight(Second), AtRight(Third)];

        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
    }

    /// <summary>Two lines that start together do not cut short a paragraph whose lines are all lined up on the right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinesThatStartTogetherByChanceDoNotCutShortARightAlignedParagraph()
    {
        const float End = 140F;
        const float Full = 120F;
        const float Short = 90F;
        const float Shorter = 110F;
        const int Fourth = 3;
        PageObjectInfo[] page =
        [
            Sized(First, First, End - Full, Full),
            Sized(Second, Second, End - Full, Full),
            Sized(Third, Third, End - Short, Short),
            Sized(Fourth, Fourth, End - Shorter, Shorter),
        ];

        await Assert.That(ParagraphFinder.Find(page, page[First])).IsEquivalentTo([First, Second, Third, Fourth]);
    }

    /// <summary>A text drawn right beside a line on the same row is part of that line and comes after it when the line reads left to right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ATextBesideALineIsPartOfIt()
    {
        const int Beside = 3;
        const float Touching = 2F;
        const float Short = 30F;
        PageObjectInfo[] page = [Line(First, First, PlainWords), Line(Second, Second, PlainWords), Line(Third, Third, PlainWords), Sized(Beside, Second, Left + Width + Touching, Short)];

        await Assert.That(string.Join(',', ParagraphFinder.Find(page, page[Second]))).IsEqualTo("0,1,3,2");
    }

    /// <summary>A text beside a line that reads right to left is read after the line when it is further left, and one further right is read before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ATextBesideARightToLeftLineIsReadInTheWayItReads()
    {
        const int Beside = 3;
        const float Touching = 2F;
        const float Short = 30F;
        PageObjectInfo[] page =
        [
            Reading(First), Reading(Second), Reading(Third),
            new(Beside, PageObjectKind.Text, new(RightEdge - LineWidths[Second] - Touching - Short, Second * Step, Short, LineHeight), RightToLeftWords),
        ];

        await Assert.That(string.Join(',', ParagraphFinder.Find(page, page[Second]))).IsEqualTo("0,1,3,2");

        PageObjectInfo Reading(int row) => new(row, PageObjectKind.Text, new(RightEdge - LineWidths[row], row * Step, LineWidths[row], LineHeight), RightToLeftWords);
    }

    /// <summary>The texts of a paragraph are joined with one space between them, and a text that is only spaces adds none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextsAreJoinedWithOneSpaceBetweenThem()
    {
        const float Piece = 20F;
        PageObjectInfo[] page =
        [
            new(First, PageObjectKind.Text, new(Left, 0F, Piece, LineHeight), " ab "),
            new(Second, PageObjectKind.Text, new(Left + Piece, 0F, Piece, LineHeight), "  "),
            new(Third, PageObjectKind.Text, new(Left + Piece + Piece, 0F, Piece, LineHeight), "cd"),
            new(Third + 1, PageObjectKind.Text, new(Left, Step, Piece, LineHeight), "ef"),
        ];

        await Assert.That(ParagraphFinder.JoinText(page, [First, Second, Third, Third + 1])).IsEqualTo("ab cd ef");
    }

    /// <summary>A text far across a row from a line is a column of its own and is not part of the paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ATextInAnotherColumnIsNotPartOfTheLine()
    {
        const int Beside = 3;
        const float Gutter = 40F;
        PageObjectInfo[] page = [Line(First, First, PlainWords), Line(Second, Second, PlainWords), Line(Third, Third, PlainWords), Sized(Beside, Second, Left + Width + Gutter, Left)];

        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
    }

    /// <summary>Lines turned to any angle are found one after the other the way they follow, and a line right where the next one would be that reads another way is left out.</summary>
    /// <param name="degrees">The way the lines read, anticlockwise from reading to the right.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(30F)]
    [Arguments(90F)]
    [Arguments(135F)]
    [Arguments(180F)]
    [Arguments(270F)]
    [Arguments(345F)]
    public async Task TurnedLinesFormAParagraph(float degrees)
    {
        var across = Turned(Far, Third + 1, degrees);
        PageObjectInfo[] page =
        [
            Turned(First, First, degrees),
            Turned(Second, Second, degrees),
            Turned(Third, Third, degrees),
            across with { Reading = across.Reading with { DirectionX = -across.Reading.DirectionY, DirectionY = across.Reading.DirectionX } },
        ];

        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
        await Assert.That(ParagraphFinder.Find(page, page[Third])[0]).IsEqualTo(First);
    }

    /// <summary>Turned lines that are centred on the same middle are found as one paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TurnedCentredLinesFormAParagraph()
    {
        const float Degrees = 60F;
        PageObjectInfo[] page = [TurnedBy(First, First, Degrees, LineWidths[First]), TurnedBy(Second, Second, Degrees, LineWidths[Second]), TurnedBy(Third, Third, Degrees, LineWidths[Third])];

        await Assert.That(ParagraphFinder.Find(page, page[Second])).IsEquivalentTo([First, Second, Third]);
    }

    /// <summary>Bullets, numbers and brackets mark list items; ordinary words do not.</summary>
    /// <param name="text">The first words of a line.</param>
    /// <param name="marked">Whether the line is a list item's first line.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("• item", true)]
    [Arguments("- item", true)]
    [Arguments("12. item", true)]
    [Arguments("3) item", true)]
    [Arguments("(a) item", true)]
    [Arguments("b) item", true)]
    [Arguments("The item", false)]
    [Arguments("1234. item", false)]
    [Arguments("item", false)]
    public async Task MarkersAreRecognised(string text, bool marked) =>
        await Assert.That(ParagraphFinder.StartsWithMarker(text)).IsEqualTo(marked);

    /// <summary>A single line is a paragraph of its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ALoneLineIsItsOwnParagraph()
    {
        PageObjectInfo[] page = [Line(LoneIndex, First, "alone")];

        await Assert.That(ParagraphFinder.Find(page, page[First])).IsEquivalentTo([LoneIndex]);
    }

    /// <summary>Makes a line of the centred paragraph.</summary>
    /// <param name="row">The line's row, which also picks its width and is its number.</param>
    /// <returns>The object.</returns>
    private static PageObjectInfo Centred(int row) => Sized(row, row, Middle - (LineWidths[row] * Half), LineWidths[row]);

    /// <summary>Makes a line of the right aligned paragraph.</summary>
    /// <param name="row">The line's row, which also picks its width and is its number.</param>
    /// <returns>The object.</returns>
    private static PageObjectInfo AtRight(int row) => Sized(row, row, RightEdge - LineWidths[row], LineWidths[row]);

    /// <summary>Makes a line of text that starts and is as wide as given.</summary>
    /// <param name="index">The object's number.</param>
    /// <param name="row">How many lines down the page it is.</param>
    /// <param name="left">Where it starts.</param>
    /// <param name="width">How wide it is.</param>
    /// <returns>The object.</returns>
    private static PageObjectInfo Sized(int index, int row, float left, float width) => new(index, PageObjectKind.Text, new(left, row * Step, width, LineHeight), "words");

    /// <summary>Makes a line of text that reads at an angle, the full length of a paragraph's lines.</summary>
    /// <param name="index">The object's number.</param>
    /// <param name="row">How many lines on from the first line it is, in the way the lines follow one another.</param>
    /// <param name="degrees">The way the line reads.</param>
    /// <returns>The object.</returns>
    private static PageObjectInfo Turned(int index, int row, float degrees) => TurnedBy(index, row, degrees, TurnedLength);

    /// <summary>Makes a line of text that reads at an angle, centred on the same middle as the others of its paragraph.</summary>
    /// <param name="index">The object's number.</param>
    /// <param name="row">How many lines on from the first line it is.</param>
    /// <param name="degrees">The way the line reads.</param>
    /// <param name="length">How long the line is.</param>
    /// <returns>The object, whose bounds are the box round the turned line.</returns>
    private static PageObjectInfo TurnedBy(int index, int row, float degrees, float length)
    {
        var angle = degrees * (MathF.PI / HalfTurnDegrees);
        var directionX = MathF.Cos(angle);
        var directionY = -MathF.Sin(angle);

        // The next line is a quarter turn clockwise from the way a line reads: down the page for a line that reads to the right.
        var middleX = TurnedCentre - (directionY * row * Step);
        var middleY = TurnedCentre + (directionX * row * Step);
        var reachX = ((MathF.Abs(directionX) * length) + (MathF.Abs(directionY) * LineHeight)) * Half;
        var reachY = ((MathF.Abs(directionY) * length) + (MathF.Abs(directionX) * LineHeight)) * Half;
        return new(index, PageObjectKind.Text, new(middleX - reachX, middleY - reachY, reachX / Half, reachY / Half), "words") { Reading = new(directionX, directionY, length, LineHeight) };
    }

    /// <summary>Makes a line of text.</summary>
    /// <param name="index">The object's number.</param>
    /// <param name="row">How many lines down the page it is.</param>
    /// <param name="text">The words.</param>
    /// <returns>The object.</returns>
    private static PageObjectInfo Line(int index, int row, string text) => new(index, PageObjectKind.Text, new(Left, row * Step, Width, LineHeight), text);
}

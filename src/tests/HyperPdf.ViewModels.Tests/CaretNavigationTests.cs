// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the text caret moves by character and by line across lines and pages.</summary>
public sealed class CaretNavigationTests
{
    /// <summary>The characters on each line of the test pages.</summary>
    private const int LineLength = 2;

    /// <summary>The size of the test pages.</summary>
    private const float PageSide = 100F;

    /// <summary>The page with two lines of text.</summary>
    private const int FirstPage = 0;

    /// <summary>The page with no text.</summary>
    private const int BlankPage = 1;

    /// <summary>The last page, with two lines of text.</summary>
    private const int LastPage = 2;

    /// <summary>The first character of the second line.</summary>
    private const int SecondLine = 2;

    /// <summary>The last character of a page of four.</summary>
    private const int LastCharacter = 3;

    /// <summary>Stepping forward at the end of a page skips a page with no text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForwardCrossesToTheNextPageWithText()
    {
        using var document = Create();

        var next = CaretNavigation.Step(document, At(FirstPage, LastCharacter), 1);

        await Assert.That(next).IsEqualTo(At(LastPage, 0));
    }

    /// <summary>Stepping back at the start of a page goes to the last character of the previous page with text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BackCrossesToThePreviousPageWithText()
    {
        using var document = Create();

        var previous = CaretNavigation.Step(document, At(LastPage, 0), -1);

        await Assert.That(previous).IsEqualTo(At(FirstPage, LastCharacter));
    }

    /// <summary>There is nowhere to step past the ends of the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StepsStopAtTheEnds()
    {
        using var document = Create();

        await Assert.That(CaretNavigation.Step(document, At(LastPage, LastCharacter), 1)).IsEqualTo(CaretNavigation.None());
        await Assert.That(CaretNavigation.Step(document, At(FirstPage, 0), -1)).IsEqualTo(CaretNavigation.None());
    }

    /// <summary>A step inside a page moves one character.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StepInsideAPageMovesOneCharacter()
    {
        using var document = Create();

        var next = CaretNavigation.Step(document, At(FirstPage, 1), 1);

        await Assert.That(next).IsEqualTo(At(FirstPage, SecondLine));
    }

    /// <summary>Stepping down moves to the character below, and stepping up moves back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LineStepsKeepTheColumn()
    {
        using var document = Create();
        List<PageRect> scratch = [];

        var down = CaretNavigation.StepLine(document, At(FirstPage, 0), 1, scratch);
        var up = CaretNavigation.StepLine(document, down, -1, scratch);

        await Assert.That(down).IsEqualTo(At(FirstPage, SecondLine));
        await Assert.That(up).IsEqualTo(At(FirstPage, 0));
    }

    /// <summary>Stepping down from the last line goes to the first character of the next page with text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LastLineStepsToTheNextPage()
    {
        using var document = Create();
        List<PageRect> scratch = [];

        var down = CaretNavigation.StepLine(document, At(FirstPage, SecondLine), 1, scratch);

        await Assert.That(down).IsEqualTo(At(LastPage, 0));
    }

    /// <summary>Stepping up from the first line goes to the last character of the previous page with text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstLineStepsToThePreviousPage()
    {
        using var document = Create();
        List<PageRect> scratch = [];

        var up = CaretNavigation.StepLine(document, At(LastPage, 0), -1, scratch);

        await Assert.That(up).IsEqualTo(At(FirstPage, LastCharacter));
    }

    /// <summary>Nothing is below the last line of the last page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EndOfDocumentHasNoLineBelow()
    {
        using var document = Create();
        List<PageRect> scratch = [];

        var down = CaretNavigation.StepLine(document, At(LastPage, SecondLine), 1, scratch);

        await Assert.That(down).IsEqualTo(CaretNavigation.None());
    }

    /// <summary>A missing document is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingDocumentIsRefused() =>
        await Assert.That(static () => CaretNavigation.Step(null!, CaretNavigation.None(), 1)).Throws<ArgumentNullException>();

    /// <summary>A missing scratch list is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingScratchListIsRefused()
    {
        using var document = Create();

        await Assert.That(() => CaretNavigation.StepLine(document, CaretNavigation.None(), 1, null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Names a position in the test document.</summary>
    /// <param name="page">The page.</param>
    /// <param name="character">The character on the page.</param>
    /// <returns>The position.</returns>
    private static CharacterPosition At(int page, int character) => new(page, character);

    /// <summary>Makes three pages: two lines of text, none, and two lines of text.</summary>
    /// <returns>The document.</returns>
    private static FakeDocument Create() => new("caret.pdf", new PageSize(PageSide, PageSide), new PageSize(PageSide, PageSide), new PageSize(PageSide, PageSide))
    {
        LineLength = LineLength,
        PageTextFor = static page => page == BlankPage ? string.Empty : "abcd",
    };
}

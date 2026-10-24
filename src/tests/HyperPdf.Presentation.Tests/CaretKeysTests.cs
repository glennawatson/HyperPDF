// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Presentation.Tests;

/// <summary>Checks the keys that move the text caret.</summary>
public sealed class CaretKeysTests
{
    /// <summary>The characters on each line of the test pages.</summary>
    private const int LineLength = 2;

    /// <summary>The size of the test pages.</summary>
    private const float PageSide = 100F;

    /// <summary>The page with two lines of text.</summary>
    private const int FirstPage = 0;

    /// <summary>The page with no text.</summary>
    private const int BlankPage = 1;

    /// <summary>The first character of the second line.</summary>
    private const int SecondLine = 2;

    /// <summary>The last character of a page of four.</summary>
    private const int LastCharacter = 3;

    /// <summary>Each arrow key moves the caret the way it points, and other keys do not move it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrowKeysMoveTheCaret()
    {
        using var document = Create();
        List<PageRect> scratch = [];
        var from = At(FirstPage, 1);

        await Assert.That(CaretKeys.GetNext(document, from, ViewerKey.Right, scratch)).IsEqualTo(At(FirstPage, SecondLine));
        await Assert.That(CaretKeys.GetNext(document, from, ViewerKey.Left, scratch)).IsEqualTo(At(FirstPage, 0));
        await Assert.That(CaretKeys.GetNext(document, from, ViewerKey.Down, scratch)).IsEqualTo(At(FirstPage, LastCharacter));
        await Assert.That(CaretKeys.GetNext(document, from, ViewerKey.Enter, scratch)).IsEqualTo(CaretNavigation.None());
    }

    /// <summary>Only the four arrows are caret keys.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyArrowsAreCaretKeys()
    {
        await Assert.That(CaretKeys.IsCaretKey(ViewerKey.Up)).IsTrue();
        await Assert.That(CaretKeys.IsCaretKey(ViewerKey.Home)).IsFalse();
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

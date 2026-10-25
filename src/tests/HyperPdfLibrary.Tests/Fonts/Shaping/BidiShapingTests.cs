// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Mixed-direction text: runs are put in visual order (Unicode Standard Annex #9) and brackets mirror in right-to-left runs.</summary>
public sealed class BidiShapingTests
{
    /// <summary>Latin text with a Hebrew word: the word is reversed in place and the paragraph reads left to right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HebrewWordInLatinTextIsReversed()
    {
        var shaped = Fixture().Shape("abc אבג def");

        await Assert.That(shaped.RightToLeft).IsFalse();
        await Assert.That(shaped.Letters).IsEqualTo("abc ZYX def");
        await Assert.That(shaped.Clusters).IsEqualTo("0,1,2,3,6,5,4,7,8,9,10");
    }

    /// <summary>A right-to-left paragraph with digits: the digits keep their order and the rest reads from the right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DigitsInRightToLeftTextKeepTheirOrder()
    {
        var shaped = Fixture().Shape("אב 12 ג");

        await Assert.That(shaped.RightToLeft).IsTrue();
        await Assert.That(shaped.Letters).IsEqualTo("Z 12 YX");
    }

    /// <summary>Brackets in a right-to-left run use the mirrored glyphs, so they face the right way.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BracketsMirrorInRightToLeftText()
    {
        var shaped = Fixture().Shape("א(ב)");

        await Assert.That(shaped.Letters).IsEqualTo("(Y)X");
    }

    /// <summary>The caller can name the base direction, which makes left-to-right letters in a right-to-left paragraph keep their order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CallerCanSetTheBaseDirection()
    {
        var rightToLeft = Fixture().Shape("abc", new(PdfWritingMode.Horizontal) { Direction = PdfTextDirection.RightToLeft });
        var leftToRight = Fixture().Shape("אב", new(PdfWritingMode.Horizontal) { Direction = PdfTextDirection.LeftToRight });

        await Assert.That(rightToLeft.RightToLeft).IsTrue();
        await Assert.That(rightToLeft.Letters).IsEqualTo("abc");
        await Assert.That(leftToRight.RightToLeft).IsFalse();
        await Assert.That(leftToRight.Letters).IsEqualTo("YX");
    }

    /// <summary>Makes a font of Latin letters, three Hebrew letters, digits, a space and brackets.</summary>
    /// <returns>The fixture.</returns>
    private static ShapingFixture Fixture()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll("abcdef 12()");
        _ = fixture.AddMapped('X', 0x05D0, ShapingFixture.Advance);
        _ = fixture.AddMapped('Y', 0x05D1, ShapingFixture.Advance);
        _ = fixture.AddMapped('Z', 0x05D2, ShapingFixture.Advance);
        return fixture;
    }
}

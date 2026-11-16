// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Checks that Hangul jamo join into syllables and that the Thai and Lao "am" signs split when a font lacks them.</summary>
public sealed class SoutheastAsianAndHangulShapingTests
{
    /// <summary>The leading consonant giyeok.</summary>
    private const string Lead = "ᄀ";

    /// <summary>The vowel a.</summary>
    private const string Vowel = "ᅡ";

    /// <summary>The trailing consonant nieun.</summary>
    private const string Trail = "ᆫ";

    /// <summary>The syllable ga, which giyeok and a make.</summary>
    private const string Ga = "가";

    /// <summary>The syllable gan, which giyeok, a and nieun make.</summary>
    private const string Gan = "간";

    /// <summary>The Thai consonant ko kai.</summary>
    private const string ThaiKo = "ก";

    /// <summary>The Thai vowel sign am.</summary>
    private const string ThaiAm = "ำ";

    /// <summary>The Thai mark nikhahit.</summary>
    private const string ThaiMark = "ํ";

    /// <summary>The Thai vowel sign long a.</summary>
    private const string ThaiLongA = "า";

    /// <summary>A leading consonant and a vowel join into a syllable when the font has it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JamoJoinIntoASyllableTheFontHas()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll(Lead + Vowel + Trail + Ga + Gan);

        var two = fixture.Shape(Lead + Vowel);
        var three = fixture.Shape(Lead + Vowel + Trail);
        var already = fixture.Shape(Ga + Trail);

        using (Assert.Multiple())
        {
            await Assert.That(two.Letters).IsEqualTo(Ga);
            await Assert.That(three.Letters).IsEqualTo(Gan);
            await Assert.That(already.Letters).IsEqualTo(Gan);
        }
    }

    /// <summary>Jamo stay as they are when the font has no glyph for the syllable they make.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JamoStayWhenTheFontLacksTheSyllable()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll(Lead + Vowel + Trail);

        var shaped = fixture.Shape(Lead + Vowel + Trail);

        await Assert.That(shaped.Letters).IsEqualTo(Lead + Vowel + Trail);
    }

    /// <summary>A Thai "am" sign splits into the mark and the long a when the font has no glyph for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThaiAmSplitsWhenTheFontLacksIt()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll(ThaiKo + ThaiMark + ThaiLongA);

        var shaped = fixture.Shape(ThaiKo + ThaiAm);

        await Assert.That(shaped.Letters).IsEqualTo(ThaiKo + ThaiMark + ThaiLongA);
    }

    /// <summary>A Thai "am" sign stays one glyph when the font has it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThaiAmStaysWhenTheFontHasIt()
    {
        var fixture = new ShapingFixture();
        fixture.AddAll(ThaiKo + ThaiAm + ThaiMark + ThaiLongA);

        var shaped = fixture.Shape(ThaiKo + ThaiAm);

        await Assert.That(shaped.Letters).IsEqualTo(ThaiKo + ThaiAm);
    }
}

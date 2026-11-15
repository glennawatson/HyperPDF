// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Engine.Tests;

/// <summary>Shapes Khmer, Myanmar, Sinhala, Mongolian and Tibetan with the fonts that ship with the app and checks the order and forms of the glyphs.</summary>
public sealed class ScriptShapingTests
{
    /// <summary>The glyphs of a word that has two.</summary>
    private const int TwoGlyphs = 2;

    /// <summary>The glyphs of a word that has three.</summary>
    private const int ThreeGlyphs = 3;

    /// <summary>A vowel sign written before a Khmer base goes in front of it, with the subscript ro between.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KhmerPreBaseVowelAndSubscriptRoComeFirst()
    {
        var (font, glyphs) = Shape("ក្រេ");

        await Assert.That(glyphs.Count).IsEqualTo(ThreeGlyphs);
        await Assert.That(glyphs[0].Glyph).IsEqualTo(font.GetGlyph(0x17C1));
        await Assert.That(glyphs[2].Glyph).IsEqualTo(font.GetGlyph(0x1780));
    }

    /// <summary>A Khmer subscript ro is written before the base, and any other subscript after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KhmerSubscriptRoGoesBeforeTheBaseAndOtherSubscriptsAfter()
    {
        var (font, ro) = Shape("ក្រ");
        var (_, other) = Shape("ក្គ");

        await Assert.That(ro.Count).IsEqualTo(TwoGlyphs);
        await Assert.That(ro[1].Glyph).IsEqualTo(font.GetGlyph(0x1780));
        await Assert.That(other[0].Glyph).IsEqualTo(font.GetGlyph(0x1780));
    }

    /// <summary>A Khmer vowel sign that wraps the base splits, so its left part goes before the base.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KhmerSplitVowelHasAPartBeforeTheBase()
    {
        var (font, glyphs) = Shape("កើ");

        await Assert.That(glyphs.Count).IsEqualTo(ThreeGlyphs);
        await Assert.That(glyphs[1].Glyph).IsEqualTo(font.GetGlyph(0x1780));
    }

    /// <summary>A Myanmar vowel sign written before the base goes in front of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MyanmarVowelSignGoesBeforeTheBase()
    {
        var (font, glyphs) = Shape("ကေ");

        await Assert.That(glyphs.Count).IsEqualTo(TwoGlyphs);
        await Assert.That(glyphs[0].Glyph).IsEqualTo(font.GetGlyph(0x1031));
        await Assert.That(glyphs[1].Glyph).IsEqualTo(font.GetGlyph(0x1000));
    }

    /// <summary>A Myanmar kinzi, written first, is drawn after the base it sits over.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MyanmarKinziComesAfterTheBase()
    {
        var (font, glyphs) = Shape("င်္က");

        await Assert.That(glyphs[0].Glyph).IsEqualTo(font.GetGlyph(0x1000));
    }

    /// <summary>A Sinhala vowel sign written before the consonant goes in front of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SinhalaKombuvaGoesBeforeTheConsonant()
    {
        var (font, glyphs) = Shape("කෙ");

        await Assert.That(glyphs.Count).IsEqualTo(TwoGlyphs);
        await Assert.That(glyphs[0].Glyph).IsEqualTo(font.GetGlyph(0x0DD9));
        await Assert.That(glyphs[1].Glyph).IsEqualTo(font.GetGlyph(0x0D9A));
    }

    /// <summary>A Sinhala vowel sign that wraps the consonant has a part on each side of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SinhalaSplitVowelWrapsTheConsonant()
    {
        var (font, glyphs) = Shape("කො");

        await Assert.That(glyphs.Count).IsEqualTo(ThreeGlyphs);
        await Assert.That(glyphs[1].Glyph).IsEqualTo(font.GetGlyph(0x0D9A));
    }

    /// <summary>A Sinhala conjunct made with a joiner becomes one glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SinhalaConjunctIsOneGlyph()
    {
        var (_, glyphs) = Shape("ක්‍ෂ");

        await Assert.That(glyphs.Count).IsEqualTo(1);
    }

    /// <summary>A Mongolian letter takes a different glyph at the start, the middle and the end of a word.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MongolianLettersJoinIntoForms()
    {
        var (_, alone) = Shape("ᠠ");
        var (_, word) = Shape("ᠠᠡᠢ");

        await Assert.That(word.Count).IsEqualTo(ThreeGlyphs);
        await Assert.That(word[0].Glyph).IsNotEqualTo(alone[0].Glyph);
        await Assert.That(word[1].Glyph).IsNotEqualTo(word[0].Glyph);
        await Assert.That(word[2].Glyph).IsNotEqualTo(word[1].Glyph);
    }

    /// <summary>A stack of Tibetan letters becomes the font's glyph for the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TibetanStackIsOneGlyph()
    {
        var (_, glyphs) = Shape("ཀྵ");

        await Assert.That(glyphs.Count).IsEqualTo(1);
    }

    /// <summary>Shapes some text with the bundled font of its script.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The font and the shaped glyphs.</returns>
    /// <exception cref="InvalidOperationException">No bundled font has the text.</exception>
    private static ShapedText Shape(string text)
    {
        var face = new BundledScriptFonts().OpenFor(text) ?? throw new InvalidOperationException("No bundled font has the text.");
        var glyphs = new List<PdfShapedGlyph>();
        _ = new PdfOpenTypeShaper(face.Source).Shape(text, PdfShapingOptions.Default, glyphs);
        return new(face.Source, glyphs);
    }

    /// <summary>The font and the glyphs it shaped.</summary>
    /// <param name="Font">The font.</param>
    /// <param name="Glyphs">The shaped glyphs.</param>
    private sealed record ShapedText(PdfFontSource Font, List<PdfShapedGlyph> Glyphs);
}

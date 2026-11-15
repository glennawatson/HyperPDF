// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the fonts that ship with the app for scripts no installed font may have.</summary>
public sealed class BundledScriptFontsTests
{
    /// <summary>Every script with a bundled font opens, and its font has a letter of the script.</summary>
    /// <param name="codePoint">A letter of the script.</param>
    /// <param name="family">The font the letter should come from.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0x0531, "Noto Sans Armenian")]
    [Arguments(0x0915, "Noto Sans Devanagari")]
    [Arguments(0x0995, "Noto Sans Bengali")]
    [Arguments(0x0A15, "Noto Sans Gurmukhi")]
    [Arguments(0x0A95, "Noto Sans Gujarati")]
    [Arguments(0x0B15, "Noto Sans Oriya")]
    [Arguments(0x0B95, "Noto Sans Tamil")]
    [Arguments(0x0C15, "Noto Sans Telugu")]
    [Arguments(0x0C95, "Noto Sans Kannada")]
    [Arguments(0x0D15, "Noto Sans Malayalam")]
    [Arguments(0x0D9A, "Noto Sans Sinhala")]
    [Arguments(0x0E01, "Noto Sans Thai")]
    [Arguments(0x0E81, "Noto Sans Lao")]
    [Arguments(0x0F40, "Noto Serif Tibetan")]
    [Arguments(0x1000, "Noto Sans Myanmar")]
    [Arguments(0x10D0, "Noto Sans Georgian")]
    [Arguments(0x1200, "Noto Sans Ethiopic")]
    [Arguments(0x1780, "Noto Sans Khmer")]
    [Arguments(0x1820, "Noto Sans Mongolian")]
    public async Task EveryBundledScriptOpensAndHasItsLetters(int codePoint, string family)
    {
        var fonts = new BundledScriptFonts();

        var face = fonts.Open(codePoint);

        await Assert.That(face).IsNotNull();
        await Assert.That(face!.Source.FamilyName).IsEqualTo(family);
        await Assert.That(BundledScriptFonts.FamilyOf(BundledScriptFonts.IndexOf(codePoint))).IsEqualTo(family);
    }

    /// <summary>A character of a script with no bundled font, and a Latin letter, find no bundled font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScriptsWithoutABundledFontFindNone()
    {
        var fonts = new BundledScriptFonts();

        await Assert.That(fonts.Open('A')).IsNull();
        await Assert.That(fonts.Open(0x4E2D)).IsNull();
        await Assert.That(BundledScriptFonts.IndexOf(0x4E2D)).IsEqualTo(-1);
    }

    /// <summary>A font is opened once and shared by every later call.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFontIsOpenedOnce()
    {
        var fonts = new BundledScriptFonts();

        await Assert.That(fonts.Open(0x1780)).IsSameReferenceAs(fonts.Open(0x1781));
    }

    /// <summary>Text is given a font only when that font has every character of it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextNeedsAFontThatHasAllOfIt()
    {
        var fonts = new BundledScriptFonts();

        await Assert.That(fonts.OpenFor("កាសា")).IsNotNull();
        await Assert.That(fonts.OpenFor("ក ាសា")).IsNotNull();
        await Assert.That(fonts.OpenFor("កक")).IsNull();
    }
}

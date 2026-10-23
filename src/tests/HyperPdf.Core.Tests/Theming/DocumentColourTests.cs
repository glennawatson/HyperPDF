// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Theming;

namespace HyperPdf.Core.Tests.Theming;

/// <summary>Tests for the high-contrast and custom page colours.</summary>
public sealed class DocumentColourTests
{
    /// <summary>The contrast the comfort rules ask of text.</summary>
    private const double ReadableContrast = 7;

    /// <summary>A custom paper colour.</summary>
    private const uint NavyPaper = 0x102040U;

    /// <summary>A custom ink colour.</summary>
    private const uint CreamInk = 0xFFF0C0U;

    /// <summary>Each high-contrast pair is dark paper, light ink, and easy to read.</summary>
    /// <param name="choice">The choice.</param>
    /// <param name="ink">The expected ink.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageToneChoice.YellowOnBlack, 0xFFFF00U)]
    [Arguments(PageToneChoice.GreenOnBlack, 0x00FF00U)]
    [Arguments(PageToneChoice.WhiteOnBlack, 0xFFFFFFU)]
    public async Task HighContrastPairsAreReadable(PageToneChoice choice, uint ink)
    {
        var tone = ThemeResolver.GetTone(choice, ColorSchemes.Light);

        await Assert.That(tone.Paper).IsEqualTo(0U);
        await Assert.That(tone.Ink).IsEqualTo(ink);
        await Assert.That(ColorMath.Contrast(tone.Paper, tone.Ink)).IsGreaterThanOrEqualTo(ReadableContrast);
    }

    /// <summary>The custom paper and ink colours become the page tone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomColoursBecomeThePageTone()
    {
        var settings = new AppSettings { PageTone = PageToneChoice.Custom, CustomPaperColor = NavyPaper, CustomInkColor = CreamInk };

        var theme = ThemeResolver.Resolve(settings, null);

        await Assert.That(theme.PageTone.Paper).IsEqualTo(NavyPaper);
        await Assert.That(theme.PageTone.Ink).IsEqualTo(CreamInk);
    }

    /// <summary>Turning the page colour off still shows the document's own colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomColoursRespectTheToneSwitch()
    {
        var settings = new AppSettings { PageTone = PageToneChoice.Custom, PageToneEnabled = false };

        var theme = ThemeResolver.Resolve(settings, null);

        await Assert.That(theme.PageTone.IsIdentity).IsTrue();
    }

    /// <summary>A custom colour is kept in the settings file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomColoursAreSaved()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-custom-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new AppSettings { PageTone = PageToneChoice.Custom, CustomPaperColor = NavyPaper, CustomInkColor = CreamInk, KeepKeyboardCursor = true });

            var loaded = new SettingsStore(path).Load();

            await Assert.That(loaded.PageTone).IsEqualTo(PageToneChoice.Custom);
            await Assert.That(loaded.CustomPaperColor).IsEqualTo(NavyPaper);
            await Assert.That(loaded.CustomInkColor).IsEqualTo(CreamInk);
            await Assert.That(loaded.KeepKeyboardCursor).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Colours are read with or without a hash and written as six upper-case digits.</summary>
    /// <param name="text">The text.</param>
    /// <param name="valid">Whether it is a colour.</param>
    /// <param name="expected">The colour when valid.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("FFFF00", true, 0xFFFF00U)]
    [Arguments("#102040", true, 0x102040U)]
    [Arguments(" 00ff00 ", true, 0x00FF00U)]
    [Arguments("FFF", false, 0U)]
    [Arguments("GGGGGG", false, 0U)]
    [Arguments("", false, 0U)]
    [Arguments("-12345", false, 0U)]
    public async Task HexColoursAreParsed(string text, bool valid, uint expected)
    {
        var ok = ColorMath.TryParseHex(text, out var rgb);

        await Assert.That(ok).IsEqualTo(valid);
        await Assert.That(rgb).IsEqualTo(expected);
        await Assert.That(ColorMath.ToHex(0x00FF00U)).IsEqualTo("00FF00");
    }
}

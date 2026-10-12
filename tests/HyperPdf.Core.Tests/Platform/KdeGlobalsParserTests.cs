// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Platform.Linux.Kde;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests for <see cref="KdeGlobalsParser"/>.</summary>
public sealed class KdeGlobalsParserTests
{
    /// <summary>An excerpt of a Breeze Dark kdeglobals file.</summary>
    private const string BreezeDark = """
        [ColorEffects:Disabled]
        Color=56,56,56

        [Colors:Button]
        BackgroundNormal=41,44,48
        ForegroundNormal=252,252,252

        [Colors:Selection]
        BackgroundNormal=61,174,233
        ForegroundNormal=255,255,255

        [Colors:View]
        BackgroundNormal=20,22,24
        ForegroundNormal=252,252,252

        [Colors:Window]
        BackgroundNormal=32,35,38
        ForegroundInactive=161,169,177
        ForegroundNormal=252,252,252

        [General]
        ColorScheme=BreezeDark
        font=Noto Sans,10,-1,5,400,0,0,0,0,0,0,0,0,0,0,1
        """;

    /// <summary>Verifies colours, accent, font and darkness are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesBreezeDark()
    {
        const uint window = 0xFF202326U;
        const uint accent = 0xFF3DAEE9U;
        const uint view = 0xFF141618U;
        const double fontSize = 10;

        var palette = KdeGlobalsParser.Parse(BreezeDark);

        await Assert.That(palette).IsNotNull();
        await Assert.That(palette!.SchemeName).IsEqualTo("BreezeDark");
        await Assert.That(palette.WindowBackground).IsEqualTo(window);
        await Assert.That(palette.ViewBackground).IsEqualTo(view);
        await Assert.That(palette.Accent).IsEqualTo(accent);
        await Assert.That(palette.IsDark).IsTrue();
        await Assert.That(palette.FontFamily).IsEqualTo("Noto Sans");
        await Assert.That(palette.FontSizePoints).IsEqualTo(fontSize);
    }

    /// <summary>Verifies a file without window colours yields no palette.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingColorsGiveNull() => await Assert.That(KdeGlobalsParser.Parse("[General]\nfoo=bar")).IsNull();

    /// <summary>Verifies colour parsing including alpha and rejection of bad values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesColors()
    {
        const uint withAlpha = 0x80010203U;

        await Assert.That(KdeGlobalsParser.TryParseColor("1,2,3,128", out var color)).IsTrue();
        await Assert.That(color).IsEqualTo(withAlpha);
        await Assert.That(KdeGlobalsParser.TryParseColor("1,2", out _)).IsFalse();
        await Assert.That(KdeGlobalsParser.TryParseColor("1,2,300", out _)).IsFalse();
        await Assert.That(KdeGlobalsParser.TryParseColor(null, out _)).IsFalse();
    }

    /// <summary>Verifies the KDE motion and caret preferences are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesMotionAndCaret()
    {
        const int blinkRate = 1000;
        const string content = $"{BreezeDark}\n[KDE]\nAnimationDurationFactor=0\nCursorBlinkRate=1000\n";

        var palette = KdeGlobalsParser.Parse(content);

        await Assert.That(palette!.AnimationDurationFactor).IsEqualTo(0D);
        await Assert.That(palette.CursorBlinkRateMilliseconds).IsEqualTo(blinkRate);
        await Assert.That(KdeGlobalsParser.Parse(BreezeDark)!.AnimationDurationFactor).IsNull();
    }
}

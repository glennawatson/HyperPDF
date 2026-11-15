// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.Linux.Wayland;
using HyperPdf.Head.Linux.X11;

namespace HyperPdf.Windowing.Tests;

/// <summary>Tests reading the keyboard layout a Wayland compositor sends, so keys type what the user's layout says.</summary>
public sealed class XkbKeymapTests
{
    /// <summary>The key code of the first letter key of the top row.</summary>
    private const uint TopRowFirst = 24;

    /// <summary>The key code of the sixth letter key of the top row.</summary>
    private const uint TopRowSixth = 29;

    /// <summary>The key code of the tenth key of the home row.</summary>
    private const uint HomeRowTenth = 47;

    /// <summary>The key code of the first digit key.</summary>
    private const uint DigitFirst = 10;

    /// <summary>The level of a key with AltGr held.</summary>
    private const int ThirdLevel = 2;

    /// <summary>The level of a key with AltGr and Shift held.</summary>
    private const int FourthLevel = 3;

    /// <summary>A group the sample map does not have.</summary>
    private const int MissingGroup = 3;

    /// <summary>The groups of the sample map.</summary>
    private const int SampleGroups = 2;

    /// <summary>Gets a small map in the form compositors send: a German first group and a Russian second group on one key.</summary>
    private static ReadOnlySpan<byte> Sample => """
        xkb_keymap {
        xkb_keycodes "sample" {
            minimum = 8;
            maximum = 255;
            <AD01> = 24;
            <AD06> = 29;
            <AC10> = 47;
            <AE01> = 10;
            alias <LatQ> = <AD01>;
            indicator 1 = "Caps Lock";
        };
        xkb_types "sample" {
            type "TWO_LEVEL" { modifiers= Shift; map[Shift]= Level2; };
        };
        xkb_symbols "sample" {
            name[group1]="German";
            // A comment between keys.
            key <AD01> { [ q, Q, at, Greek_OMEGA ] };
            key <AD06> { type= "ALPHABETIC", symbols[Group1]= [ z, Z ], symbols[Group2]= [ Cyrillic_en, Cyrillic_EN ] };
            key <AC10> { [ odiaeresis, Odiaeresis ] };
            key <AE01> { type[group1]= "FOUR_LEVEL", symbols[Group1]= [ 1, exclam ], actions[Group1]= [ NoAction(), SetMods(modifiers=Shift) ] };
            modifier_map Shift { <LFSH> };
        };
        };
        """u8;

    /// <summary>Each key gives the symbol of its group and level, in Latin, Greek and Cyrillic.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeysGiveTheSymbolsOfTheirGroupAndLevel()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip.Test("Key symbol names are looked up in libX11, which is on Linux.");
        }

        var keymap = XkbKeymap.Parse(Sample);
        await Assert.That(keymap).IsNotNull();
        var plain = keymap!.Symbol(TopRowFirst, 0, 0);
        var third = keymap.Symbol(TopRowFirst, 0, ThirdLevel);
        var omega = X11Input.ToScalar(keymap.Symbol(TopRowFirst, 0, FourthLevel));
        var german = X11Input.ToScalar(keymap.Symbol(HomeRowTenth, 0, 0));
        var russian = X11Input.ToScalar(keymap.Symbol(TopRowSixth, 1, 0));
        var wrapped = keymap.Symbol(TopRowSixth, MissingGroup, 1);
        var shiftedDigit = keymap.Symbol(DigitFirst, 0, 1);

        await Assert.That(keymap.GroupCount).IsEqualTo(SampleGroups);
        await Assert.That(plain).IsEqualTo((uint)'q');
        await Assert.That(third).IsEqualTo((uint)'@');
        await Assert.That(omega).IsEqualTo((uint)'Ω');
        await Assert.That(german).IsEqualTo((uint)'ö');
        await Assert.That(russian).IsEqualTo((uint)'н');
        await Assert.That(wrapped).IsEqualTo((uint)'Z');
        await Assert.That(shiftedDigit).IsEqualTo((uint)'!');
    }

    /// <summary>Text that is not a keyboard map gives no layout, so keys fall back to their positions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWithoutKeysGivesNoLayout() => await Assert.That(XkbKeymap.Parse("xkb_keymap { };"u8)).IsNull();
}

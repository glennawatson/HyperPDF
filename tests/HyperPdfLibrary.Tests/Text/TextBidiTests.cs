// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the bidirectional levels and reordering, and their use when a line is extracted.</summary>
[NotInParallel]
public sealed class TextBidiTests
{
    /// <summary>Bet, a bracket and alef, drawn left to right: a bracket between two right-to-left letters.</summary>
    private const string BracketedContent = "BT /F1 10 Tf 10 100 Td (\\240\\(\\241) Tj ET";

    /// <summary>Alef, a European digit and bet, drawn left to right.</summary>
    private const string DigitContent = "BT /F1 10 Tf 10 100 Td (\\2401\\241) Tj ET";

    /// <summary>The right-to-left level.</summary>
    private const byte Odd = 1;

    /// <summary>The level of a number inside right-to-left text.</summary>
    private const byte Number = 2;

    /// <summary>The number of characters in the sample lines.</summary>
    private const int LineLength = 6;

    /// <summary>The levels of <see cref="MixedLine"/> in a left-to-right paragraph.</summary>
    private static readonly byte[] MixedLevels = [0, 0, 0, 0, Odd, Odd];

    /// <summary>The levels of <see cref="DigitsInHebrew"/> in a right-to-left paragraph.</summary>
    private static readonly byte[] DigitLevels = [Odd, Number, Number, Odd];

    /// <summary>The levels of an Arabic letter and two digits after it.</summary>
    private static readonly byte[] ArabicLevels = [Odd, Number, Number];

    /// <summary>The display order of <see cref="DigitLevels"/>: the run reversed, the digits kept in order.</summary>
    private static readonly int[] ReorderedDigits = [3, 1, 2, 0];

    /// <summary>Left-to-right letters, a space and two right-to-left letters.</summary>
    private static readonly TextBidiClass[] MixedLine =
    [
        TextBidiClass.L, TextBidiClass.L, TextBidiClass.L, TextBidiClass.WS, TextBidiClass.R, TextBidiClass.R,
    ];

    /// <summary>Right-to-left letters around two European digits.</summary>
    private static readonly TextBidiClass[] DigitsInHebrew =
    [
        TextBidiClass.R, TextBidiClass.EN, TextBidiClass.EN, TextBidiClass.R,
    ];

    /// <summary>Levels of a left-to-right paragraph give right-to-left letters level 1 and leave the rest at 0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightToLeftLettersGetLevelOne()
    {
        var types = (TextBidiClass[])MixedLine.Clone();
        var levels = new byte[LineLength];
        TextBidi.ResolveLevels(types, 0, levels);

        await Assert.That(levels).IsEquivalentTo(MixedLevels);
    }

    /// <summary>Digits after a right-to-left letter stay European numbers and rise to level 2 in a right-to-left paragraph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DigitsInRightToLeftTextRiseToLevelTwo()
    {
        var types = (TextBidiClass[])DigitsInHebrew.Clone();
        var levels = new byte[DigitsInHebrew.Length];
        TextBidi.ResolveLevels(types, Odd, levels);

        await Assert.That(levels).IsEquivalentTo(DigitLevels);
    }

    /// <summary>Arabic letters turn the digits after them into Arabic numbers (rule W2), which also rise to level 2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DigitsAfterArabicLettersBecomeArabicNumbers()
    {
        TextBidiClass[] types = [TextBidiClass.AL, TextBidiClass.EN, TextBidiClass.EN];
        var levels = new byte[types.Length];
        TextBidi.ResolveLevels(types, 0, levels);

        await Assert.That(types[1]).IsEqualTo(TextBidiClass.AN);
        await Assert.That(levels).IsEquivalentTo(ArabicLevels);
    }

    /// <summary>Reversing runs from the highest level down puts right-to-left runs in reverse and keeps the numbers inside them in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderReversesRunsFromTheTopLevel()
    {
        var levels = (byte[])DigitLevels.Clone();
        var order = new int[levels.Length];
        TextBidi.Reorder(levels, order);

        await Assert.That(order).IsEquivalentTo(ReorderedDigits);
    }

    /// <summary>Only lines with right-to-left letters or Arabic numbers need reordering.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyRightToLeftLinesNeedReordering()
    {
        await Assert.That(TextBidi.NeedsReordering([TextBidiClass.L, TextBidiClass.EN, TextBidiClass.WS])).IsFalse();
        await Assert.That(TextBidi.NeedsReordering(MixedLine)).IsTrue();
        await Assert.That(TextBidi.IsRightToLeftLine(MixedLine)).IsFalse();
        await Assert.That(TextBidi.IsRightToLeftLine(DigitsInHebrew)).IsTrue();
    }

    /// <summary>A bracket between right-to-left letters is reversed with them and shows its mirrored counterpart.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BracketsAreMirroredInRightToLeftRuns()
    {
        var page = TextTestDocument.Extract(BracketedContent);

        await Assert.That(page.Text).IsEqualTo("ב)א");
    }

    /// <summary>A digit between right-to-left letters keeps its place in the reversed run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DigitsKeepOrderInsideRightToLeftText()
    {
        var page = TextTestDocument.Extract(DigitContent);

        await Assert.That(page.Text).IsEqualTo("ב1א");
    }
}

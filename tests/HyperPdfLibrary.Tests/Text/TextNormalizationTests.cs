// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;
using TUnit.Assertions.Enums;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the decomposition and mark ordering that search folding is built on.</summary>
public sealed class TextNormalizationTests
{
    /// <summary>The combining class of the acute accent, which sits above its letter.</summary>
    private const int AboveClass = 230;

    /// <summary>The combining class of the dot below, which sits under its letter.</summary>
    private const int BelowClass = 220;

    /// <summary>The characters the longest compatibility decomposition (U+FDFA, a ligature of a whole phrase) has.</summary>
    private const int LongestCompatibilityLength = 18;

    /// <summary>The source index of the first character in the map test.</summary>
    private const int FirstSource = 10;

    /// <summary>The source index of the second character in the map test.</summary>
    private const int SecondSource = 11;

    /// <summary>The source index of the third character in the map test.</summary>
    private const int ThirdSource = 12;

    /// <summary>Verifies that precomposed letters decompose fully into a base letter and marks in canonical order.</summary>
    /// <param name="value">The precomposed character.</param>
    /// <param name="expected">The decomposition.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments('\U000000E9', "e\U00000301")]
    [Arguments('\U000000C5', "A\U0000030A")]
    [Arguments('\U0000212B', "A\U0000030A")]
    [Arguments('\U00001EA5', "a\U00000302\U00000301")]
    [Arguments('\U000001D6', "u\U00000308\U00000304")]
    public async Task Decompose_Canonical_ExpandsToBaseAndMarks(char value, string expected)
    {
        var buffer = new char[TextUnicodeData.LongestCompatibility];

        var length = TextNormalization.Decompose(value, false, buffer);

        await Assert.That(new string(buffer, 0, length)).IsEqualTo(expected);
    }

    /// <summary>Verifies that characters with no decomposition, and ASCII, are reported as not decomposing.</summary>
    /// <param name="value">The character.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments('a')]
    [Arguments('~')]
    [Arguments('\U000000F8')]
    [Arguments('\U000065E5')]
    [Arguments('\U00000627')]
    public async Task Decompose_CharacterWithNoDecomposition_WritesNothing(char value)
    {
        var buffer = new char[TextUnicodeData.LongestCompatibility];

        await Assert.That(TextNormalization.Decompose(value, true, buffer)).IsEqualTo(0);
    }

    /// <summary>Verifies that compatibility decomposition expands ligatures, superscripts, circled digits and units, which canonical decomposition leaves alone.</summary>
    /// <param name="value">The character.</param>
    /// <param name="expected">The compatibility decomposition.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments('\U0000FB01', "fi")]
    [Arguments('\U000000B2', "2")]
    [Arguments('\U00002460', "1")]
    [Arguments('\U0000338F', "kg")]
    [Arguments('\U00002163', "IV")]
    [Arguments('\U0000FF71', "\U000030A2")]
    [Arguments('\U000000E9', "e\U00000301")]
    public async Task Decompose_Compatibility_ExpandsCompatibilityForms(char value, string expected)
    {
        var buffer = new char[TextUnicodeData.LongestCompatibility];

        var length = TextNormalization.Decompose(value, true, buffer);

        await Assert.That(new string(buffer, 0, length)).IsEqualTo(expected);
    }

    /// <summary>Verifies that canonical decomposition leaves compatibility-only forms alone.</summary>
    /// <param name="value">The compatibility form.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments('\U0000FB01')]
    [Arguments('\U000000B2')]
    [Arguments('\U00002460')]
    public async Task Decompose_Canonical_LeavesCompatibilityFormsAlone(char value)
    {
        var buffer = new char[TextUnicodeData.LongestCompatibility];

        await Assert.That(TextNormalization.Decompose(value, false, buffer)).IsEqualTo(0);
    }

    /// <summary>Verifies that the longest compatibility decomposition fits the table's recorded maximum.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Decompose_LongestForm_FitsTheRecordedMaximum()
    {
        var buffer = new char[TextUnicodeData.LongestCompatibility];

        await Assert.That(TextNormalization.Decompose('\U0000FDFA', true, buffer)).IsEqualTo(LongestCompatibilityLength);
    }

    /// <summary>Verifies that the decomposed length counts the characters decomposition will write.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DecomposedLength_CountsExpandedCharacters()
    {
        await Assert.That(TextNormalization.DecomposedLength("plain ascii", false)).IsEqualTo("plain ascii".Length);
        await Assert.That(TextNormalization.DecomposedLength("caf\U000000E9", false)).IsEqualTo("caf\U000000E9".Length + 1);
        await Assert.That(TextNormalization.DecomposedLength("\U0000FB01x", false)).IsEqualTo("\U0000FB01x".Length);
        await Assert.That(TextNormalization.DecomposedLength("\U0000FB01x", true)).IsEqualTo("fix".Length);
    }

    /// <summary>Verifies the combining classes of an accent above a letter, a dot below it, and a letter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CombiningClass_ReportsAboveBelowAndNone()
    {
        await Assert.That((int)TextNormalization.CombiningClass('\U00000301')).IsEqualTo(AboveClass);
        await Assert.That((int)TextNormalization.CombiningClass('\U00000323')).IsEqualTo(BelowClass);
        await Assert.That((int)TextNormalization.CombiningClass('a')).IsEqualTo(0);
        await Assert.That((int)TextNormalization.CombiningClass('\U000065E5')).IsEqualTo(0);
    }

    /// <summary>Verifies that marks are put in ascending combining class, so two spellings of the same letter become one text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reorder_PutsMarksInCombiningClassOrder()
    {
        var chars = "a\U00000301\U00000323b".ToCharArray();

        TextNormalization.Reorder(chars);

        await Assert.That(new string(chars)).IsEqualTo("a\U00000323\U00000301b");
    }

    /// <summary>Verifies that marks of equal class keep their order, because canonical ordering is stable.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reorder_KeepsTheOrderOfEqualClasses()
    {
        var chars = "a\U00000301\U00000302".ToCharArray();

        TextNormalization.Reorder(chars);

        await Assert.That(new string(chars)).IsEqualTo("a\U00000301\U00000302");
    }

    /// <summary>Verifies that the source index of each mark moves with it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reorder_MovesTheMapWithTheMarks()
    {
        var chars = "a\U00000301\U00000323".ToCharArray();
        int[] map = [FirstSource, SecondSource, ThirdSource];

        TextNormalization.Reorder(chars, map);

        await Assert.That(new string(chars)).IsEqualTo("a\U00000323\U00000301");
        await Assert.That(map).IsEquivalentTo([FirstSource, ThirdSource, SecondSource], CollectionOrdering.Matching);
    }
}

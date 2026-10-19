// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the clusters a selection must not split: accented letters, Indic vowel signs and conjuncts joined by a virama.</summary>
public sealed class TextClusterTests
{
    /// <summary>
    /// Devanagari ka, virama and ssa (a conjunct), a space, ka with the vowel sign i, a space, then "e" and a combining acute
    /// accent. Characters: 0 to 2 conjunct, 3 space, 4 and 5 ka with sign, 6 space, 7 and 8 accented letter.
    /// </summary>
    private const string Clusters = "BT /F1 10 Tf 10 100 Td (\\020\\021\\022 \\020\\023 e\\004) Tj ET";

    /// <summary>The index of the virama.</summary>
    private const int Virama = 1;

    /// <summary>The index of the consonant ssa that the virama joins to ka.</summary>
    private const int Ssa = 2;

    /// <summary>The index of the space after the conjunct.</summary>
    private const int SpaceAfterConjunct = 3;

    /// <summary>The index of the second ka.</summary>
    private const int SecondKa = 4;

    /// <summary>The index of the vowel sign i.</summary>
    private const int VowelSign = 5;

    /// <summary>The index of the space before the accented letter.</summary>
    private const int SpaceBeforeLetter = 6;

    /// <summary>The index of the letter "e".</summary>
    private const int Letter = 7;

    /// <summary>The index of the combining acute accent.</summary>
    private const int Accent = 8;

    /// <summary>The characters of the conjunct.</summary>
    private const int ConjunctLength = 3;

    /// <summary>The characters of a consonant and its vowel sign.</summary>
    private const int SyllableLength = 2;

    /// <summary>The characters of a letter and its accent.</summary>
    private const int AccentedLetterLength = 2;

    /// <summary>A count that runs past the end of the page.</summary>
    private const int PastTheEnd = 100;

    /// <summary>Verifies that every character of a conjunct, a syllable with a vowel sign and an accented letter starts at its cluster's first character.</summary>
    /// <param name="index">The character index.</param>
    /// <param name="expected">The first character of its cluster.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, 0)]
    [Arguments(Virama, 0)]
    [Arguments(Ssa, 0)]
    [Arguments(SpaceAfterConjunct, SpaceAfterConjunct)]
    [Arguments(SecondKa, SecondKa)]
    [Arguments(VowelSign, SecondKa)]
    [Arguments(SpaceBeforeLetter, SpaceBeforeLetter)]
    [Arguments(Letter, Letter)]
    [Arguments(Accent, Letter)]
    public async Task ClusterStart_IsTheFirstCharacterOfTheCluster(int index, int expected)
    {
        var page = TextTestDocument.Extract(Clusters);

        await Assert.That(page.GetClusterStart(index)).IsEqualTo(expected);
    }

    /// <summary>Verifies that the end of a cluster is the index after its last character.</summary>
    /// <param name="index">The character index.</param>
    /// <param name="expected">The index after the cluster's last character.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, ConjunctLength)]
    [Arguments(Virama, ConjunctLength)]
    [Arguments(Ssa, ConjunctLength)]
    [Arguments(SpaceAfterConjunct, SecondKa)]
    [Arguments(SecondKa, SpaceBeforeLetter)]
    [Arguments(VowelSign, SpaceBeforeLetter)]
    [Arguments(Letter, Accent + 1)]
    [Arguments(Accent, Accent + 1)]
    public async Task ClusterEnd_IsTheIndexAfterTheClusterOfTheCharacter(int index, int expected)
    {
        var page = TextTestDocument.Extract(Clusters);

        await Assert.That(page.GetClusterEnd(index)).IsEqualTo(expected);
    }

    /// <summary>Verifies that a run of characters is widened to whole clusters at both ends.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClusterRange_WidensARunToWholeClusters()
    {
        var page = TextTestDocument.Extract(Clusters);

        await Assert.That(page.GetClusterRange(Virama, 1)).IsEqualTo(new(0, ConjunctLength));
        await Assert.That(page.GetClusterRange(VowelSign, 1)).IsEqualTo(new(SecondKa, SyllableLength));
        await Assert.That(page.GetClusterRange(Ssa, SyllableLength)).IsEqualTo(new(0, SecondKa));
        await Assert.That(page.GetClusterRange(Letter, 1)).IsEqualTo(new(Letter, AccentedLetterLength));
        await Assert.That(page.GetClusterRange(SecondKa, PastTheEnd)).IsEqualTo(new(SecondKa, page.CharCount - SecondKa));
    }

    /// <summary>Verifies that a run with no characters stays empty.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClusterRange_OfNothing_IsEmpty()
    {
        var page = TextTestDocument.Extract(Clusters);

        await Assert.That(page.GetClusterRange(Virama, 0).Count).IsEqualTo(0);
        await Assert.That(page.GetClusterRange(-1, 1).Count).IsEqualTo(0);
        await Assert.That(page.GetClusterRange(page.CharCount, 1).Count).IsEqualTo(0);
    }

    /// <summary>Verifies that plain letters are clusters of one, and that a character without text is a cluster of its own.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlainCharacters_AreClustersOfOne()
    {
        var page = TextTestDocument.Extract("BT /F1 10 Tf 10 100 Td (a\\002b) Tj ET");

        await Assert.That(page.GetClusterStart(1)).IsEqualTo(1);
        await Assert.That(page.GetClusterEnd(1)).IsEqualTo(SyllableLength);
        await Assert.That(page.GetClusterRange(0, 1)).IsEqualTo(new(0, 1));
        await Assert.That(page.GetClusterRange(SyllableLength, 1)).IsEqualTo(new(SyllableLength, 1));
    }

    /// <summary>Verifies that an index outside the page is refused.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClusterQueries_RefuseAnIndexOutsideThePage()
    {
        var page = TextTestDocument.Extract(Clusters);

        await Assert.That(() => page.GetClusterStart(-1)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => page.GetClusterEnd(page.CharCount)).Throws<ArgumentOutOfRangeException>();
    }
}

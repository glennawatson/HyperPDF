// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;
using TUnit.Assertions.Enums;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks that search treats the different spellings of one letter as one: a letter and its accent written separately or together, marks in any order, and compatibility forms.</summary>
[NotInParallel]
public sealed class TextNormalizedSearchTests
{
    /// <summary>
    /// "cafe" followed by a combining acute accent (code 4), then "café" with the precomposed letter. The page text is
    /// "cafe", the accent, a space, then "café": ten characters.
    /// </summary>
    private const string BothSpellings = "BT /F1 10 Tf 10 100 Td (cafe\\004 caf\\351) Tj ET";

    /// <summary>Two letters "a", each with a dot below (code 5) and an acute accent (code 4), written in opposite orders.</summary>
    private const string MarksInBothOrders = "BT /F1 10 Tf 10 100 Td (a\\005\\004 a\\004\\005) Tj ET";

    /// <summary>"x" with a one quarter sign, a space, and one half. The test font keeps these codes as their Latin-1 characters.</summary>
    private const string CompatibilityForms = "BT /F1 10 Tf 10 100 Td (x\\274 \\275) Tj ET";

    /// <summary>The characters of the first "café": "cafe" and its accent.</summary>
    private const int DecomposedLength = 5;

    /// <summary>The characters of the second "café".</summary>
    private const int PrecomposedLength = 4;

    /// <summary>The page text index of the second "café".</summary>
    private const int PrecomposedStart = 6;

    /// <summary>The characters of a letter with two marks.</summary>
    private const int LetterWithTwoMarks = 3;

    /// <summary>The page text index of the second letter with two marks.</summary>
    private const int SecondLetterStart = 4;

    /// <summary>The characters of "x" and the one quarter sign.</summary>
    private const int LetterAndQuarterLength = 2;

    /// <summary>The page text index of the one half.</summary>
    private const int HalfStart = 3;

    /// <summary>The compatibility decomposition of the one quarter sign: one, a fraction slash and four.</summary>
    private const string Quarter = "1\U00002044" + "4";

    /// <summary>The compatibility decomposition of the one half sign: one, a fraction slash and two.</summary>
    private const string Half = "1\U00002044" + "2";

    /// <summary>Verifies that a precomposed query finds both spellings of "café", and so does a query with the accent written separately.</summary>
    /// <param name="query">The query.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("caf\U000000E9")]
    [Arguments("cafe\U00000301")]
    public async Task EquivalentSpellingsOfALetter_AreFoundByEither(string query)
    {
        var page = TextTestDocument.Extract(BothSpellings);

        var matches = Find(page, query, PdfTextSearchOptions.None);

        await Assert.That(matches).IsEquivalentTo(
            [new PdfTextMatch(0, DecomposedLength), new PdfTextMatch(PrecomposedStart, PrecomposedLength)],
            CollectionOrdering.Matching);
    }

    /// <summary>Verifies that a base letter does not match inside an accented letter, however the accent is written, unless accents are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BaseLetter_DoesNotMatchInsideAnAccentedLetter()
    {
        var page = TextTestDocument.Extract(BothSpellings);

        var exact = Find(page, "cafe", PdfTextSearchOptions.None);
        var ignoring = Find(page, "cafe", PdfTextSearchOptions.IgnoreDiacritics);

        await Assert.That(exact).IsEmpty();
        await Assert.That(ignoring).IsEquivalentTo(
            [new PdfTextMatch(0, DecomposedLength), new PdfTextMatch(PrecomposedStart, PrecomposedLength)],
            CollectionOrdering.Matching);
    }

    /// <summary>Verifies that marks in a different order still match, because canonical ordering gives both the same text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MarksInADifferentOrder_Match()
    {
        var page = TextTestDocument.Extract(MarksInBothOrders);

        var matches = Find(page, "a\U00000301\U00000323", PdfTextSearchOptions.None);

        await Assert.That(matches).IsEquivalentTo(
            [new PdfTextMatch(0, LetterWithTwoMarks), new PdfTextMatch(SecondLetterStart, LetterWithTwoMarks)],
            CollectionOrdering.Matching);
    }

    /// <summary>Verifies that compatibility folding finds a fraction sign by its spelled-out form, and exact search does not.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FractionSigns_AreFoundByTheirSpelledOutFormsWhenFolded()
    {
        var page = TextTestDocument.Extract(CompatibilityForms);

        await Assert.That(page.Text).IsEqualTo("x\U000000BC \U000000BD");
        await Assert.That(Find(page, Quarter, PdfTextSearchOptions.None)).IsEmpty();
        await Assert.That(Find(page, $"x{Quarter}", PdfTextSearchOptions.FoldCompatibility)).IsEquivalentTo([new PdfTextMatch(0, LetterAndQuarterLength)], CollectionOrdering.Matching);
        await Assert.That(Find(page, Half, PdfTextSearchOptions.FoldCompatibility)).IsEquivalentTo([new PdfTextMatch(HalfStart, 1)], CollectionOrdering.Matching);
    }

    /// <summary>Finds every match of a query.</summary>
    /// <param name="page">The page.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <returns>The matches.</returns>
    private static List<PdfTextMatch> Find(PdfTextPage page, string query, PdfTextSearchOptions options)
    {
        var matches = new List<PdfTextMatch>();
        page.Find(query, options, matches);
        return matches;
    }
}

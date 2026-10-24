// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the search options: case, whole words, diacritics, compatibility variants and white space.</summary>
[NotInParallel]
public sealed class TextSearchModeTests
{
    /// <summary>Accented words drawn in Latin-1 octal codes: "Résumé" and "résumé" then "cafe".</summary>
    private const string Accents = "BT /F1 10 Tf 10 100 Td (R\\351sum\\351 r\\351sum\\351 cafe) Tj ET";

    /// <summary>Words with digits, an underscore and a hyphen.</summary>
    private const string WordShapes = "BT /F1 10 Tf 10 100 Td (cat cats cat_1 cat-x 2cat) Tj ET";

    /// <summary>Two lines, the second starting with the end of a phrase.</summary>
    private const string TwoLines = "BT /F1 10 Tf 10 100 Td (hello) Tj 0 -20 Td (world again) Tj ET";

    /// <summary>The number of matches of "resume" when accents are ignored.</summary>
    private const int BothResumes = 2;

    /// <summary>The query without accents.</summary>
    private const string Plain = "resume";

    /// <summary>The page text indexes of the characters kept when "e", a combining acute accent, "a" and a precomposed e acute are folded.</summary>
    private static readonly int[] FoldedMap = [0, 2, 3];

    /// <summary>Accents are only ignored on request, and case is ignored unless asked for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DiacriticsAreMatchedOnlyOnRequest()
    {
        var page = TextTestDocument.Extract(Accents);

        await Assert.That(Count(page, Plain, PdfTextSearchOptions.None)).IsEqualTo(0);
        await Assert.That(Count(page, Plain, PdfTextSearchOptions.IgnoreDiacritics)).IsEqualTo(BothResumes);
        await Assert.That(Count(page, "RESUME", PdfTextSearchOptions.IgnoreDiacritics)).IsEqualTo(BothResumes);
        await Assert.That(Count(page, Plain, PdfTextSearchOptions.IgnoreDiacritics | PdfTextSearchOptions.MatchCase)).IsEqualTo(1);
        await Assert.That(Count(page, "résumé", PdfTextSearchOptions.MatchCase)).IsEqualTo(1);
    }

    /// <summary>A match found after accents were dropped covers the accented characters, not a shifted range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FoldedMatchesMapBackToTheCharacters()
    {
        var page = TextTestDocument.Extract(Accents);
        var matches = new List<PdfTextMatch>();
        page.Find("cafe", PdfTextSearchOptions.IgnoreDiacritics, matches);

        await Assert.That(matches).IsEquivalentTo([new PdfTextMatch(page.Text.IndexOf("cafe", StringComparison.Ordinal), "cafe".Length)]);
    }

    /// <summary>Whole-word matching rejects letters, digits and underscores next to the match but allows hyphens and spaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WholeWordUsesWordCharacters()
    {
        var page = TextTestDocument.Extract(WordShapes);
        var starts = new List<int>();
        var matches = new List<PdfTextMatch>();
        page.Find("cat", PdfTextSearchOptions.WholeWord, matches);
        foreach (var match in matches)
        {
            starts.Add(match.Start);
        }

        await Assert.That(starts).IsEquivalentTo([0, page.Text.IndexOf("cat-x", StringComparison.Ordinal)]);
    }

    /// <summary>Spaces in a query match any white space in the page, including a line break, and spaces at the query's ends are ignored.</summary>
    /// <param name="query">The query.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("hello world")]
    [Arguments("hello   world")]
    [Arguments("  hello world  ")]
    public async Task QuerySpacesMatchAnyWhiteSpace(string query)
    {
        var page = TextTestDocument.Extract(TwoLines);

        await Assert.That(Count(page, query, PdfTextSearchOptions.None)).IsEqualTo(1);
        await Assert.That(Count(page, "   ", PdfTextSearchOptions.None)).IsEqualTo(0);
    }

    /// <summary>Compatibility folding treats quotes, dashes, spaces and fullwidth letters as their plain forms.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompatibilityFoldingMapsVariantsToPlainCharacters()
    {
        var folded = TextFolding.Fold("“Hi” – ｆｕｌｌ ’s", PdfTextSearchOptions.FoldCompatibility);
        var searched = new List<PdfTextMatch>();
        TextSearch.FindAll(folded, "\"hi\" - full 's", PdfTextSearchOptions.FoldCompatibility, searched, static (list, first, last) => list.Add(new(first, last)));

        await Assert.That(folded.Text).IsEqualTo("\"hi\" - full 's");
        await Assert.That(searched.Count).IsEqualTo(1);
    }

    /// <summary>Diacritic folding drops combining marks and reports the index of each remaining character.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DiacriticFoldingKeepsAnIndexMap()
    {
        var folded = TextFolding.Fold("éaé", PdfTextSearchOptions.IgnoreDiacritics);

        await Assert.That(folded.Text).IsEqualTo("eae");
        await Assert.That(folded.Map).IsEquivalentTo(FoldedMap);
    }

    /// <summary>Counts the matches of a query.</summary>
    /// <param name="page">The page.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <returns>The number of matches.</returns>
    private static int Count(PdfTextPage page, string query, PdfTextSearchOptions options)
    {
        var matches = new List<PdfTextMatch>();
        page.Find(query, options, matches);
        return matches.Count;
    }
}

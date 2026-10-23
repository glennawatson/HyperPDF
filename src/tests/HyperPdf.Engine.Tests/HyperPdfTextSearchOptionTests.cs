// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that the viewer's accent and look-alike character search options reach the library.</summary>
public sealed class HyperPdfTextSearchOptionTests
{
    /// <summary>The length of "resume".</summary>
    private const int ResumeLength = 6;

    /// <summary>The page index of the only page.</summary>
    private const int FirstPage = 0;

    /// <summary>Verifies that an accent-free query finds an accented word only when accents are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoreDiacritics_FindsAccentedWords()
    {
        using var input = new HyperPdfTestDocument(TextSearchSamples.CreateAccentedPage());

        var exact = Find(input, "resume", SearchOptions.None);
        var ignoring = Find(input, "resume", SearchOptions.IgnoreDiacritics);

        await Assert.That(exact).IsEmpty();
        await Assert.That(ignoring).IsEquivalentTo([new TextMatch(FirstPage, 0, ResumeLength)]);
    }

    /// <summary>Verifies that straight quotes and a hyphen find curly quotes and an en dash only when look-alike characters match.</summary>
    /// <param name="query">The query, written with straight quotes and a hyphen.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("\"quoted\"")]
    [Arguments("2024-25")]
    public async Task FoldCompatibility_FindsLookAlikeCharacters(string query)
    {
        using var input = new HyperPdfTestDocument(TextSearchSamples.CreateAccentedPage());

        var exact = Find(input, query, SearchOptions.None);
        var folded = Find(input, query, SearchOptions.FoldCompatibility);

        await Assert.That(exact).IsEmpty();
        await Assert.That(folded.Count).IsEqualTo(1);
        await Assert.That(folded[0].Length).IsEqualTo(query.Length);
    }

    /// <summary>Finds a query on the only page.</summary>
    /// <param name="input">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <returns>The matches.</returns>
    private static List<TextMatch> Find(HyperPdfTestDocument input, string query, SearchOptions options)
    {
        var matches = new List<TextMatch>();
        input.Document.Find(FirstPage, query, options, matches);
        return matches;
    }
}

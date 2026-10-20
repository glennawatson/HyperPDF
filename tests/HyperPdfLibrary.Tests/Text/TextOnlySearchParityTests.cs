// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>
/// Checks that whole-document search, which reads each page's text without making a text page, reports exactly the matches
/// a built text page reports.
/// </summary>
public sealed class TextOnlySearchParityTests
{
    /// <summary>The most pages of each file that are compared.</summary>
    private const int MaxPages = 12;

    /// <summary>The most queries taken from each page.</summary>
    private const int QueriesPerPage = 3;

    /// <summary>The shortest word used as a query.</summary>
    private const int MinWord = 3;

    /// <summary>The option combinations compared.</summary>
    private static readonly PdfTextSearchOptions[] OptionSets =
    [
        PdfTextSearchOptions.None,
        PdfTextSearchOptions.WholeWord,
        PdfTextSearchOptions.MatchCase,
        PdfTextSearchOptions.IgnoreDiacritics,
        PdfTextSearchOptions.FoldCompatibility | PdfTextSearchOptions.Consecutive,
    ];

    /// <summary>Whole-document search finds what a text page finds, across mixed reference files, in every option set.</summary>
    /// <param name="id">The reference file id.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("irs-w9-2024")]
    [Arguments("pypdf-arabic")]
    [Arguments("commons-apna-aap")]
    [Arguments("usgs-lost-city-2023")]
    [Arguments("pyhanko-ua-and-a")]
    public async Task ReferenceFilesGiveIdenticalMatches(string id, CancellationToken cancellationToken)
    {
        var path = ReferencePdfFiles.RequirePaths(id)[0];
        using var built = PdfDocumentReader.Open(await File.ReadAllBytesAsync(path, cancellationToken), null);
        using var streamed = PdfDocumentReader.Open(await File.ReadAllBytesAsync(path, cancellationToken), null);
        var queries = CollectQueries(built);

        foreach (var options in OptionSets)
        {
            foreach (var query in queries)
            {
                await AssertSame(built, streamed, query, options, cancellationToken);
            }
        }
    }

    /// <summary>Searching does not fill the text page cache, so the reader's pages stay cached.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchLeavesTheCacheAlone(CancellationToken cancellationToken)
    {
        var path = ReferencePdfFiles.RequirePaths("irs-w9-2024")[0];
        using var document = PdfDocumentReader.Open(await File.ReadAllBytesAsync(path, cancellationToken), null);
        var first = PdfDocumentText.GetTextPage(document, 0);

        await foreach (var unused in PdfDocumentText.FindAsync(document, "the", PdfTextSearchOptions.None, cancellationToken))
        {
            _ = unused;
        }

        await Assert.That(PdfDocumentText.GetTextPage(document, 0)).IsSameReferenceAs(first);
    }

    /// <summary>Takes a few words of each page's text as queries, plus a phrase of two words.</summary>
    /// <param name="document">The document, read through the text page path.</param>
    /// <returns>The distinct queries.</returns>
    private static List<string> CollectQueries(PdfDocument document)
    {
        var queries = new HashSet<string>(StringComparer.Ordinal);
        var pages = Math.Min(document.PageCount, MaxPages);
        for (var i = 0; i < pages; i++)
        {
            var words = PdfDocumentText.GetTextPage(document, i).Text.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
            var taken = 0;
            for (var w = 0; w < words.Length - 1 && taken < QueriesPerPage; w += Math.Max(1, words.Length / QueriesPerPage))
            {
                if (words[w].Length < MinWord)
                {
                    continue;
                }

                _ = queries.Add(words[w]);
                _ = queries.Add($"{words[w]} {words[w + 1]}");
                taken++;
            }
        }

        return [.. queries];
    }

    /// <summary>Compares the streamed search with the text page search for one query.</summary>
    /// <param name="built">A document whose pages are read as text pages.</param>
    /// <param name="streamed">A second document, searched with the whole-document search.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSame(PdfDocument built, PdfDocument streamed, string query, PdfTextSearchOptions options, CancellationToken cancellationToken)
    {
        var expected = new Dictionary<int, PdfTextMatch[]>();
        var matches = new List<PdfTextMatch>();
        for (var i = 0; i < built.PageCount; i++)
        {
            matches.Clear();
            PdfDocumentText.GetTextPage(built, i).Find(query, options, matches);
            if (matches.Count > 0)
            {
                expected[i] = [.. matches];
            }
        }

        var actual = new Dictionary<int, PdfTextMatch[]>();
        await foreach (var page in PdfDocumentText.FindAsync(streamed, query, options, cancellationToken))
        {
            actual[page.PageIndex] = page.Matches;
        }

        await Assert.That(actual.Keys).IsEquivalentTo(expected.Keys);
        foreach (var (index, pageMatches) in expected)
        {
            await Assert.That(actual[index]).IsEquivalentTo(pageMatches);
        }
    }
}

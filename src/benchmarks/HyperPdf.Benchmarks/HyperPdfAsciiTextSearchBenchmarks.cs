// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures folding and searching page text that is plain ASCII, which is what most pages hold.</summary>
public class HyperPdfAsciiTextSearchBenchmarks
{
    /// <summary>One paragraph of ASCII sample text.</summary>
    private const string Paragraph = "The editor reviewed every entry in the report before the team met to agree the three themes. ";

    /// <summary>The number of paragraphs in the sample page.</summary>
    private const int Paragraphs = 200;

    /// <summary>A query whose first letter is the commonest in English text.</summary>
    private const string CommonFirstLetterQuery = "entry in the report";

    /// <summary>A query whose first letter is rare in the text.</summary>
    private const string RareFirstLetterQuery = "themes";

    /// <summary>The page text.</summary>
    private string _text = string.Empty;

    /// <summary>The page text folded for case-insensitive search.</summary>
    private FoldedText _folded = new(string.Empty, null);

    /// <summary>The number of matches the last search found.</summary>
    private int _found;

    /// <summary>Builds the sample page text.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat(Paragraph, Paragraphs));
        _folded = TextFolding.Fold(_text, PdfTextSearchOptions.None);
    }

    /// <summary>Folds the page text, ignoring case.</summary>
    /// <returns>The folded length.</returns>
    [Benchmark]
    public int FoldIgnoringCase() => TextFolding.Fold(_text, PdfTextSearchOptions.None).Text.Length;

    /// <summary>Folds the page text, ignoring case and accents.</summary>
    /// <returns>The folded length.</returns>
    [Benchmark]
    public int FoldIgnoringAccents() => TextFolding.Fold(_text, PdfTextSearchOptions.IgnoreDiacritics).Text.Length;

    /// <summary>Finds a phrase that starts with a common letter.</summary>
    /// <returns>The match count.</returns>
    [Benchmark]
    public int SearchCommonFirstLetter()
    {
        _found = 0;
        TextSearch.FindAll(_folded, CommonFirstLetterQuery, PdfTextSearchOptions.None, this, static (benchmark, _, _) => benchmark._found++);
        return _found;
    }

    /// <summary>Finds a word that starts with a rarer letter.</summary>
    /// <returns>The match count.</returns>
    [Benchmark]
    public int SearchRareFirstLetter()
    {
        _found = 0;
        TextSearch.FindAll(_folded, RareFirstLetterQuery, PdfTextSearchOptions.None, this, static (benchmark, _, _) => benchmark._found++);
        return _found;
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>Measures page text search over folded text, text folding and bidirectional level resolution.</summary>
public class HyperPdfTextSearchBenchmarks
{
    /// <summary>One paragraph of sample text with accents and a repeated phrase.</summary>
    private const string Paragraph = "The résumé of the café owner lists every quick brown fox that jumped over the lazy dog. ";

    /// <summary>The number of paragraphs in the sample page.</summary>
    private const int Paragraphs = 200;

    /// <summary>The query searched for.</summary>
    private const string Query = "quick brown fox";

    /// <summary>The page text.</summary>
    private string _text = string.Empty;

    /// <summary>The page text folded for case-insensitive search.</summary>
    private FoldedText _folded = new(string.Empty, null);

    /// <summary>The classes of a mixed-direction line.</summary>
    private TextBidiClass[] _classes = [];

    /// <summary>The levels buffer.</summary>
    private byte[] _levels = [];

    /// <summary>The number of matches the last search found.</summary>
    private int _found;

    /// <summary>Builds the sample page text.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat(Paragraph, Paragraphs));
        _folded = TextFolding.Fold(_text, PdfTextSearchOptions.None);
        _classes = new TextBidiClass[_text.Length];
        _levels = new byte[_text.Length];
    }

    /// <summary>Finds every match in already folded text.</summary>
    /// <returns>The match count.</returns>
    [Benchmark]
    public int SearchFolded()
    {
        _found = 0;
        TextSearch.FindAll(_folded, Query, PdfTextSearchOptions.None, this, static (benchmark, _, _) => benchmark._found++);
        return _found;
    }

    /// <summary>Folds the page text, ignoring case and accents.</summary>
    /// <returns>The folded length.</returns>
    [Benchmark]
    public int FoldIgnoringAccents() => TextFolding.Fold(_text, PdfTextSearchOptions.IgnoreDiacritics).Text.Length;

    /// <summary>Classifies the text and resolves its bidirectional levels.</summary>
    /// <returns>The level of the last character.</returns>
    [Benchmark]
    public int ResolveLevels()
    {
        for (var i = 0; i < _text.Length; i++)
        {
            _classes[i] = TextUnicode.GetClass(_text[i]);
        }

        TextBidi.ResolveLevels(_classes, 0, _levels);
        return _levels[^1];
    }
}

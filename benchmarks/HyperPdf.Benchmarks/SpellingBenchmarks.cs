// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Spelling;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures finding misspelled words in a form field's text, done each time the text changes.</summary>
public class SpellingBenchmarks
{
    /// <summary>A long field entry with a few mistakes, numbers and abbreviations.</summary>
    private const string Text = "Please recieve the signed form from teh PDF office at 42 Station Road, and don't send the adress twice.";

    /// <summary>A caret inside "recieve".</summary>
    private const int CaretIndex = 10;

    /// <summary>No words are kept.</summary>
    private static readonly HashSet<string> NoneKept = [];

    /// <summary>The misspelled words, reused.</summary>
    private readonly List<TextRange> _found = [];

    /// <summary>Finds the misspelled words.</summary>
    /// <returns>How many there are.</returns>
    [Benchmark]
    public int FindMisspelled()
    {
        SpellingWords.FindMisspelled(Text, FakeSpellChecker.Instance, NoneKept, _found);
        return _found.Count;
    }

    /// <summary>Finds the word under the caret.</summary>
    /// <returns>The word's length.</returns>
    [Benchmark]
    public int WordAtCaret() => SpellingWords.WordAt(Text, CaretIndex).Length;
}

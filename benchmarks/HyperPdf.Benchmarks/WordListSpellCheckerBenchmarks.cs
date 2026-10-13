// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdf.Platform.Linux.Spelling;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the Linux word list checker against a word list the size of a system dictionary, and compares the correction
/// index with the simplest alternative: scanning every word for one within two letter changes.
/// </summary>
public class WordListSpellCheckerBenchmarks
{
    /// <summary>The words in the list, about the size of /usr/share/dict/british-english.</summary>
    private const int WordCount = 100_000;

    /// <summary>The most letter changes a correction may need.</summary>
    private const int MaxEdits = 2;

    /// <summary>The table rows kept while counting changes.</summary>
    private const int RowsKept = 2;

    /// <summary>The longest word compared.</summary>
    private const int MaxWordLength = 32;

    /// <summary>The shortest made-up word.</summary>
    private const int ShortestWord = 4;

    /// <summary>The range of made-up word lengths.</summary>
    private const uint LengthRange = 8;

    /// <summary>Spreads word numbers across the generator's range (Knuth's multiplicative hash).</summary>
    private const uint Spread = 2_654_435_761;

    /// <summary>The generator's multiplier (Numerical Recipes).</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The generator's increment (Numerical Recipes).</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>Where the dropped letter sits in the misspelling.</summary>
    private const int DroppedLetter = 2;

    /// <summary>The letters words are made from.</summary>
    private const string Letters = "etaoinshrdlucmfwypvbgkjqxz";

    /// <summary>The misspellings in the mixed run.</summary>
    private const int MixedCount = 32;

    /// <summary>The step between the words the mixed misspellings are made from.</summary>
    private const int MixedStride = 3119;

    /// <summary>The kinds of misspelling in the mixed run: a dropped letter, swapped letters, two changes, and no word.</summary>
    private const int MixedKinds = 4;

    /// <summary>A misspelling with a letter dropped.</summary>
    private const int DroppedKind = 0;

    /// <summary>A misspelling with two neighbouring letters swapped.</summary>
    private const int SwappedKind = 1;

    /// <summary>A misspelling with a letter dropped and another changed.</summary>
    private const int TwoChangesKind = 2;

    /// <summary>The word used for the checks, made from the same letters as the list.</summary>
    private string _misspelled = string.Empty;

    /// <summary>Misspellings of every kind and length, for the mixed run.</summary>
    private string[] _mixed = [];

    /// <summary>The words, for the scanning baseline.</summary>
    private string[] _words = [];

    /// <summary>The checker.</summary>
    private WordListSpellChecker _checker = null!;

    /// <summary>The word list file.</summary>
    private string _path = string.Empty;

    /// <summary>Writes a word list of made-up words and loads it, with the correction index already built.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // Made-up words from a fixed number sequence, so every run measures the same list.
        _words = new string[WordCount];
        for (var i = 0; i < WordCount; i++)
        {
            var seed = (uint)i * Spread;
            _words[i] = string.Create(ShortestWord + (int)(seed % LengthRange), seed, static (span, state) =>
            {
                for (var c = 0; c < span.Length; c++)
                {
                    state = (state * Multiplier) + Increment;
                    span[c] = Letters[(int)(state % (uint)Letters.Length)];
                }
            });
        }

        _path = Path.Combine(Path.GetTempPath(), string.Create(CultureInfo.InvariantCulture, $"hyperpdf-words-{Environment.ProcessId}"));
        File.WriteAllLines(_path, _words);
        _checker = new(_path);
        var word = _words[WordCount >> 1];
        _misspelled = string.Concat(word.AsSpan(0, DroppedLetter), word.AsSpan(DroppedLetter + 1));
        _mixed = new string[MixedCount];
        for (var i = 0; i < MixedCount; i++)
        {
            _mixed[i] = MakeTypo(_words[(i * MixedStride) % WordCount], i % MixedKinds);
        }

        _ = _checker.Suggest(_misspelled);
    }

    /// <summary>Deletes the word list.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _checker.Dispose();
        File.Delete(_path);
    }

    /// <summary>Checks a word, as is done for each word typed.</summary>
    /// <returns>Whether it is spelled correctly.</returns>
    [Benchmark]
    public bool CheckWord() => _checker.IsCorrect(_misspelled);

    /// <summary>Finds corrections with the correction index, as the field's menu does.</summary>
    /// <returns>How many were found.</returns>
    [Benchmark]
    public int Suggest() => _checker.Suggest(_misspelled).Count;

    /// <summary>Finds corrections for misspellings of every kind and length: one letter off, two off, and nothing close.</summary>
    /// <returns>How many were found.</returns>
    [Benchmark(OperationsPerInvoke = MixedCount)]
    public int SuggestMixed()
    {
        var found = 0;
        foreach (var typo in _mixed)
        {
            found += _checker.Suggest(typo).Count;
        }

        return found;
    }

    /// <summary>Loads the word list and finds the first corrections, which builds the correction index.</summary>
    /// <returns>How many were found.</returns>
    [Benchmark]
    public int FirstSuggestion()
    {
        // Disposing hands the index's arrays back to the pool, which is what lets the next load reuse them.
        using var checker = new WordListSpellChecker(_path);
        return checker.Suggest(_misspelled).Count;
    }

    /// <summary>Finds corrections by scanning every word, the baseline the correction index is chosen over.</summary>
    /// <returns>How many were found.</returns>
    [Benchmark(Baseline = true)]
    public int SuggestByScanning()
    {
        var found = 0;
        foreach (var word in _words)
        {
            if (EditDistance(_misspelled, word) <= MaxEdits)
            {
                found++;
            }
        }

        return found;
    }

    /// <summary>Makes a misspelling of a word.</summary>
    /// <param name="word">The word, at least four letters.</param>
    /// <param name="kind">The kind of misspelling; anything past the named kinds makes a word nothing is close to.</param>
    /// <returns>The misspelling.</returns>
    private static string MakeTypo(string word, int kind)
    {
        var middle = word.Length >> 1;
        return kind switch
        {
            DroppedKind => string.Concat(word.AsSpan(0, middle), word.AsSpan(middle + 1)),
            SwappedKind => string.Concat(word.AsSpan(0, middle - 1), [word[middle], word[middle - 1]], word.AsSpan(middle + 1)),
            TwoChangesKind => string.Concat(word.AsSpan(0, 1), "q", word.AsSpan(DroppedLetter, middle - DroppedLetter), word.AsSpan(middle + 1)),
            _ => new string('x', word.Length),
        };
    }

    /// <summary>Counts letter changes between two words, stopping once past the limit.</summary>
    /// <param name="first">The first word.</param>
    /// <param name="second">The second word.</param>
    /// <returns>The changes, or more than the limit.</returns>
    private static int EditDistance(ReadOnlySpan<char> first, ReadOnlySpan<char> second)
    {
        if (Math.Abs(first.Length - second.Length) > MaxEdits)
        {
            return MaxEdits + 1;
        }

        if (second.Length >= MaxWordLength)
        {
            return MaxEdits + 1;
        }

        // Two rows of the table, the previous and the current, used in turn.
        var width = second.Length + 1;
        Span<int> rows = stackalloc int[(MaxWordLength + 1) * RowsKept];
        for (var j = 0; j < width; j++)
        {
            rows[j] = j;
        }

        for (var i = 1; i <= first.Length; i++)
        {
            var previous = ((i - 1) & 1) * width;
            var current = (i & 1) * width;
            rows[current] = i;
            var best = i;
            for (var j = 1; j < width; j++)
            {
                var cost = first[i - 1] == second[j - 1] ? 0 : 1;
                rows[current + j] = Math.Min(Math.Min(rows[previous + j] + 1, rows[current + j - 1] + 1), rows[previous + j - 1] + cost);
                best = Math.Min(best, rows[current + j]);
            }

            if (best > MaxEdits)
            {
                return MaxEdits + 1;
            }
        }

        return rows[((first.Length & 1) * width) + second.Length];
    }
}

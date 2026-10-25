// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;
using HyperPdf.Core.Spelling;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Spelling;

/// <summary>Tests for <see cref="SpellingWords"/>, which finds the misspelled words in typed text.</summary>
public sealed class SpellingWordsTests
{
    /// <summary>A misspelled word.</summary>
    private const string Misspelled = "recieve";

    /// <summary>No words are kept.</summary>
    private static readonly HashSet<string> NoneKept = [];

    /// <summary>Misspelled words are found where they are, and numbers, codes, abbreviations and contractions are left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsMisspelledWords()
    {
        const string text = "Teh form, please recieve 42 x7 PDF ABC don't adress";
        List<TextRange> found = [];

        SpellingWords.FindMisspelled(text, FakeSpellChecker.Instance, NoneKept, found);

        await Assert.That(found.Select(static range => text.Substring(range.Start, range.Length))).IsEquivalentTo(["Teh", "recieve", "adress"]);
    }

    /// <summary>Kept words and an unavailable dictionary mark nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsKeptWordsAndMissingDictionaries()
    {
        List<TextRange> found = [];
        HashSet<string> kept = new(["RECIEVE"], StringComparer.OrdinalIgnoreCase);
        SpellingWords.FindMisspelled(Misspelled, FakeSpellChecker.Instance, kept, found);
        await Assert.That(found).IsEmpty();

        SpellingWords.FindMisspelled(Misspelled, NullSpellChecker.Instance, NoneKept, found);
        await Assert.That(found).IsEmpty();
    }

    /// <summary>The word under a place in the text is found, including at its very end.</summary>
    /// <param name="index">The place.</param>
    /// <param name="start">The word's start.</param>
    /// <param name="length">The word's length, 0 for none.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, 0, 3)]
    [Arguments(3, 0, 3)]
    [Arguments(6, 4, 5)]
    [Arguments(10, 10, 0)]
    public async Task FindsWordAtPlace(int index, int start, int length)
    {
        const string text = "the don't  x";
        var word = SpellingWords.WordAt(text, index);

        await Assert.That(word.Length).IsEqualTo(length);
        if (length > 0)
        {
            await Assert.That(word.Start).IsEqualTo(start);
        }
    }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Speech.English;
using HyperPdf.Speech.Kokoro;

namespace HyperPdf.Core.Tests.Speech;

/// <summary>Tests for <see cref="EnglishPhonemizer"/>, against an extract of the misaki lexicon.</summary>
public sealed class EnglishPhonemizerTests
{
    /// <summary>Gets a new phonemizer; each test has its own, as a phonemizer reuses its buffers and is not thread safe.</summary>
    private static EnglishPhonemizer Phonemizer => new(PronunciationLexicon.Load(Lexicon.ToArray()), false);

    /// <summary>Gets an extract of misaki's us_gold.json (Apache-2.0).</summary>
    private static ReadOnlySpan<byte> Lexicon => """
        {"hello": "həlˈO", "world": "wˈɜɹld", "walk": "wˈɔk", "write": "ɹˈIt", "box": "bˈɑks", "screen": "skɹˈin", "reader": "ɹˈidəɹ",
         "P": "pˈi", "D": "dˈi", "F": "ˈɛf", "nineteen": "nˌIntˈin", "eighty": "ˈAɾi", "four": "fˈɔɹ", "forty": "fˈɔɹɾi", "two": "tˈu",
         "point": "pˈYnt", "five": "fˈIv", "percent": "pəɹsˈɛnt", "book": "bˈʊk", "apple": "ˈæpᵊl", "cat": "kˈæt",
         "read": {"ADJ": "ɹˈɛd", "DEFAULT": "ɹˈid", "VBD": "ɹˈɛd"}, "A": "ˈA"}
        """u8;

    /// <summary>Verifies words, endings, compounds, acronyms, numbers and the small words come out as misaki would say them.</summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The phonemes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Hello world.", "həlˈO wˈɜɹld.")]
    [Arguments("walked boxes writing cats", "wˈɔkt bˈɑksᵻz ɹˈIɾɪŋ kˈæts")]
    [Arguments("PDF", "pˌidˌiˈɛf")]
    [Arguments("screenreader", "skɹˈinɹˌidəɹ")]
    [Arguments("1984", "nˌIntˈin ˈAɾi fˈɔɹ")]
    [Arguments("42.5%", "fˈɔɹɾi tˈu pˈYnt fˈIv pəɹsˈɛnt")]
    [Arguments("the apple, the book; a cat", "ði ˈæpᵊl, ðə bˈʊk; ɐ kˈæt")]
    [Arguments("read", "ɹˈid")]
    public async Task Phonemizes(string text, string expected) => await Assert.That(Phonemizer.Phonemize(text)).IsEqualTo(expected);

    /// <summary>Verifies phonemes become Kokoro token ids, dropping characters it does not know.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TokenizesForKokoro()
    {
        List<long> tokens = [];
        KokoroVocabulary.Tokenize("həlˈO☃", tokens);

        await Assert.That(tokens.Count).IsEqualTo("həlˈO".Length);
        await Assert.That(tokens.TrueForAll(static id => id > 0)).IsTrue();
    }
}

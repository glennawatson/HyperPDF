// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Speech;
using HyperPdf.Http.Speech;
using HyperPdf.Speech.English;
using HyperPdf.Speech.Kokoro;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures Read Aloud's text work: splitting a page into sentences, preparing a sentence, turning it into phonemes and
/// Kokoro tokens, and the Azure SSML and PCM conversions. The neural voice itself needs its downloaded model, so it is
/// not run here. Allocations come from the EventPipe trace.
/// </summary>
public class SpeechBenchmarks
{
    /// <summary>The paragraphs on the benchmark page.</summary>
    private const int Paragraphs = 12;

    /// <summary>The bytes of a second of 24 kHz 16 bit audio.</summary>
    private const int SecondOfPcm = 48_000;

    /// <summary>A brisk reading speed.</summary>
    private const float Brisk = 1.25F;

    /// <summary>A sentence read aloud.</summary>
    private const string Sentence = "Hello world, the reader walks to the screen and writes 1984 in the box.";

    /// <summary>The sentences found, reused so only the library's allocations are measured.</summary>
    private readonly List<SpeechSentence> _sentences = [];

    /// <summary>The tokens, reused.</summary>
    private readonly List<long> _tokens = [];

    /// <summary>A second of silence as PCM.</summary>
    private readonly byte[] _pcm = new byte[SecondOfPcm];

    /// <summary>A page of text.</summary>
    private string _page = string.Empty;

    /// <summary>The phonemizer.</summary>
    private EnglishPhonemizer _phonemizer = null!;

    /// <summary>The sentence's phonemes.</summary>
    private string _phonemes = string.Empty;

    /// <summary>Builds the page and the phonemizer.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var page = new StringBuilder();
        for (var i = 0; i < Paragraphs; i++)
        {
            _ = page.Append("Dr. Smith reads the report aloud. It covers 3.5 percent growth over the year!\r\nThe next line con-\r\ntinues here; does it end? Yes.\r\n");
        }

        _page = page.ToString();
        var lexicon = """
            {"hello": "həlˈO", "world": "wˈɜɹld", "the": "ðə", "reader": "ɹˈidəɹ", "walks": "wˈɔks", "to": "tˈu", "screen": "skɹˈin",
             "and": "ænd", "writes": "ɹˈIts", "in": "ɪn", "box": "bˈɑks", "nineteen": "nˌIntˈin", "eighty": "ˈAɾi", "four": "fˈɔɹ"}
            """u8;
        _phonemizer = new(PronunciationLexicon.Load(lexicon.ToArray()), false);
        _phonemes = _phonemizer.Phonemize(Sentence);
    }

    /// <summary>Splits a page into sentences.</summary>
    /// <returns>The sentence count.</returns>
    [Benchmark]
    public int SplitPage()
    {
        _sentences.Clear();
        SentenceSplitter.Split(_page, _sentences);
        return _sentences.Count;
    }

    /// <summary>Prepares a sentence that runs over a line break for the voice.</summary>
    /// <returns>The text.</returns>
    [Benchmark]
    public string PrepareSentence() => SentenceSplitter.ToSpeech("The next line con-\r\ntinues here; does it end?");

    /// <summary>Turns written English with money, dates, times, units and abbreviations into the words said.</summary>
    /// <returns>The spoken text.</returns>
    [Benchmark]
    public string Normalize() => TextNormalizer.Normalize("Dr. Smith paid $1,200.50 on 2024-03-12 at 10:30 for 12 km of road, e.g. the 3rd section [4].", false);

    /// <summary>Turns a sentence into phonemes.</summary>
    /// <returns>The phonemes.</returns>
    [Benchmark]
    public string Phonemize() => _phonemizer.Phonemize(Sentence);

    /// <summary>Turns phonemes into Kokoro tokens.</summary>
    /// <returns>The token count.</returns>
    [Benchmark]
    public int Tokenize()
    {
        _tokens.Clear();
        KokoroVocabulary.Tokenize(_phonemes, _tokens);
        return _tokens.Count;
    }

    /// <summary>Builds the SSML sent to Azure for a sentence.</summary>
    /// <returns>The SSML.</returns>
    [Benchmark]
    public string BuildSsml() => AzureSpeechEngine.BuildSsml(Sentence, "en-GB-SoniaNeural", Brisk);

    /// <summary>Converts a second of Azure's PCM into samples.</summary>
    /// <returns>The sample count.</returns>
    [Benchmark]
    public int ConvertPcm() => AzureSpeechEngine.ToSamples(_pcm).Length;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// The files and voices of MeloTTS-English (MIT): the synthesizer, the first ten layers of bert-base-uncased
/// (Apache-2.0) that give it sentence-level prosody, the CMU dictionary and g2p_en's spelling-to-sound network. About
/// 270 MB from this repository's <see cref="VoiceRelease"/>, downloaded once into the voice folder.
/// </summary>
public static class MeloModel
{
    /// <summary>Gets the synthesizer's file name.</summary>
    public static string ModelFile => "melo-en.onnx";

    /// <summary>Gets the symbol table's file name.</summary>
    public static string SymbolsFile => "melo-en.json";

    /// <summary>Gets the BERT model's file name.</summary>
    public static string BertFile => "bert-en.onnx";

    /// <summary>Gets the BERT vocabulary's file name.</summary>
    public static string VocabularyFile => "bert-en-vocab.txt";

    /// <summary>Gets the dictionary's file name.</summary>
    public static string LexiconFile => "melo-en-lexicon.txt";

    /// <summary>Gets the spelling-to-sound network's file name.</summary>
    public static string SpellingFile => "g2p-en.bin";

    /// <summary>Gets the voices offered, Australian first: each is one of the model's English speakers.</summary>
    public static IReadOnlyList<SpeechVoice> Voices { get; } =
    [
        new("EN-AU", "Australian", "Australian English"),
        new("EN-BR", "British", "British English"),
        new("EN-US", "American", "American English"),
        new("EN_INDIA", "Indian", "Indian English"),
    ];

    /// <summary>Gets the files to download.</summary>
    public static IReadOnlyList<SpeechModelFile> Files { get; } =
    [
        VoiceRelease.File(ModelFile, ModelFile),
        VoiceRelease.File(SymbolsFile, SymbolsFile),
        VoiceRelease.File(BertFile, BertFile),
        VoiceRelease.File(VocabularyFile, VocabularyFile),
        VoiceRelease.File(LexiconFile, LexiconFile),
        VoiceRelease.File(SpellingFile, SpellingFile),
    ];

    /// <summary>Determines whether a voice reads dates and spellings the British way, as Australian and Indian English do.</summary>
    /// <param name="voiceId">The voice.</param>
    /// <returns><see langword="true"/> unless the voice is American.</returns>
    public static bool IsBritish(string voiceId) => !string.Equals(voiceId, "EN-US", StringComparison.Ordinal);
}

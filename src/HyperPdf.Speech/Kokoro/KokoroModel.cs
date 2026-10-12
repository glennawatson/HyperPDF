// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Speech;

namespace HyperPdf.Speech.Kokoro;

/// <summary>
/// The files and voices of Kokoro-82M (Apache-2.0): the unsigned 8 bit ONNX model from onnx-community, a handful of voices,
/// and the misaki pronunciation dictionaries, mirrored in this repository's <see cref="VoiceRelease"/>. About 190 MB,
/// downloaded once into the voice folder.
/// </summary>
public static class KokoroModel
{
    /// <summary>Kokoro's sample rate in hertz.</summary>
    private const int Hertz = 24_000;

    /// <summary>
    /// Gets the model's file name inside the voice folder. The unsigned 8 bit export is used: on CPUs it synthesises
    /// about five times faster than the default quantised export (which is slower than real time, leaving gaps between
    /// sentences), the half precision export can overflow to silence on some voices, and it is about half the size of
    /// the full precision model.
    /// </summary>
    public static string ModelFile => "model_uint8.onnx";

    /// <summary>Gets the audio sample rate Kokoro produces.</summary>
    public static int SampleRate => Hertz;

    /// <summary>Gets the voices offered: natural sounding American and British English voices.</summary>
    public static IReadOnlyList<SpeechVoice> Voices { get; } =
    [
        new("af_heart", "Heart", "American English, warm"),
        new("af_bella", "Bella", "American English, bright"),
        new("am_michael", "Michael", "American English, calm"),
        new("bf_emma", "Emma", "British English, gentle"),
        new("bm_george", "George", "British English, steady"),
    ];

    /// <summary>Gets the files to download.</summary>
    public static IReadOnlyList<SpeechModelFile> Files { get; } = BuildFiles();

    /// <summary>Gets the local path of a voice pack.</summary>
    /// <param name="voiceId">The voice.</param>
    /// <returns>The path inside the voice folder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string VoiceFile(string voiceId) => Path.Combine("voices", $"{voiceId}.bin");

    /// <summary>Determines whether a voice speaks British English, from its id's first letter.</summary>
    /// <param name="voiceId">The voice.</param>
    /// <returns><see langword="true"/> for British voices.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBritish(string voiceId) => voiceId.StartsWith('b');

    /// <summary>Gets the dictionaries for an accent, gold first.</summary>
    /// <param name="british">Whether British.</param>
    /// <returns>The dictionary file names.</returns>
    public static string[] LexiconFiles(bool british) => british ? ["gb_gold.json", "gb_silver.json"] : ["us_gold.json", "us_silver.json"];

    /// <summary>Lists the files to download.</summary>
    /// <returns>The files.</returns>
    private static List<SpeechModelFile> BuildFiles()
    {
        var files = new List<SpeechModelFile> { VoiceRelease.File($"kokoro-{ModelFile}", ModelFile) };
        foreach (var voice in Voices)
        {
            files.Add(VoiceRelease.File($"kokoro-{voice.Id}.bin", VoiceFile(voice.Id)));
        }

        foreach (var british in (ReadOnlySpan<bool>)[false, true])
        {
            foreach (var lexicon in LexiconFiles(british))
            {
                files.Add(VoiceRelease.File($"misaki-{lexicon}", lexicon));
            }
        }

        return files;
    }
}

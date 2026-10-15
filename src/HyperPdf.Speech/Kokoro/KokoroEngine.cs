// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Speech;
using HyperPdf.Speech.English;
using Microsoft.ML.OnnxRuntime;

namespace HyperPdf.Speech.Kokoro;

/// <summary>
/// Reads aloud on this computer with Kokoro-82M through ONNX Runtime: natural sounding voices, no account and nothing
/// sent anywhere. Text becomes phonemes (misaki), phonemes become token ids, and the model turns them and the voice's
/// style into 24 kHz audio.
/// </summary>
[DebuggerDisplay("KokoroEngine: {Name}, ready={IsReady}")]
public sealed class KokoroEngine : ISpeechEngine
{
    /// <summary>The most phoneme tokens the model reads at once.</summary>
    private const int MaxTokens = 510;

    /// <summary>The length of a voice's style vector.</summary>
    private const int StyleLength = 256;

    /// <summary>The padding tokens around each window, one at each end.</summary>
    private const int PadTokens = 2;

    /// <summary>The processors left to the rest of the app (drawing pages, the window) while speech is made.</summary>
    private const int ReservedProcessors = 1;

    /// <summary>The most threads inference uses, leaving the rest of the computer responsive.</summary>
    private const int MaxThreads = 4;

    /// <summary>The voice folder.</summary>
    private readonly string _directory;

    /// <summary>Serialises inference and lazy loading.</summary>
    private readonly Lock _gate = new();

    /// <summary>The loaded voices' style vectors.</summary>
    private readonly Dictionary<string, float[]> _styles = [with(StringComparer.Ordinal)];

    /// <summary>The phonemizers by accent, loaded on first use.</summary>
    private readonly Dictionary<bool, EnglishPhonemizer> _phonemizers = [];

    /// <summary>The buffers text is normalized into, reused under the gate.</summary>
    private readonly NormalizerBuffers _normalizing = new();

    /// <summary>The token ids, reused.</summary>
    private readonly List<long> _tokens = [];

    /// <summary>The ONNX session, loaded on first use.</summary>
    private InferenceSession? _session;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="KokoroEngine"/> class.</summary>
    /// <param name="directory">The folder holding the model, voices and dictionaries.</param>
    public KokoroEngine(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
    }

    /// <inheritdoc/>
    public string Name => "On this computer (Kokoro)";

    /// <inheritdoc/>
    public bool IsReady
    {
        get
        {
            foreach (var file in KokoroModel.Files)
            {
                if (!File.Exists(Path.Combine(_directory, file.LocalName)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<SpeechVoice> Voices => KokoroModel.Voices;

    /// <summary>Gets a value indicating whether the ONNX session is loaded, which tests use to check that disposal frees it.</summary>
    internal bool IsSessionLoaded => Volatile.Read(ref _session) is not null;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            ReleaseIfDisposed();
        }
    }

    /// <inheritdoc/>
    public Task<SpeechAudio> SynthesizeAsync(string text, string voiceId, float speed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(voiceId);
        return Task.Run(() => Synthesize(text, voiceId, speed, cancellationToken), cancellationToken);
    }

    /// <summary>Turns text into the phonemes the voice would read, for tests and diagnostics.</summary>
    /// <param name="text">The text.</param>
    /// <param name="voiceId">The voice, which sets the accent.</param>
    /// <returns>The phonemes.</returns>
    public string Phonemize(string text, string voiceId)
    {
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                var british = KokoroModel.IsBritish(voiceId);
                return new(GetPhonemizer(british).Phonemize(TextNormalizer.Normalize(text.AsSpan(), british, _normalizing)));
            }
        }
        finally
        {
            ReleaseIfDisposed();
        }
    }

    /// <summary>Runs the model on one window of tokens.</summary>
    /// <param name="session">The session.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="style">The voice's styles, one row of 256 per token count.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="samples">The samples so far.</param>
    private static void Infer(InferenceSession session, ReadOnlySpan<long> tokens, float[] style, float speed, List<float> samples)
    {
        var ids = new long[tokens.Length + PadTokens];
        ids[0] = KokoroVocabulary.Pad;
        tokens.CopyTo(ids.AsSpan(1));
        ids[^1] = KokoroVocabulary.Pad;

        // The voice pack has one style per phoneme count: n phonemes use row n - 1.
        var row = Math.Clamp(tokens.Length - 1, 0, (style.Length / StyleLength) - 1);
        var styleRow = style.AsSpan(row * StyleLength, StyleLength).ToArray();
        var inputNames = session.InputMetadata.ContainsKey("input_ids") ? "input_ids" : "tokens";
        using var idsValue = OrtValue.CreateTensorValueFromMemory(ids, [1, ids.Length]);
        using var styleValue = OrtValue.CreateTensorValueFromMemory(styleRow, [1, StyleLength]);
        using var speedValue = OrtValue.CreateTensorValueFromMemory(new[] { speed }, [1]);
        using var options = new RunOptions();
        using var outputs = session.Run(options, [inputNames, "style", "speed"], [idsValue, styleValue, speedValue], [session.OutputNames[0]]);
        samples.AddRange(outputs[0].GetTensorDataAsSpan<float>());
    }

    /// <summary>Speaks text, one window of at most 510 tokens at a time.</summary>
    /// <param name="text">The text.</param>
    /// <param name="voiceId">The voice.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="cancellationToken">Cancels between windows.</param>
    /// <returns>The audio.</returns>
    private SpeechAudio Synthesize(string text, string voiceId, float speed, CancellationToken cancellationToken)
    {
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                var british = KokoroModel.IsBritish(voiceId);
                var phonemes = GetPhonemizer(british).Phonemize(TextNormalizer.Normalize(text.AsSpan(), british, _normalizing));
                KokoroVocabulary.Tokenize(phonemes, _tokens);
                if (_tokens.Count == 0)
                {
                    return new([], KokoroModel.SampleRate);
                }

                var session = GetSession();
                var style = GetStyle(voiceId);
                var samples = new List<float>();
                var tokens = CollectionsMarshal.AsSpan(_tokens);
                for (var start = 0; start < tokens.Length; start += MaxTokens)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Infer(session, tokens.Slice(start, Math.Min(MaxTokens, tokens.Length - start)), style, speed, samples);
                }

                return new([.. samples], KokoroModel.SampleRate);
            }
        }
        finally
        {
            ReleaseIfDisposed();
        }
    }

    /// <summary>
    /// Frees the ONNX session when the engine is disposed and no call holds the gate. A call that holds it frees the session
    /// itself when it leaves, so the session is released by whoever leaves last.
    /// </summary>
    private void ReleaseIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 0 || !_gate.TryEnter())
        {
            return;
        }

        try
        {
            ReleaseSession();
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>Frees the ONNX session. Callers hold the gate.</summary>
    private void ReleaseSession()
    {
        _session?.Dispose();
        _session = null;
    }

    /// <summary>Gets the phonemizer for an accent, loading its dictionaries on first use. Callers hold the gate.</summary>
    /// <param name="british">Whether British.</param>
    /// <returns>The phonemizer.</returns>
    private EnglishPhonemizer GetPhonemizer(bool british)
    {
        if (_phonemizers.TryGetValue(british, out var cached))
        {
            return cached;
        }

        var files = KokoroModel.LexiconFiles(british);
        var dictionaries = new byte[files.Length][];
        for (var i = 0; i < files.Length; i++)
        {
            dictionaries[i] = File.ReadAllBytes(Path.Combine(_directory, files[i]));
        }

        var phonemizer = new EnglishPhonemizer(PronunciationLexicon.Load(dictionaries), british);
        _phonemizers[british] = phonemizer;
        return phonemizer;
    }

    /// <summary>Gets the ONNX session, loading the model on first use. Callers hold the gate.</summary>
    /// <returns>The session.</returns>
    private InferenceSession GetSession()
    {
        if (_session is not null)
        {
            return _session;
        }

        using var options = new SessionOptions { IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount - ReservedProcessors, 1, MaxThreads) };
        _session = new(Path.Combine(_directory, KokoroModel.ModelFile), options);
        return _session;
    }

    /// <summary>Gets a voice's style vectors, loading the voice pack on first use. Callers hold the gate.</summary>
    /// <param name="voiceId">The voice.</param>
    /// <returns>The styles.</returns>
    private float[] GetStyle(string voiceId)
    {
        if (_styles.TryGetValue(voiceId, out var cached))
        {
            return cached;
        }

        var bytes = File.ReadAllBytes(Path.Combine(_directory, KokoroModel.VoiceFile(voiceId)));
        var style = MemoryMarshal.Cast<byte, float>(bytes).ToArray();
        _styles[voiceId] = style;
        return style;
    }
}

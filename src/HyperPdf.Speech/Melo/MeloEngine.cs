// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HyperPdf.Core.Speech;
using HyperPdf.Speech.English;
using Microsoft.ML.OnnxRuntime;

namespace HyperPdf.Speech.Melo;

/// <summary>
/// Reads aloud on this computer with MeloTTS-English through ONNX Runtime: Australian, British, American and Indian
/// voices, no account and nothing sent anywhere. Text becomes ARPAbet phones, BERT reads the sentence for its
/// prosody, and the synthesizer turns both into 44.1 kHz audio. No Python is involved: the front end is C#.
/// </summary>
[DebuggerDisplay("MeloEngine: {Name}, ready={IsReady}")]
public sealed class MeloEngine : ISpeechEngine
{
    /// <summary>The longest piece of text read at once, in characters, as MeloTTS splits long text.</summary>
    private const int MaxPieceLength = 400;

    /// <summary>The width of a BERT feature.</summary>
    private const int BertWidth = 768;

    /// <summary>How much the voice varies from one reading to the next; MeloTTS's default.</summary>
    private const float Noise = 0.6F;

    /// <summary>How much the timing varies; MeloTTS's default.</summary>
    private const float DurationNoise = 0.8F;

    /// <summary>How much of the timing comes from the stochastic duration predictor; MeloTTS's default.</summary>
    private const float StochasticShare = 0.2F;

    /// <summary>The fewest phones, with blanks and padding, that hold a sound.</summary>
    private const int MinPhones = 6;

    /// <summary>The processors left to the rest of the app (drawing pages, the window) while speech is made.</summary>
    private const int ReservedProcessors = 1;

    /// <summary>The most threads inference uses, leaving the rest of the computer responsive.</summary>
    private const int MaxThreads = 4;

    /// <summary>The level below which a sample counts as silence.</summary>
    private const float Quiet = 0.005F;

    /// <summary>The silence kept after speech, in seconds.</summary>
    private const double TrailingPause = 0.45;

    /// <summary>The slowest speed.</summary>
    private const float MinSpeed = 0.25F;

    /// <summary>The fastest speed.</summary>
    private const float MaxSpeed = 4F;

    /// <summary>The characters MeloTTS drops before reading: brackets and double quotes.</summary>
    private static readonly SearchValues<char> Dropped = SearchValues.Create("<>()[]\"«»“”");

    /// <summary>The voice variation, as the model's input.</summary>
    private static readonly float[] NoiseValue = [Noise];

    /// <summary>The timing variation, as the model's input.</summary>
    private static readonly float[] DurationNoiseValue = [DurationNoise];

    /// <summary>The stochastic share, as the model's input.</summary>
    private static readonly float[] StochasticShareValue = [StochasticShare];

    /// <summary>The voice folder.</summary>
    private readonly string _directory;

    /// <summary>Serialises inference and lazy loading.</summary>
    private readonly Lock _gate = new();

    /// <summary>The model's input, reused.</summary>
    private readonly MeloInput _input = new();

    /// <summary>The front end, loaded on first use.</summary>
    private MeloFrontEnd? _frontEnd;

    /// <summary>The synthesizer session, loaded on first use.</summary>
    private InferenceSession? _synthesizer;

    /// <summary>The BERT session, loaded on first use.</summary>
    private InferenceSession? _bert;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="MeloEngine"/> class.</summary>
    /// <param name="directory">The folder holding the model files.</param>
    public MeloEngine(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        _directory = directory;
    }

    /// <inheritdoc/>
    public string Name => "On this computer (MeloTTS)";

    /// <inheritdoc/>
    public bool IsReady
    {
        get
        {
            foreach (var file in MeloModel.Files)
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
    public IReadOnlyList<SpeechVoice> Voices => MeloModel.Voices;

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

    /// <summary>Turns text into the phone symbols the voice would read, for tests and diagnostics.</summary>
    /// <param name="text">The text.</param>
    /// <param name="voiceId">The voice, which sets how dates and numbers are read.</param>
    /// <returns>The phones, separated by spaces, without blanks.</returns>
    public string Phonemize(string text, string voiceId)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(voiceId);
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                var frontEnd = GetFrontEnd();
                frontEnd.Prepare(Normalize(text, MeloModel.IsBritish(voiceId)), _input);
                var names = new StringBuilder();
                foreach (var id in _input.Phones)
                {
                    if (id != MeloSymbols.Blank)
                    {
                        _ = names.Append(names.Length > 0 ? " " : string.Empty).Append(frontEnd.Symbols.Name(id));
                    }
                }

                return names.ToString();
            }
        }
        finally
        {
            ReleaseIfDisposed();
        }
    }

    /// <summary>
    /// Prepares text as MeloTTS does before its front end: words in camel case are split, quotes straightened, brackets
    /// dropped, then numbers, dates and symbols written out in lower case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="british">Whether dates and spellings are British.</param>
    /// <returns>The prepared text.</returns>
    internal static string Normalize(string text, bool british)
    {
        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (i > 0 && char.IsAsciiLetterUpper(c) && char.IsAsciiLetterLower(text[i - 1]))
            {
                _ = builder.Append(' ');
            }

            Append(builder, c);
        }

        return TextNormalizer.Normalize(builder.ToString(), british).ToLowerInvariant();
    }

    /// <summary>Appends a character as MeloTTS reads it: brackets and double quotes dropped, single quotes straightened.</summary>
    /// <param name="builder">The text so far.</param>
    /// <param name="c">The character.</param>
    private static void Append(StringBuilder builder, char c)
    {
        if (!Dropped.Contains(c))
        {
            _ = builder.Append(c is '‘' or '’' ? '\'' : c);
        }
    }

    /// <summary>Cuts long text into pieces of at most 400 characters at spaces.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">Where the next piece starts.</param>
    /// <returns>The piece's length.</returns>
    private static int NextPiece(string text, int start)
    {
        var remaining = text.Length - start;
        if (remaining <= MaxPieceLength)
        {
            return remaining;
        }

        var space = text.LastIndexOf(' ', start + MaxPieceLength, MaxPieceLength);
        return space > start ? space - start : MaxPieceLength;
    }

    /// <summary>
    /// Finds where speech ends plus a natural pause. MeloTTS ends every reading with about 0.8 s of silence; Read Aloud
    /// adds its own pause between sentences, so the tail is cut to <see cref="TrailingPause"/> to keep pauses even.
    /// </summary>
    /// <param name="samples">The samples.</param>
    /// <param name="sampleRate">The sample rate.</param>
    /// <returns>How many samples to keep.</returns>
    private static int Trimmed(List<float> samples, int sampleRate)
    {
        var span = CollectionsMarshal.AsSpan(samples);
        var last = span.Length - 1;
        while (last >= 0 && Math.Abs(span[last]) < Quiet)
        {
            last--;
        }

        return Math.Min(span.Length, last + 1 + (int)(TrailingPause * sampleRate));
    }

    /// <summary>Lines BERT's features up with the phones: each phone takes the feature of the token covering it.</summary>
    /// <param name="hidden">BERT's output, one row of 768 per token.</param>
    /// <param name="input">The model's input.</param>
    /// <returns>The features as one 768 by phone-count matrix.</returns>
    private static float[] AlignFeatures(ReadOnlySpan<float> hidden, MeloInput input)
    {
        var phones = input.Phones.Count;
        var features = new float[BertWidth * phones];
        var phone = 0;
        var counts = CollectionsMarshal.AsSpan(input.PhonesPerToken);
        for (var token = 0; token < counts.Length; token++)
        {
            var row = hidden.Slice(token * BertWidth, BertWidth);
            for (var n = 0; n < counts[token] && phone < phones; n++, phone++)
            {
                for (var d = 0; d < BertWidth; d++)
                {
                    features[(d * phones) + phone] = row[d];
                }
            }
        }

        return features;
    }

    /// <summary>Speaks text, one piece at a time.</summary>
    /// <param name="text">The text.</param>
    /// <param name="voiceId">The voice.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="cancellationToken">Cancels between pieces.</param>
    /// <returns>The audio.</returns>
    private SpeechAudio Synthesize(string text, string voiceId, float speed, CancellationToken cancellationToken)
    {
        try
        {
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                var frontEnd = GetFrontEnd();
                var symbols = frontEnd.Symbols;
                if (!symbols.Speakers.TryGetValue(voiceId, out var speaker))
                {
                    speaker = symbols.Speakers[MeloModel.Voices[0].Id];
                }

                var prepared = Normalize(text, MeloModel.IsBritish(voiceId));
                var samples = new List<float>();
                for (var start = 0; start < prepared.Length;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var length = NextPiece(prepared, start);
                    frontEnd.Prepare(prepared[start..(start + length)], _input);
                    start += length;
                    if (_input.Phones.Count >= MinPhones)
                    {
                        Infer(speaker, speed, samples);
                    }
                }

                return new(CollectionsMarshal.AsSpan(samples)[..Trimmed(samples, symbols.SampleRate)].ToArray(), symbols.SampleRate);
            }
        }
        finally
        {
            ReleaseIfDisposed();
        }
    }

    /// <summary>
    /// Frees the ONNX sessions when the engine is disposed and no call holds the gate. A call that holds it frees them
    /// itself when it leaves, so the sessions are released by whoever leaves last.
    /// </summary>
    private void ReleaseIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 0 || !_gate.TryEnter())
        {
            return;
        }

        try
        {
            ReleaseSessions();
        }
        finally
        {
            _gate.Exit();
        }
    }

    /// <summary>Frees the ONNX sessions. Callers hold the gate.</summary>
    private void ReleaseSessions()
    {
        _synthesizer?.Dispose();
        _synthesizer = null;
        _bert?.Dispose();
        _bert = null;
    }

    /// <summary>Runs BERT and the synthesizer on the prepared input. Callers hold the gate.</summary>
    /// <param name="speaker">The speaker id.</param>
    /// <param name="speed">The speed.</param>
    /// <param name="samples">The samples so far.</param>
    private void Infer(int speaker, float speed, List<float> samples)
    {
        var (synthesizer, bert) = GetSessions();
        var tokens = _input.Tokens.ToArray();
        var mask = new long[tokens.Length];
        Array.Fill(mask, 1L);
        using var options = new RunOptions();
        float[] features;
        using (var ids = OrtValue.CreateTensorValueFromMemory(tokens, [1, tokens.Length]))
        using (var attention = OrtValue.CreateTensorValueFromMemory(mask, [1, tokens.Length]))
        using (var types = OrtValue.CreateTensorValueFromMemory(new long[tokens.Length], [1, tokens.Length]))
        using (var hidden = bert.Run(options, ["input_ids", "attention_mask", "token_type_ids"], [ids, attention, types], ["hidden"]))
        {
            features = AlignFeatures(hidden[0].GetTensorDataAsSpan<float>(), _input);
        }

        var phones = _input.Phones.Count;
        using var x = OrtValue.CreateTensorValueFromMemory(_input.Phones.ToArray(), [1, phones]);
        using var lengths = OrtValue.CreateTensorValueFromMemory(new long[] { phones }, [1]);
        using var tones = OrtValue.CreateTensorValueFromMemory(_input.Tones.ToArray(), [1, phones]);
        using var languages = OrtValue.CreateTensorValueFromMemory(_input.Languages.ToArray(), [1, phones]);
        using var prosody = OrtValue.CreateTensorValueFromMemory(features, [1, BertWidth, phones]);
        using var sid = OrtValue.CreateTensorValueFromMemory(new long[] { speaker }, [1]);
        using var noise = OrtValue.CreateTensorValueFromMemory(NoiseValue, []);
        using var lengthScale = OrtValue.CreateTensorValueFromMemory(new[] { 1F / Math.Clamp(speed, MinSpeed, MaxSpeed) }, []);
        using var durationNoise = OrtValue.CreateTensorValueFromMemory(DurationNoiseValue, []);
        using var stochastic = OrtValue.CreateTensorValueFromMemory(StochasticShareValue, []);
        using var audio = synthesizer.Run(
            options,
            ["x", "x_lengths", "tones", "languages", "ja_bert", "sid", "noise_scale", "length_scale", "noise_scale_w", "sdp_ratio"],
            [x, lengths, tones, languages, prosody, sid, noise, lengthScale, durationNoise, stochastic],
            ["audio"]);
        samples.AddRange(audio[0].GetTensorDataAsSpan<float>());
    }

    /// <summary>Gets the front end, loading its files on first use. Callers hold the gate.</summary>
    /// <returns>The front end.</returns>
    private MeloFrontEnd GetFrontEnd()
    {
        if (_frontEnd is not null)
        {
            return _frontEnd;
        }

        _frontEnd = MeloFrontEnd.Load(_directory);
        return _frontEnd;
    }

    /// <summary>Gets the ONNX sessions, loading the models on first use. Callers hold the gate.</summary>
    /// <returns>The synthesizer and BERT sessions.</returns>
    private (InferenceSession Synthesizer, InferenceSession Bert) GetSessions()
    {
        if (_synthesizer is null || _bert is null)
        {
            using var options = new SessionOptions { IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount - ReservedProcessors, 1, MaxThreads) };
            _synthesizer ??= new(Path.Combine(_directory, MeloModel.ModelFile), options);
            _bert ??= new(Path.Combine(_directory, MeloModel.BertFile), options);
        }

        return (_synthesizer, _bert);
    }
}

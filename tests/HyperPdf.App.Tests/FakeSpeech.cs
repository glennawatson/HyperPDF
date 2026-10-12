// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.App.Services;
using HyperPdf.Core.Speech;

namespace HyperPdf.App.Tests;

/// <summary>A speech engine and sound output that record what they are asked to do, so tests never make a sound.</summary>
[DebuggerDisplay("FakeSpeech: Spoken={Spoken.Count}, Played={Played}")]
internal sealed class FakeSpeech : ISpeechEngine, IAudioOutput
{
    /// <summary>The sample rate of the fake audio.</summary>
    private const int Hertz = 24_000;

    /// <summary>The samples in each fake clip, a hundredth of a second.</summary>
    private const int ClipSamples = 240;

    /// <summary>The voices offered.</summary>
    private static readonly SpeechVoice[] FakeVoices = [new("fake_one", "One", "Test voice"), new("fake_two", "Two", "Second test voice")];

    /// <summary>Holds each clip until released, when set.</summary>
    private readonly SemaphoreSlim? _gate;

    /// <summary>The number of clips played.</summary>
    private int _played;

    /// <summary>Initializes a new instance of the <see cref="FakeSpeech"/> class.</summary>
    /// <param name="ready">Whether the voice is ready.</param>
    /// <param name="holdEachClip">Whether each clip waits for <see cref="Release"/>.</param>
    internal FakeSpeech(bool ready, bool holdEachClip)
    {
        IsReady = ready;
        _gate = holdEachClip ? new(0) : null;
    }

    /// <inheritdoc/>
    public string Name => "Fake";

    /// <inheritdoc/>
    public bool IsReady { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<SpeechVoice> Voices => FakeVoices;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <summary>Gets the text of each sentence asked for, in order, with its voice and speed.</summary>
    internal ConcurrentQueue<(string Text, string Voice, float Speed)> Spoken { get; } = new();

    /// <summary>Gets the number of clips played to the end.</summary>
    internal int Played => Volatile.Read(ref _played);

    /// <summary>Gets the number of times the voice was downloaded.</summary>
    internal int Downloads { get; private set; }

    /// <inheritdoc/>
    public Task<SpeechAudio> SynthesizeAsync(string text, string voiceId, float speed, CancellationToken cancellationToken)
    {
        Spoken.Enqueue((text, voiceId, speed));
        return Task.FromResult(new SpeechAudio(new float[ClipSamples], Hertz));
    }

    /// <inheritdoc/>
    public async Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken)
    {
        if (_gate is not null)
        {
            await _gate.WaitAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _ = Interlocked.Increment(ref _played);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // The test owns the fake; the services dispose it more than once.
    }

    /// <summary>Creates a setup using this fake.</summary>
    /// <param name="directory">The voice folder.</param>
    /// <returns>The setup.</returns>
    internal SpeechSetup CreateSetup(string directory) =>
        new(directory, static _ => [], (_, _) => this, () => this, (_, progress, _) => DownloadAsync(progress));

    /// <summary>Lets the clip being played finish.</summary>
    internal void Release() => _gate?.Release();

    /// <summary>Pretends to download the voice.</summary>
    /// <param name="progress">The progress.</param>
    /// <returns>A task.</returns>
    private Task DownloadAsync(IProgress<double> progress)
    {
        Downloads++;
        progress.Report(1);
        IsReady = true;
        return Task.CompletedTask;
    }
}

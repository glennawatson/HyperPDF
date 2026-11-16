// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Speech;

namespace HyperPdf.ViewModels.Reading;

/// <summary>The next sentence's audio, which the voice prepares while the current sentence plays; disposing stops any preparing that is still going.</summary>
internal sealed class SpeechPrefetch : IDisposable
{
    /// <summary>Stops the preparing of each sentence when disposed.</summary>
    private readonly MultipleDisposable _preparing = new();

    /// <summary>Gets the replayed audio of the next sentence, or <see langword="null"/> when none is being prepared.</summary>
    internal IObservable<SpeechAudio>? Prepared { get; private set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _preparing.Dispose();

    /// <summary>Starts preparing the next sentence now; whoever asks for the audio later gets it from the start.</summary>
    /// <param name="audio">The voice's cold signal for the sentence.</param>
    internal void Begin(IObservable<SpeechAudio> audio)
    {
        var replay = audio.Replay();
        Prepared = replay;
        _preparing.Add(replay.Connect());
    }

    /// <summary>Forgets the prepared sentence, because there is no next one.</summary>
    internal void Clear() => Prepared = null;
}

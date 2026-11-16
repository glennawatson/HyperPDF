// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;

namespace HyperPdf.Core.Speech;

/// <summary>Plays audio through the computer's speakers.</summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Gets a value indicating whether audio can be played.</summary>
    bool IsAvailable { get; }

    /// <summary>Plays audio, ending when it has finished playing.</summary>
    /// <param name="audio">The audio.</param>
    /// <returns>A cold signal giving one value once the audio has been heard; unsubscribing stops playing straight away.</returns>
    IObservable<RxVoid> Play(SpeechAudio audio);
}

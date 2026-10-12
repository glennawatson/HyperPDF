// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Speech;

/// <summary>Plays audio through the computer's speakers.</summary>
public interface IAudioOutput : IDisposable
{
    /// <summary>Gets a value indicating whether audio can be played.</summary>
    bool IsAvailable { get; }

    /// <summary>Plays audio, completing when it has finished playing.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops playing straight away.</param>
    /// <returns>A task.</returns>
    Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken);
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Speech;

/// <summary>Turns text into speech. One sentence is spoken at a time; an engine is used by one reader at a time.</summary>
public interface ISpeechEngine : IDisposable
{
    /// <summary>Gets the engine's name, for example "On this computer (Kokoro)".</summary>
    string Name { get; }

    /// <summary>Gets a value indicating whether the engine can speak now, for example once its voice files are installed.</summary>
    bool IsReady { get; }

    /// <summary>Gets the voices.</summary>
    IReadOnlyList<SpeechVoice> Voices { get; }

    /// <summary>Speaks a piece of text into audio.</summary>
    /// <param name="text">The text, normally one sentence.</param>
    /// <param name="voiceId">The voice's id.</param>
    /// <param name="speed">The speed, 1 for normal.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The audio.</returns>
    Task<SpeechAudio> SynthesizeAsync(string text, string voiceId, float speed, CancellationToken cancellationToken);
}

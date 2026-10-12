// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Speech;

/// <summary>Spoken audio: mono samples from -1 to 1.</summary>
/// <param name="Samples">The samples.</param>
/// <param name="SampleRate">The samples per second.</param>
[DebuggerDisplay("SpeechAudio: {Samples.Length} samples at {SampleRate} Hz")]
public sealed record SpeechAudio(float[] Samples, int SampleRate)
{
    /// <summary>Gets how long the audio plays.</summary>
    public TimeSpan Duration => SampleRate <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}

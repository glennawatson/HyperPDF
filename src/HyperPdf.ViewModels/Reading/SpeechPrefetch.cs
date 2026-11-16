// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Speech;

namespace HyperPdf.ViewModels.Reading;

/// <summary>The next sentence's audio, which the voice prepares while the current sentence plays.</summary>
internal sealed class SpeechPrefetch
{
    /// <summary>Gets or sets the voice's task for the next sentence, or <see langword="null"/> when none is being prepared.</summary>
    internal Task<SpeechAudio>? Prepared { get; set; }
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>Which voice reads documents aloud.</summary>
public enum SpeechEngineChoice
{
    /// <summary>MeloTTS, the natural sounding voice that runs on this computer, downloaded once; the default. Nothing leaves the computer.</summary>
    OnDevice = 0,

    /// <summary>Azure AI Speech with the person's own key; each sentence is sent to their Azure resource.</summary>
    Azure = 1,

    /// <summary>Kokoro, the other voice that runs on this computer, downloaded once. Nothing leaves the computer.</summary>
    Kokoro = 2,
}

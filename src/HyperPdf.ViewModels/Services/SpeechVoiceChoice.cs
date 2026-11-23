// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Speech;

namespace HyperPdf.ViewModels.Services;

/// <summary>One voice a person can pick for reading aloud, and the engine that speaks it. Picking the voice picks the engine.</summary>
/// <param name="Engine">The engine that speaks the voice.</param>
/// <param name="EngineName">The engine's name, which is a product name and is not translated.</param>
/// <param name="Voice">The voice.</param>
[DebuggerDisplay("SpeechVoiceChoice: {EngineName} {Voice.Name}")]
public sealed record SpeechVoiceChoice(SpeechEngineChoice Engine, string EngineName, SpeechVoice Voice);

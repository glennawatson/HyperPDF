// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Speech;

/// <summary>A voice that can read aloud.</summary>
/// <param name="Id">The engine's id for the voice.</param>
/// <param name="Name">The name shown, for example "Heart".</param>
/// <param name="Description">A short description, for example "American English, warm".</param>
[DebuggerDisplay("SpeechVoice: {Name} ({Id})")]
public sealed record SpeechVoice(string Id, string Name, string Description);

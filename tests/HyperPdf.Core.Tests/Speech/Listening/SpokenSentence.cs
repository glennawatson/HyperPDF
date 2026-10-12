// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Speech;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>One sentence read by the voice.</summary>
/// <param name="Text">The sentence as spoken.</param>
/// <param name="Audio">The audio.</param>
/// <param name="Measurement">What a listener notices about it.</param>
/// <param name="Work">How long synthesis took.</param>
[DebuggerDisplay("SpokenSentence: {Measurement}: {Text}")]
internal sealed record SpokenSentence(string Text, SpeechAudio Audio, SpeechMeasurement Measurement, TimeSpan Work);

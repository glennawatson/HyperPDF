// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Tests.Speech.Listening;

/// <summary>A passage read sentence by sentence.</summary>
/// <param name="Sentences">The sentences, in order.</param>
[DebuggerDisplay("ListeningSession: {Sentences.Count} sentences")]
internal sealed record ListeningSession(List<SpokenSentence> Sentences);

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Speech;

/// <summary>A sentence of a page's text, by character position, so it can be highlighted while it is read.</summary>
/// <param name="Start">The first character.</param>
/// <param name="Length">The number of characters.</param>
[DebuggerDisplay("SpeechSentence: {Start}+{Length}")]
public readonly record struct SpeechSentence(int Start, int Length);

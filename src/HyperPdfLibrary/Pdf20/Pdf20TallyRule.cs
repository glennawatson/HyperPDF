// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Pdf20;

/// <summary>A sentence the summary says when a count of the dictionary pass is not zero.</summary>
/// <param name="Count">Reads the count from the tally.</param>
/// <param name="Clause">The clause of ISO 32000-2 behind the sentence.</param>
/// <param name="Text">The sentence in plain words.</param>
[DebuggerDisplay("Pdf20TallyRule: {Clause}")]
internal sealed record Pdf20TallyRule(Func<Pdf20ConversionTally, int> Count, string Clause, string Text);

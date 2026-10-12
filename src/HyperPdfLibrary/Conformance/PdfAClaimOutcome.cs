// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>What a writer does after checking its output against the claimed PDF/A level.</summary>
/// <param name="Check">The check, for the caller's report.</param>
/// <param name="Repaired">The repaired file to write instead of the output, or <see langword="null"/>.</param>
internal readonly record struct PdfAClaimOutcome(PdfAClaimCheck Check, byte[]? Repaired);

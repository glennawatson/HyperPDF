// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>A plan with the reasons signing is refused and what the user should be told.</summary>
/// <param name="Plan">The plan; its values are usable only when <paramref name="Problems"/> is empty.</param>
/// <param name="Problems">Every reason signing is refused.</param>
/// <param name="Notes">What the user should know.</param>
/// <param name="TimestampUrl">The timestamp authority the seed values name, or <see langword="null"/>.</param>
/// <param name="TimestampRequired">Whether the seed values require a signature timestamp.</param>
[DebuggerDisplay("PdfSigningPlanResult: {Problems.Count} problems")]
internal sealed record PdfSigningPlanResult(PdfSigningPlan Plan, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes, string? TimestampUrl, bool TimestampRequired);

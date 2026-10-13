// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// The outcome of checking all of a document's requirements (ISO 32000-2 §12.11.3 and §12.11.6). The penalties of the
/// unmet requirements are added up; a total of 100 or more means the author's intent cannot be produced.
/// </summary>
/// <param name="Results">One result for each requirement, in array order.</param>
/// <param name="TotalPenalty">The sum of the penalties of the unmet requirements.</param>
/// <param name="Decision">What the total allows.</param>
/// <param name="Summary">What the user should be told in plain words, or an empty string when every requirement is met.</param>
[DebuggerDisplay("PdfRequirementAssessment: {Decision}, penalty {TotalPenalty}")]
public sealed record PdfRequirementAssessment(IReadOnlyList<PdfRequirementResult> Results, int TotalPenalty, PdfRequirementDecision Decision, string Summary)
{
    /// <summary>Gets the assessment of a document with no requirements.</summary>
    public static PdfRequirementAssessment Empty { get; } = new([], 0, PdfRequirementDecision.Satisfied, string.Empty);

    /// <summary>Gets a value indicating whether the document may be processed: its requirements are met or optional.</summary>
    public bool AllowsProcessing => Decision != PdfRequirementDecision.Critical;

    /// <summary>
    /// Gets a value indicating whether the total penalty is over 100, the point where the standard says a processor should not
    /// try to display or process the document at all (§12.11.3).
    /// </summary>
    public bool ExceedsDisplayLimit => TotalPenalty > PdfRequirementEvaluator.CriticalPenalty;
}

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>The result of checking one requirement against a viewer's capabilities.</summary>
/// <param name="Requirement">The requirement that was checked.</param>
/// <param name="Outcome">What the check found.</param>
/// <param name="Feature">The feature the requirement names in plain words, or the requirement type when the viewer does not know it.</param>
/// <param name="Message">What the check found, in plain words for the user.</param>
/// <param name="HandlersDisabled">
/// Whether the viewer could check this requirement itself, so the document's alternative handlers for it are to be disabled
/// (ISO 32000-2 §12.11.5). Handlers are never run either way.
/// </param>
[DebuggerDisplay("PdfRequirementResult: {Requirement.Type} {Outcome}")]
public sealed record PdfRequirementResult(PdfRequirement Requirement, PdfRequirementOutcome Outcome, string Feature, string Message, bool HandlersDisabled)
{
    /// <summary>Gets a value indicating whether the requirement is met.</summary>
    public bool IsMet => Outcome == PdfRequirementOutcome.Met;

    /// <summary>Gets the penalty this requirement adds: zero when it is met, otherwise its own penalty.</summary>
    public int AppliedPenalty => IsMet ? 0 : Requirement.Penalty;
}

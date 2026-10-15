// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// What a save found when it checked a file that claims PDF/UA-1 (ISO 14289-1:2014). The checks cover the rules listed
/// in <see cref="PdfUaRequirement"/>; they are a practical check, not a certification, so use veraPDF or the PDF
/// Accessibility Checker to prove a file. The problems the save could fix are repaired in the document in memory as one
/// undoable edit, so the written file has them fixed.
/// </summary>
/// <param name="Problems">Every problem found, repaired or not, in requirement order.</param>
[DebuggerDisplay("PdfUaClaimCheck: {Problems.Count} problems, removed {Removed}")]
public sealed record PdfUaClaimCheck(IReadOnlyList<PdfUaProblem> Problems)
{
    /// <summary>Gets a value indicating whether the save removed the claim because a problem could not be fixed and the caller chose to remove it.</summary>
    public bool Removed { get; init; }

    /// <summary>Gets a value indicating whether the file met every checked rule without any repair.</summary>
    public bool Passed => Problems.Count == 0;

    /// <summary>Lists the problems the save fixed. Tell the user what each one was.</summary>
    /// <returns>The problems.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfUaProblem[] GetRepaired() => Select(true);

    /// <summary>Lists the problems the save could not fix. They are why the save was refused, or why the claim was removed.</summary>
    /// <returns>The problems.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfUaProblem[] GetUnrepaired() => Select(false);

    /// <summary>Picks the problems that were, or were not, repaired.</summary>
    /// <param name="repaired">Whether to pick the repaired ones.</param>
    /// <returns>The problems.</returns>
    private PdfUaProblem[] Select(bool repaired)
    {
        var picked = new List<PdfUaProblem>(Problems.Count);
        foreach (var problem in Problems)
        {
            if (problem.Repaired == repaired)
            {
                picked.Add(problem);
            }
        }

        return [.. picked];
    }
}

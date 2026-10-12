// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// What a save found when it checked the written file against the PDF/A level the document claims. A file that breaks the
/// level is repaired when it can be: the saved file is then a converted copy that meets the level, and the document in
/// memory is unchanged, so the next save repairs it again. Only when the file cannot be repaired is the claim removed.
/// </summary>
/// <param name="Level">The level the document claimed.</param>
/// <param name="Removed">
/// Whether the claim was removed because the file breaks the level and could not be repaired. Show the user a warning
/// when it was, with the reasons in <see cref="Problems"/> and the rules in <paramref name="Broken"/>.
/// </param>
/// <param name="Broken">The rules the written file broke before any repair; empty when it met the level.</param>
[DebuggerDisplay("PdfAClaimCheck: {Level}, repaired {Repaired}, removed {Removed}, {Broken.Length} rules broken")]
public sealed record PdfAClaimCheck(PdfALevel Level, bool Removed, PdfARuleResult[] Broken)
{
    /// <summary>
    /// Gets a value indicating whether the file broke the level and the save wrote a repaired copy that meets it. Tell
    /// the user what <see cref="Repairs"/> lists.
    /// </summary>
    public bool Repaired { get; init; }

    /// <summary>Gets what the repair changed, in plain words; empty when nothing was repaired.</summary>
    public PdfAConversionNote[] Repairs { get; init; } = [];

    /// <summary>Gets why the file could not be repaired, in plain words; empty unless the claim was removed.</summary>
    public PdfAConversionNote[] Problems { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether an incremental save wrote the repaired file in full rather than appending an
    /// update, as a repair rewrites the whole file.
    /// </summary>
    public bool Rewritten { get; init; }
}

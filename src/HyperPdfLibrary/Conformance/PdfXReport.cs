// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A read-only check of a PDF/X claim. The raw claim stays visible even when checks fail. Only PDF/X-4 and PDF/X-4p have
/// rule sets so far; for other levels <see cref="Findings"/> is empty and <see cref="NotChecked"/> says so.
/// </summary>
/// <param name="Claim">The raw <c>GTS_PDFXVersion</c> text, or <see langword="null"/> when the file makes no claim.</param>
/// <param name="Level">The recognised level.</param>
/// <param name="Findings">Each check made, passed or failed.</param>
/// <param name="NotChecked">Requirements this report does not check, in plain words; they are never counted as passing.</param>
[DebuggerDisplay("PdfXReport: {Claim} {Level}, {Findings.Length} checks")]
public sealed record PdfXReport(string? Claim, PdfXLevel Level, PdfXFinding[] Findings, string[] NotChecked)
{
    /// <summary>Gets a value indicating whether the file claims PDF/X.</summary>
    public bool IsClaimed => Claim is not null;

    /// <summary>Gets a value indicating whether checks were made and every one passed.</summary>
    public bool AllChecksPassed => Findings.Length > 0 && Array.TrueForAll(Findings, static finding => finding.Passed);
}
